namespace TrafficManager.UI {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using ColossalFramework;
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using JetBrains.Annotations;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.API.Util;
    using TrafficManager.Lifecycle;
    using TrafficManager.State;
    using TrafficManager.U;
    using TrafficManager.UI.Helpers;
    using TrafficManager.UI.MainMenu;
    using TrafficManager.UI.MainMenu.OSD;
    using TrafficManager.UI.SubTools;
    using TrafficManager.UI.SubTools.SpeedLimits;
    using TrafficManager.Util;
    using UnityEngine;
    using static TrafficManager.Util.SegmentTraverser;
    using static TrafficManager.Util.Shortcuts;

    [UsedImplicitly]
    public class TrafficManagerTool
        : DefaultTool,
          IObserver<GlobalConfig> {
        // TODO [issue #710] Road adjust mechanism seem to have changed in Sunset Harbor DLC.
        // activate when we know the mechinism.
        private bool ReadjustPathMode => false; //ShiftIsPressed;

        // /// <summary>Set this to true to once call <see cref="RequestOnscreenDisplayUpdate"/>.</summary>
        // public bool InvalidateOnscreenDisplayFlag { get; set; }

        public GuideHandler Guide;

        private ToolMode toolMode_;

        private NetTool netTool_;

        private CursorInfo nopeCursor_;

        /// <summary>Maximum error of HitPos field.</summary>
        internal const float MAX_HIT_ERROR = 2.5f;

        /// <summary>Maximum detection radius of segment raycast hit position.</summary>
        internal const float NODE_DETECTION_RADIUS = 15f;

        internal static ushort HoveredNodeId;

        internal static ushort HoveredSegmentId;

        /// <summary>The hit position of the mouse raycast.</summary>
        internal static Vector3 HitPos;

        internal Vector3 MousePosition => m_mousePosition; //expose protected member.

        private static bool _mouseClickProcessed;

        public const float DEBUG_CLOSE_LOD = 300f;

        /// <summary>Square of the distance, where overlays are not rendered.</summary>
        public const float MAX_OVERLAY_DISTANCE_SQR = 450f * 450f;

        private IDictionary<ToolMode, LegacySubTool> legacySubTools_;

        private IDictionary<ToolMode, TrafficManagerSubTool> subTools_;

        public static ushort SelectedNodeId { get; internal set; }

        public static ushort SelectedSegmentId { get; internal set; }

        public static TransportDemandViewMode CurrentTransportDemandViewMode { get; internal set; }
            = TransportDemandViewMode.Outgoing;

        internal static ExtVehicleType[] InfoSignsToDisplay = {
            ExtVehicleType.PassengerCar, ExtVehicleType.Bicycle, ExtVehicleType.Bus,
            ExtVehicleType.Taxi, ExtVehicleType.Tram, ExtVehicleType.CargoTruck,
            ExtVehicleType.Service, ExtVehicleType.RailVehicle,
        };

        [Obsolete("Convert your legacy tools to new TrafficManagerSubTool style")]
        private LegacySubTool activeLegacySubTool_;

        private TrafficManagerSubTool activeSubTool_;

        private static IDisposable _confDisposable;

        static TrafficManagerTool() { }

        /// <summary>
        /// Determines if the current tool is traffic manager tool.
        /// </summary>
        public static bool IsCurrentTool =>
            ToolsModifierControl.toolController?.CurrentTool != null
            && ToolsModifierControl.toolController.CurrentTool is TrafficManagerTool;

        protected override void OnDestroy() {
            Log.Info("TrafficManagerTool.OnDestroy() called");
            if (nopeCursor_) {
                Destroy(nopeCursor_);
                nopeCursor_ = null;
            }

            base.OnDestroy();
        }

        internal ToolController GetToolController() {
            return m_toolController;
        }

        /// <summary>
        /// Defines initial screen location for tool Rect, based on default menu x and y,
        /// whatever tools need them for.
        /// </summary>
        /// <param name="rect">A rect to place.</param>
        /// <returns>New rect moved around screen.</returns>
        internal static Rect GetDefaultScreenPositionForRect(Rect rect) {
            // x := main menu x + rect.x
            // y := main menu y + main menu height + rect.y
            return new(
                MainMenuWindow.DEFAULT_MENU_X + rect.x,
                MainMenuWindow.DEFAULT_MENU_Y + rect.y + ModUI.Instance.MainMenu.height,
                rect.width,
                rect.height);
        }

        // TODO: Move to UI.Helpers
        internal static bool IsNodeWithinViewDistance(ushort nodeId) {
            return IsPosWithinOverlayDistance(nodeId.ToNode().m_position);
        }

        // TODO: Move to UI.Helpers
        internal static bool IsPosWithinOverlayDistance(Vector3 position) {
            return (position - Singleton<SimulationManager>.instance.m_simulationView.m_position)
                   .sqrMagnitude <= MAX_OVERLAY_DISTANCE_SQR;
        }

        [Obsolete("Use U.UIScaler and U size and position logic")]
        internal static float AdaptWidth(float originalWidth) {
            return originalWidth;
            // return originalWidth * ((float)Screen.width / 1920f);
        }

        internal const float MAX_ZOOM = 0.05f;

        internal static float GetWindowAlpha() {
            return TransparencyToAlpha(GlobalConfig.Instance.Main.GuiTransparency);
        }

        internal static float GetHandleAlpha(bool hovered) {
            byte transparency = GlobalConfig.Instance.Main.OverlayTransparency;
            if (hovered) {
                // reduce transparency when handle is hovered
                transparency = (byte)Math.Min(20, transparency >> 2);
            }

            return TransparencyToAlpha(transparency);
        }

        /// <summary>Gives convenient access to NetTool from the original game.</summary>
        private NetTool NetTool {
            get {
                if (netTool_ == null) {
                    Log._Debug("NetTool field value is null. Searching for instance...");
                    netTool_ = ToolsModifierControl.toolController.Tools.OfType<NetTool>()
                                                   .FirstOrDefault();
                }

                return netTool_;
            }
        }

        private static float TransparencyToAlpha(byte transparency) {
            return Mathf.Clamp(100 - transparency, 0f, 100f) / 100f;
        }

        internal void Initialize() {
            Log.Info("TrafficManagerTool: Initialization running now.");
            Guide = new GuideHandler();

            LegacySubTool timedLightsTool =
                new SubTools.TimedTrafficLights.TimedTrafficLightsTool(this);

            subTools_ = new TinyDictionary<ToolMode, TrafficManagerSubTool> {
                [ToolMode.LaneArrows] = new SubTools.LaneArrows.LaneArrowTool(this),
                [ToolMode.SpeedLimits] = new SpeedLimitsTool(this),
            };
            legacySubTools_ = new TinyDictionary<ToolMode, LegacySubTool> {
                [ToolMode.ToggleTrafficLight] = new ToggleTrafficLightsTool(this),
                [ToolMode.AddPrioritySigns] = new SubTools.PrioritySigns.PrioritySignsTool(this),
                [ToolMode.ManualSwitch] = new ManualTrafficLightsTool(this),
                [ToolMode.TimedTrafficLights] = timedLightsTool,
                [ToolMode.VehicleRestrictions] = new VehicleRestrictionsTool(this),
                [ToolMode.LaneConnector] = new LaneConnectorTool(this),
                [ToolMode.JunctionRestrictions] = new JunctionRestrictionsTool(this),
                [ToolMode.ParkingRestrictions] = new ParkingRestrictionsTool(this),
            };

            InitializeSubTools();

            SetToolMode(ToolMode.None);

            _confDisposable?.Dispose();
            _confDisposable = GlobalConfig.Instance.Subscribe(this);

            Log.Info("TrafficManagerTool: Initialization completed.");
        }

        public void OnUpdate(GlobalConfig config) {
            InitializeSubTools();
        }

        internal void InitializeSubTools() {
            foreach (KeyValuePair<ToolMode, LegacySubTool> e in legacySubTools_) {
                e.Value.Initialize();
            }
        }

        protected override void Awake() {
            Log._Debug($"TrafficManagerTool: Awake {GetHashCode()}");
            nopeCursor_ = ScriptableObject.CreateInstance<CursorInfo>();
            nopeCursor_.m_texture = UIView.GetAView().defaultAtlas["Niet"]?.texture;
            nopeCursor_.m_hotspot = new Vector2(45, 45);
            base.Awake();
        }

        /// <summary>Only used from CustomRoadBaseAI.</summary>
        public LegacySubTool GetSubTool(ToolMode mode) {
            if (legacySubTools_.TryGetValue(mode, out LegacySubTool ret)) {
                return ret;
            }

            return null;
        }

        public ToolMode GetToolMode() {
            return toolMode_;
        }

        /// <summary>Deactivate current active tool. Set new active tool.</summary>
        /// <param name="newToolMode">New mode.</param>
        public void SetToolMode(ToolMode newToolMode) {
            ToolMode oldToolMode = toolMode_;

            if (toolMode_ != ToolMode.None && TMPELifecycle.PlayMode) {
                // Make it impossible for user to undo changes performed by Road selection panels
                // after changing traffic rule vis other tools.
                // TODO: This code will not be necessary when we implement intent.
                SimulationManager.instance.m_ThreadingWrapper.QueueMainThread(
                    RoadSelectionPanels.RoadWorldInfoPanel.Hide);

                if (RoadSelectionPanels.Root != null) {
                    // this can be null on mod reload
                    RoadSelectionPanels.Root.Function = RoadSelectionPanels.FunctionModes.None;
                }
            }

            bool toolModeChanged = newToolMode != toolMode_;

            if (!toolModeChanged) {
                Log._Debug($"SetToolMode: not changed old={oldToolMode} new={newToolMode}");
                return;
            }

            SetToolMode_DeactivateTool();
            // Try figure out whether legacy subtool or a new subtool is selected
            if (!legacySubTools_.TryGetValue(newToolMode, out activeLegacySubTool_)
                && !subTools_.TryGetValue(newToolMode, out activeSubTool_)) {
                activeLegacySubTool_ = null;
                activeSubTool_ = null;
                toolMode_ = ToolMode.None;

                Log._Debug($"SetToolMode: reset because toolmode not found {newToolMode}");
                OnscreenDisplay.DisplayIdle();
                ModUI.Instance.MainMenu.UpdateButtons();
                return;
            }

            SetToolMode_Activate(newToolMode);
            Log._Debug($"SetToolMode: changed old={oldToolMode} new={newToolMode}");
        }

        /// <summary>Resets the tool and calls deactivate on it.</summary>
        private void SetToolMode_DeactivateTool() {
            // Clear OSD panel with keybinds
            OnscreenDisplay.Clear();

            if (activeLegacySubTool_ != null || activeSubTool_ != null) {
                activeLegacySubTool_?.Cleanup();
                activeLegacySubTool_ = null;

                activeSubTool_?.DeactivateTool();
                activeSubTool_ = null;
                toolMode_ = ToolMode.None;
            }
        }

        /// <summary>
        /// Sets new active tool. Resets selected segment and node. Calls activate on tools.
        /// Also shows advisor.
        /// </summary>
        /// <param name="newToolMode">New mode.</param>
        private void SetToolMode_Activate(ToolMode newToolMode) {
            toolMode_ = newToolMode;
            SelectedNodeId = 0;
            SelectedSegmentId = 0;

            activeLegacySubTool_?.OnActivate();
            activeSubTool_?.ActivateTool();

            if (activeLegacySubTool_ != null) {
                ShowAdvisor(activeLegacySubTool_.GetTutorialKey());
                Guide.DeactivateAll();
            }
        }

        // Overridden to disable base class behavior
        protected override void OnEnable() {
            // If TMPE was enabled by switching back from another tool (eg: buldozer, free camera), show main menue panel.
            if (ModUI.Instance != null && !ModUI.Instance.IsVisible())
                ModUI.Instance.ShowMainMenu();

            if (legacySubTools_ != null) {
                Log._Debug("TrafficManagerTool.OnEnable(): Performing cleanup");
                foreach (KeyValuePair<ToolMode, LegacySubTool> e in legacySubTools_) {
                    e.Value.Cleanup();
                }
            }

            // Disabled camera may indicate the main camera change.
            // Reinitialize camera cache to make sure we will use the correct one
            if (!InGameUtil.Instance.CachedMainCamera.enabled) {
                Log.Info("CachedMainCamera disabled - camera cache reinitialization");
                InGameUtil.Instantiate();
            }

            // no call to base method to disable base class behavior
        }

        protected override void OnDisable() {
            // If TMPE was disabled by switching to another tool, hide main menue panel.
            if (ModUI.Instance != null && ModUI.Instance.IsVisible()) {
                ModUI.Instance.CloseMainMenu();
            }

            // revert to normal mode if underground
            if (Highlight.IsUndergroundMode) {
                InfoManager.instance.SetCurrentMode(
                    mode: InfoManager.InfoMode.None,
                    InfoManager.SubInfoMode.Default);
            }

            // hide speed limit overlay if necessary
            SubTools.PrioritySigns.MassEditOverlay.Show =
                RoadSelectionPanels.Root.ShouldShowMassEditOverlay();

            // no call to base method to disable base class behavior
        }

        public override void RenderGeometry(RenderManager.CameraInfo cameraInfo) {
            if (HoveredNodeId != 0) {
                m_toolController.RenderCollidingNotifications(cameraInfo, 0, 0);
            }
        }

        public override void RenderOverlay(RenderManager.CameraInfo cameraInfo) {
            RenderOverlayImpl(cameraInfo);
            if (GetToolMode() == ToolMode.None) {
                DefaultRenderOverlay(cameraInfo);
            }
        }

        /// <summary>
        /// renders presistent overlay.
        /// if any subtool is active it renders overlay for that subtool (e.g. node selection, segment selection, etc.)
        /// Must not call base.RenderOverlay() . Doing so may cause infinite recursion with Postfix of base.RenderOverlay()
        /// </summary>
        public void RenderOverlayImpl(RenderManager.CameraInfo cameraInfo) {
            if (!(isActiveAndEnabled || SubTools.PrioritySigns.MassEditOverlay.IsActive)) {
                return;
            }

            activeLegacySubTool_?.RenderOverlay(cameraInfo);
            activeSubTool_?.RenderActiveToolOverlay(cameraInfo);

            ToolMode currentMode = GetToolMode();

            // For all _other_ legacy subtools let them render something too
            foreach (KeyValuePair<ToolMode, LegacySubTool> e in legacySubTools_) {
                if (e.Key == currentMode) {
                    continue;
                }

                e.Value?.RenderOverlayForOtherTools(cameraInfo);
            }

            foreach (var st in subTools_) {
                if (st.Key != GetToolMode()) {
                    st.Value.RenderGenericInfoOverlay(cameraInfo);
                }
            }
        }

        /// <summary>
        /// Renders overlay when no subtool is active.
        /// May call base.RenderOverlay() without risk of infinte recursion.
        /// </summary>
        void DefaultRenderOverlay(RenderManager.CameraInfo cameraInfo) {
            if (!TMPELifecycle.PlayMode) {
                return; // world info view panels are not availble in edit mode
            }

            SubTools.PrioritySigns.MassEditOverlay.Show
                = ControlIsPressed || RoadSelectionPanels.Root.ShouldShowMassEditOverlay();

            NetManager.instance.NetAdjust.PathVisible =
                RoadSelectionPanels.Root.ShouldPathBeVisible();
            if (NetManager.instance.NetAdjust.PathVisible) {
                base.RenderOverlay(cameraInfo); // render path.
            }

            if (HoveredSegmentId == 0) {
                return;
            }

            NetAdjust netAdjust = NetManager.instance?.NetAdjust;
            if (netAdjust == null) {
                return;
            }

            // use the same color as in NetAdjust
            ref NetSegment segment = ref HoveredSegmentId.ToSegment();
            var color = ToolsModifierControl.toolController.m_validColorInfo;
            float alpha = 1f;
            NetTool.CheckOverlayAlpha(ref segment, ref alpha);
            color.a *= alpha;

            if (ReadjustPathMode) {
                if (Input.GetMouseButton(0)) {
                    color = GetToolColor(Input.GetMouseButton(0), false);
                }

                bool isRoundabout = RoundaboutMassEdit.Instance.TraverseLoop(
                    HoveredSegmentId,
                    out var segmentList);
                if (!isRoundabout) {
                    var segments = SegmentTraverser.Traverse(
                        HoveredSegmentId,
                        TraverseDirection.AnyDirection,
                        TraverseSide.Straight,
                        SegmentStopCriterion.None,
                        (_) => true);
                    segmentList = new List<ushort>(segmentList);
                }

                foreach (ushort segmentId in segmentList ?? Enumerable.Empty<ushort>()) {
                    ref NetSegment seg =
                        ref Singleton<NetManager>.instance.m_segments.m_buffer[segmentId];
                    NetTool.RenderOverlay(
                        cameraInfo,
                        ref seg,
                        color,
                        color);
                }
            } else {
                NetTool.RenderOverlay(cameraInfo, ref segment, color, color);
            }
        }

        /// <summary>
        /// Primarily handles click events on hovered nodes/segments
        /// </summary>
        protected override void OnToolUpdate() {
            base.OnToolUpdate();

            // Log._Debug($"OnToolUpdate");
            if (Input.GetKeyUp(KeyCode.PageDown)) {
                InfoManager.instance.SetCurrentMode(
                    InfoManager.InfoMode.Underground,
                    InfoManager.SubInfoMode.Default);
                UIView.library.Hide("TrafficInfoViewPanel");
            } else if (Input.GetKeyUp(KeyCode.PageUp)) {
                InfoManager.instance.SetCurrentMode(
                    InfoManager.InfoMode.None,
                    InfoManager.SubInfoMode.Default);
            }

            ToolCursor = null;
            bool elementsHovered =
                DetermineHoveredElements(activeLegacySubTool_ is not LaneConnectorTool);
            if (activeLegacySubTool_ != null && NetTool != null && elementsHovered) {
                ToolCursor = NetTool.m_upgradeCursor;

                if (activeLegacySubTool_ is LaneConnectorTool lcs
                    && HoveredNodeId != 0
                    && !Highlight.IsNodeVisible(HoveredNodeId)
                    && lcs.CanShowNopeCursor) {
                    ToolCursor = nopeCursor_;
                }
            }

            bool primaryMouseClicked = Input.GetMouseButtonDown(0);
            bool secondaryMouseClicked = Input.GetMouseButtonUp(1);

            // check if clicked
            if (!primaryMouseClicked && !secondaryMouseClicked) {
                return;
            }

            // check if mouse is inside panel
#if DEBUG
            bool mouseInsideAnyPanel = ModUI.Instance.GetMenu().containsMouse
                                       || ModUI.Instance.GetDebugMenu().containsMouse;
#else
            bool mouseInsideAnyPanel = ModUI.Instance.GetMenu().containsMouse;
#endif

            // !elementsHovered ||
            mouseInsideAnyPanel |=
                activeLegacySubTool_ != null && activeLegacySubTool_.IsCursorInPanel();

            if (!mouseInsideAnyPanel) {
                if (primaryMouseClicked) {
                    activeLegacySubTool_?.OnPrimaryClickOverlay();
                    activeSubTool_?.OnToolLeftClick();
                }

                if (secondaryMouseClicked) {
                    if (GetToolMode() == ToolMode.None) {
                        RoadSelectionPanels roadSelectionPanels =
                            UIView.GetAView().GetComponent<RoadSelectionPanels>();
                        if (roadSelectionPanels && roadSelectionPanels.RoadWorldInfoPanelExt &&
                            roadSelectionPanels.RoadWorldInfoPanelExt.isVisible) {
                            RoadSelectionPanels.RoadWorldInfoPanel.Hide();
                        } else {
                            ModUI.Instance.CloseMainMenu();
                        }
                    } else {
                        activeLegacySubTool_?.OnSecondaryClickOverlay();
                        activeSubTool_?.OnToolRightClick();
                    }
                }
            }
        }

        protected override void OnToolGUI(Event e) {
            OnToolGUIImpl(e);
            if (GetToolMode() == ToolMode.None) {
                DefaultOnToolGUI(e);
            }
        }

        /// <summary>
        /// Immediate mode GUI (IMGUI) handler called every frame for input and IMGUI rendering (persistent overlay).
        /// If any subtool is active it calls OnToolGUI for that subtool
        /// Must not call base.OnToolGUI(). Doing so may cause infinite recursion with Postfix of DefaultTool.OnToolGUI()
        /// </summary>
        /// <param name="e">Event to handle.</param>
        public void OnToolGUIImpl(Event e) {
            try {
                if (!Input.GetMouseButtonDown(0)) {
                    _mouseClickProcessed = false;
                }

                if (e.type == EventType.keyDown && e.keyCode == KeyCode.Escape) {
                    ModUI.Instance.CloseMainMenu();
                }

                if (Options.nodesOverlay) {
                    DebugToolGUI.DisplaySegments();
                    DebugToolGUI.DisplayNodes();
                }

                if (Options.vehicleOverlay) {
                    DebugToolGUI.DisplayVehicles();
                }

                if (Options.citizenOverlay) {
                    DebugToolGUI.DisplayCitizens();
                }

                if (Options.buildingOverlay) {
                    DebugToolGUI.DisplayBuildings();
                }

                //----------------------
                // Render legacy GUI overlay, and new style GUI mode overlays need to render too
                ToolMode toolMode = GetToolMode();

                foreach (KeyValuePair<ToolMode, LegacySubTool> en in legacySubTools_) {
                    en.Value.ShowGUIOverlay(
                        toolMode: en.Key,
                        viewOnly: en.Key != toolMode);
                }

                foreach (KeyValuePair<ToolMode, TrafficManagerSubTool> st in subTools_) {
                    if (st.Key == toolMode) {
                        st.Value.RenderActiveToolOverlay_GUI();
                    } else {
                        st.Value.RenderGenericInfoOverlay_GUI();
                    }
                }

                Color guiColor = GUI.color;
                guiColor.a = 1f;
                GUI.color = guiColor;

                if (activeLegacySubTool_ != null) {
                    activeLegacySubTool_.OnToolGUI(e);
                } else {
                    activeSubTool_?.UpdateEveryFrame();
                }
            }
            catch (Exception ex) {
                Log.Error("GUI Error: " + ex);
            }
        }

        void DefaultOnToolGUI(Event e) {
            if (!TMPELifecycle.PlayMode) {
                return; // world info view panels are not availble in edit mode
            }

            if (e.type == EventType.MouseDown && e.button == 0) {
                bool isRoad = HoveredSegmentId != 0 &&
                              HoveredSegmentId.ToSegment().Info.m_netAI is RoadBaseAI;
                if (!isRoad)
                    return;

                if (ReadjustPathMode) {
                    bool isRoundabout = RoundaboutMassEdit.Instance.TraverseLoop(
                        HoveredSegmentId,
                        out var segmentList);
                    if (!isRoundabout) {
                        var segments = SegmentTraverser.Traverse(
                            HoveredSegmentId,
                            TraverseDirection.AnyDirection,
                            TraverseSide.Straight,
                            SegmentStopCriterion.None,
                            (_) => true);
                        segmentList = new List<ushort>(segments);
                    }

                    RoadSelectionUtil.SetRoad(HoveredSegmentId, segmentList);
                }

                InstanceID instanceID = new InstanceID {
                    NetSegment = HoveredSegmentId,
                };

                SimulationManager.instance.m_ThreadingWrapper.QueueMainThread(
                    () => {
                        OpenWorldInfoPanel(
                            instanceID,
                            HitPos);
                    });
            }
        }

        /// <summary>
        /// Gets the coordinates of the given node.
        /// </summary>
        private static Vector3 GetNodePos(ushort nodeId) {
            NetNode[] nodeBuffer = Singleton<NetManager>.instance.m_nodes.m_buffer;
            Vector3 pos = nodeBuffer[nodeId].m_position;
            float terrainY = Singleton<TerrainManager>.instance.SampleDetailHeightSmooth(pos);
            if (terrainY > pos.y) {
                pos.y = terrainY;
            }

            return pos;
        }

        /// <returns>the average half width of all connected segments</returns>
        private static float CalculateNodeRadius(ushort nodeId) {
            float sumHalfWidth = 0;
            int count = 0;
            ref NetNode node = ref nodeId.ToNode();
            for (int i = 0; i < 8; ++i) {
                ushort segmentId = node.GetSegment(i);
                if (segmentId != 0) {
                    sumHalfWidth += segmentId.ToSegment().Info.m_halfWidth;
                    count++;
                }
            }

            return sumHalfWidth / count;
        }

        /// <summary>Shows a tutorial message. Must be called by a Unity thread.</summary>
        /// <param name="localeKey">Tutorial key.</param>
        public static void ShowAdvisor(string localeKey) {
            if (!GlobalConfig.Instance.Main.EnableTutorial || !TMPELifecycle.PlayMode) {
                return;
            }

            if (!Translation.Tutorials.HasString(
                    Translation.TUTORIAL_BODY_KEY_PREFIX + localeKey)) {
                Log.Warning($"ShowAdvisor: localeKey:{localeKey} does not exist");
                return;
            }

            Log._Debug($"TrafficManagerTool.ShowAdvisor({localeKey}) called.");
            TutorialAdvisorPanel tutorialPanel = ToolsModifierControl.advisorPanel;
            string key = Translation.TUTORIAL_KEY_PREFIX + localeKey;

            if (GlobalConfig.Instance.Main.DisplayedTutorialMessages.Contains(localeKey)) {
                tutorialPanel.Refresh(
                    localeID: key,
                    icon: "ToolbarIconZoomOutGlobe",
                    sprite: string.Empty);
            } else {
                tutorialPanel.Show(
                    localeID: key,
                    icon: "ToolbarIconZoomOutGlobe",
                    sprite: string.Empty,
                    timeout: 0f);
                GlobalConfig.Instance.Main.AddDisplayedTutorialMessage(localeKey);
                GlobalConfig.WriteConfig();
            }
        }

        private static Vector3 prevMousePosition;

        private bool DetermineHoveredElements(bool raycastSegment = true) {
            if (prevMousePosition == m_mousePosition) {
                // if mouse ray is not changing use cached results.
                // the assumption is that its practically impossible to change mouse ray
                // without changing m_mousePosition.
                return HoveredNodeId != 0 || HoveredSegmentId != 0;
            }

            HoveredSegmentId = 0;
            HoveredNodeId = 0;
            HitPos = m_mousePosition;

            bool mouseRayValid = !UIView.IsInsideUI() && Cursor.visible &&
                                 (activeLegacySubTool_ == null ||
                                  !activeLegacySubTool_.IsCursorInPanel());

            if (mouseRayValid) {
                // find currently hovered node
                var nodeInput = new RaycastInput(m_mouseRay, m_mouseRayLength) {
                    m_netService = {
                        // find road nodes
                        m_itemLayers = ItemClass.Layer.Default | ItemClass.Layer.MetroTunnels,
                        m_service = ItemClass.Service.Road,
                    },
                    m_ignoreTerrain = true,
                    m_ignoreNodeFlags = NetNode.Flags.None,
                };

                // nodeInput.m_netService2.m_itemLayers = ItemClass.Layer.Default
                //     | ItemClass.Layer.PublicTransport | ItemClass.Layer.MetroTunnels;
                // nodeInput.m_netService2.m_service = ItemClass.Service.PublicTransport;
                // nodeInput.m_netService2.m_subService = ItemClass.SubService.PublicTransportTrain;
                // nodeInput.m_ignoreNodeFlags = NetNode.Flags.Untouchable;

                if (RayCast(nodeInput, out RaycastOutput nodeOutput)) {
                    HoveredNodeId = nodeOutput.m_netNode;
                } else {
                    // find train nodes
                    nodeInput.m_netService.m_itemLayers =
                        ItemClass.Layer.Default | ItemClass.Layer.MetroTunnels;
                    nodeInput.m_netService.m_service = ItemClass.Service.PublicTransport;
                    nodeInput.m_netService.m_subService = ItemClass.SubService.PublicTransportTrain;
                    nodeInput.m_ignoreNodeFlags = NetNode.Flags.None;
                    // nodeInput.m_ignoreNodeFlags = NetNode.Flags.Untouchable;

                    if (RayCast(nodeInput, out nodeOutput)) {
                        HoveredNodeId = nodeOutput.m_netNode;
                    } else {
                        // find metro nodes
                        nodeInput.m_netService.m_itemLayers =
                            ItemClass.Layer.Default | ItemClass.Layer.MetroTunnels;
                        nodeInput.m_netService.m_service = ItemClass.Service.PublicTransport;
                        nodeInput.m_netService.m_subService =
                            ItemClass.SubService.PublicTransportMetro;
                        nodeInput.m_ignoreNodeFlags = NetNode.Flags.None;
                        // nodeInput.m_ignoreNodeFlags = NetNode.Flags.Untouchable;

                        if (RayCast(nodeInput, out nodeOutput)) {
                            HoveredNodeId = nodeOutput.m_netNode;
                        }
                    }
                }

                ushort segmentId = 0;
                // find currently hovered segment
                var segmentInput = new RaycastInput(m_mouseRay, m_mouseRayLength) {
                    m_netService = {
                        // find road segments
                        m_itemLayers = ItemClass.Layer.Default | ItemClass.Layer.MetroTunnels,
                        m_service = ItemClass.Service.Road,
                    },
                    m_ignoreTerrain = true,
                    m_ignoreSegmentFlags = NetSegment.Flags.None,
                };
                // segmentInput.m_ignoreSegmentFlags = NetSegment.Flags.Untouchable;

                if (RayCast(segmentInput, out RaycastOutput segmentOutput)) {
                    segmentId = segmentOutput.m_netSegment;
                } else {
                    // find train segments
                    segmentInput.m_netService.m_itemLayers =
                        ItemClass.Layer.Default | ItemClass.Layer.MetroTunnels;
                    segmentInput.m_netService.m_service = ItemClass.Service.PublicTransport;
                    segmentInput.m_netService.m_subService =
                        ItemClass.SubService.PublicTransportTrain;
                    segmentInput.m_ignoreTerrain = true;
                    segmentInput.m_ignoreSegmentFlags = NetSegment.Flags.None;
                    // segmentInput.m_ignoreSegmentFlags = NetSegment.Flags.Untouchable;

                    if (RayCast(segmentInput, out segmentOutput)) {
                        segmentId = segmentOutput.m_netSegment;
                    } else {
                        // find metro segments
                        segmentInput.m_netService.m_itemLayers =
                            ItemClass.Layer.Default | ItemClass.Layer.MetroTunnels;
                        segmentInput.m_netService.m_service = ItemClass.Service.PublicTransport;
                        segmentInput.m_netService.m_subService =
                            ItemClass.SubService.PublicTransportMetro;
                        segmentInput.m_ignoreSegmentFlags = NetSegment.Flags.None;
                        // segmentInput.m_ignoreSegmentFlags = NetSegment.Flags.Untouchable;

                        if (RayCast(segmentInput, out segmentOutput)) {
                            segmentId = segmentOutput.m_netSegment;
                        }
                    }
                }

                if (segmentId != 0 && raycastSegment) {
                    HitPos = segmentOutput.m_hitPos;
                    HoveredSegmentId = segmentId;
                }

                if (HoveredNodeId <= 0 && segmentId > 0) {
                    // alternative way to get a node hit: check distance to start and end nodes
                    // of the segment
                    ushort startNodeId = Singleton<NetManager>
                                         .instance.m_segments.m_buffer[segmentId].m_startNode;
                    ushort endNodeId = Singleton<NetManager>
                                       .instance.m_segments.m_buffer[segmentId].m_endNode;

                    NetNode[] nodesBuffer = Singleton<NetManager>.instance.m_nodes.m_buffer;
                    float startDist = (segmentOutput.m_hitPos - nodesBuffer[startNodeId]
                                           .m_position).magnitude;
                    float endDist = (segmentOutput.m_hitPos - nodesBuffer[endNodeId]
                                         .m_position).magnitude;
                    if (startDist < endDist && startDist < NODE_DETECTION_RADIUS) {
                        HoveredNodeId = startNodeId;
                    } else if (endDist < startDist && endDist < NODE_DETECTION_RADIUS) {
                        HoveredNodeId = endNodeId;
                    }
                }

                if (HoveredNodeId != 0 && HoveredSegmentId != 0 && raycastSegment) {
                    HoveredSegmentId = GetHoveredSegmentFromNode(segmentOutput.m_hitPos);
                }
            }

            return HoveredNodeId != 0 || HoveredSegmentId != 0;
        }

        /// <summary>
        /// returns the node(HoveredNodeId) segment that is closest to the input position.
        /// </summary>
        internal ushort GetHoveredSegmentFromNode(Vector3 hitPos) {
            ushort minSegId = 0;
            float minDistance = float.MaxValue;
            ref NetNode node = ref HoveredNodeId.ToNode();
            for (int i = 0; i < 8; ++i) {
                ushort segmentId = node.GetSegment(i);
                if (segmentId != 0) {
                    Vector3 pos = segmentId.ToSegment().GetClosestPosition(hitPos);
                    float distance = (hitPos - pos).sqrMagnitude;
                    if (distance < minDistance) {
                        minDistance = distance;
                        minSegId = segmentId;
                    }
                }
            }

            return minSegId;
        }

        private static float prev_H = 0f;
        private static float prev_H_Fixed;

        /// <summary>
        /// Calculates accurate vertical element of raycast hit position.
        /// </summary>
        internal static float GetAccurateHitHeight() {
            // cache result.
            if (FloatUtil.NearlyEqual(HitPos.y, prev_H)) {
                return prev_H_Fixed;
            }

            prev_H = HitPos.y;

            if (Shortcuts.GetSeg(HoveredSegmentId).GetClosestLanePosition(
                point: HitPos,
                laneTypes: NetInfo.LaneType.All,
                vehicleTypes: VehicleInfo.VehicleType.All,
                position: out Vector3 pos,
                laneID: out uint laneId,
                laneIndex: out int laneIndex,
                laneOffset: out float laneOffset)) {
                return prev_H_Fixed = pos.y;
            }

            return prev_H_Fixed = HitPos.y + 0.5f;
        }

        new internal Color GetToolColor(bool warning, bool error) {
            return base.GetToolColor(warning, error);
        }

        /// <summary>Creates a texture width x height, filled with color.</summary>
        /// <param name="width">Size x.</param>
        /// <param name="height">Size y.</param>
        /// <param name="col">Fill color.</param>
        /// <returns>New solid color Texture2D.</returns>
        public static Texture2D CreateSolidColorTexture(int width, int height, Color col) {
            var pix = new Color[width * height];

            for (var i = 0; i < pix.Length; i++) {
                pix[i] = col;
            }

            var result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();

            return result;
        }

        [Obsolete(
            "Avoid using globals, pass mouse coords to overlays. Use rect.Contains(mousePos) instead.")]
        internal static bool IsMouseOver(Rect boundingBox) {
            return boundingBox.Contains(Event.current.mousePosition);
        }

        /// <summary>
        /// this method should be used in OnToolGUI() instead of Input.GetMouseButtonDown(0).
        /// This is because Input.GetMouseButtonDown(0) is consumed by OnToolUpdate()
        /// to call OnPrimaryClickOverlay().
        /// You should call this method from OnPrimaryClickOverlay() once click is handled. consume the click.
        /// TODO [issue #740] improve this.
        /// </summary>
        [Obsolete(
            "Avoid using LegacyTool, Immediate Mode GUI and OnToolGUI, use new U GUI, new TrafficManagerSubTool base class and mouse events instead.")]
        internal bool CheckClicked() {
            if (Input.GetMouseButtonDown(0) && !_mouseClickProcessed) {
                _mouseClickProcessed = true;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Displays a warning prompt in center of the screen.
        /// </summary>
        ///
        /// <param name="message">The localized body text of the prompt.</param>
        public void WarningPrompt(string message) {
            if (string.IsNullOrEmpty(message)) {
                return;
            }

            Prompt.Warning("Warning", message);
        }

        /// <summary>
        /// Called when the onscreen hint update is due. This will request the update from the
        /// active Traffic Manager Tool, or show the default hint.
        /// </summary>
        public void RequestOnscreenDisplayUpdate() {
            if (!GlobalConfig.Instance.Main.KeybindsPanelVisible) {
                OnscreenDisplay.Clear();
                return;
            }

            var activeLegacyOsd = activeLegacySubTool_ as IOnscreenDisplayProvider;
            activeLegacyOsd?.UpdateOnscreenDisplayPanel();

            var activeOsd = activeSubTool_ as IOnscreenDisplayProvider;
            activeOsd?.UpdateOnscreenDisplayPanel();

            if (activeOsd == null && activeLegacyOsd == null) {
                // No tool hint support was available means we have to show the default
                OnscreenDisplay.DisplayIdle();
            }
        }
    }
}