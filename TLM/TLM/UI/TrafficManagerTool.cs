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
        private bool ReadjustPathMode => false; // ShiftIsPressed;

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

        internal Vector3 MousePosition => this.m_mousePosition; // expose protected member.

        private static bool mouseClickProcessed;

        public const float DEBUG_CLOSE_LOD = 300f;

        /// <summary>Square of the distance, where overlays are not rendered.</summary>
        public const float MAX_OVERLAY_DISTANCE_SQR = 450f * 450f;

        private IDictionary<ToolMode, LegacySubTool> legacySubTools_;

        private IDictionary<ToolMode, TrafficManagerSubTool> subTools_;

        public static ushort SelectedNodeId { get; internal set; }

        public static ushort SelectedSegmentId { get; internal set; }

        public static TransportDemandViewMode CurrentTransportDemandViewMode { get; internal set; }
            = TransportDemandViewMode.Outgoing;

        internal static readonly ExtVehicleType[] InfoSignsToDisplay = {
            ExtVehicleType.PassengerCar, ExtVehicleType.Bicycle, ExtVehicleType.Bus,
            ExtVehicleType.Taxi, ExtVehicleType.Tram, ExtVehicleType.CargoTruck,
            ExtVehicleType.Service, ExtVehicleType.RailVehicle,
        };

        [Obsolete("Convert your legacy tools to new TrafficManagerSubTool style")]
        private LegacySubTool activeLegacySubTool_;

        private TrafficManagerSubTool activeSubTool_;

        private static IDisposable confDisposable;

        static TrafficManagerTool() { }

        /// <summary>Gets a value indicating whether the current tool is traffic manager tool.</summary>
        public static bool IsCurrentTool =>
            ToolsModifierControl.toolController != null
            && ToolsModifierControl.toolController.CurrentTool is TrafficManagerTool;

        protected override void OnDestroy() {
            Log.Info("TrafficManagerTool.OnDestroy() called");
            if (this.nopeCursor_) {
                Destroy(this.nopeCursor_);
                this.nopeCursor_ = null;
            }

            base.OnDestroy();
        }

        internal ToolController GetToolController() {
            return this.m_toolController;
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
                if (this.netTool_ == null) {
                    Log._Debug("NetTool field value is null. Searching for instance...");
                    this.netTool_ = ToolsModifierControl.toolController.Tools.OfType<NetTool>()
                                                   .FirstOrDefault();
                }

                return this.netTool_;
            }
        }

        private static float TransparencyToAlpha(byte transparency) {
            return Mathf.Clamp(100 - transparency, 0f, 100f) / 100f;
        }

        internal void Initialize() {
            Log.Info("TrafficManagerTool: Initialization running now.");
            this.Guide = new GuideHandler();

            LegacySubTool timedLightsTool =
                new SubTools.TimedTrafficLights.TimedTrafficLightsTool(this);

            this.subTools_ = new TinyDictionary<ToolMode, TrafficManagerSubTool> {
                [ToolMode.LaneArrows] = new SubTools.LaneArrows.LaneArrowTool(this),
                [ToolMode.SpeedLimits] = new SpeedLimitsTool(this),
            };

            this.legacySubTools_ = new TinyDictionary<ToolMode, LegacySubTool> {
                [ToolMode.ToggleTrafficLight] = new ToggleTrafficLightsTool(this),
                [ToolMode.AddPrioritySigns] = new SubTools.PrioritySigns.PrioritySignsTool(this),
                [ToolMode.ManualSwitch] = new ManualTrafficLightsTool(this),
                [ToolMode.TimedTrafficLights] = timedLightsTool,
                [ToolMode.VehicleRestrictions] = new VehicleRestrictionsTool(this),
                [ToolMode.LaneConnector] = new LaneConnectorTool(this),
                [ToolMode.JunctionRestrictions] = new JunctionRestrictionsTool(this),
                [ToolMode.ParkingRestrictions] = new ParkingRestrictionsTool(this),
            };

            this.InitializeSubTools();
            this.SetToolMode(ToolMode.None);

            confDisposable?.Dispose();
            confDisposable = GlobalConfig.Instance.Subscribe(this);

            Log.Info("TrafficManagerTool: Initialization completed.");
        }

        public void OnUpdate(GlobalConfig config) {
            this.InitializeSubTools();
        }

        internal void InitializeSubTools() {
            foreach (KeyValuePair<ToolMode, LegacySubTool> e in legacySubTools_) {
                e.Value.Initialize();
            }
        }

        protected override void Awake() {
            Log._Debug($"TrafficManagerTool: Awake {this.GetHashCode()}");
            this.nopeCursor_ = ScriptableObject.CreateInstance<CursorInfo>();
            this.nopeCursor_.m_texture = UIView.GetAView().defaultAtlas["Niet"]?.texture;
            this.nopeCursor_.m_hotspot = new Vector2(45, 45);
            base.Awake();
        }

        /// <summary>Only used from CustomRoadBaseAI.</summary>
        public LegacySubTool GetSubTool(ToolMode mode) {
            return this.legacySubTools_.TryGetValue(mode, out LegacySubTool ret)
                       ? ret : null;
        }

        public ToolMode GetToolMode() {
            return this.toolMode_;
        }

        /// <summary>Deactivate current active tool. Set new active tool.</summary>
        /// <param name="newToolMode">New mode.</param>
        public void SetToolMode(ToolMode newToolMode) {
            ToolMode oldToolMode = this.toolMode_;

            if (this.toolMode_ != ToolMode.None && TMPELifecycle.PlayMode) {
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

            bool toolModeChanged = newToolMode != this.toolMode_;

            if (!toolModeChanged) {
                Log._Debug($"SetToolMode: not changed old={oldToolMode} new={newToolMode}");
                return;
            }

            this.SetToolMode_DeactivateTool();

            // Try figure out whether legacy subtool or a new subtool is selected
            if (!this.legacySubTools_.TryGetValue(newToolMode, value: out this.activeLegacySubTool_)
                && !this.subTools_.TryGetValue(newToolMode, out activeSubTool_)) {
                this.toolMode_ = ToolMode.None;

                Log._Debug($"SetToolMode: reset because toolmode not found {newToolMode}");
                OnscreenDisplay.DisplayIdle();
                ModUI.Instance.MainMenu.UpdateButtons();
                return;
            }

            this.SetToolMode_Activate(newToolMode);
            Log._Debug($"SetToolMode: changed old={oldToolMode} new={newToolMode}");
        }

        /// <summary>Resets the tool and calls deactivate on it.</summary>
        private void SetToolMode_DeactivateTool() {
            // Clear OSD panel with keybinds
            OnscreenDisplay.Clear();

            if (this.activeLegacySubTool_ != null || this.activeSubTool_ != null) {
                this.activeLegacySubTool_?.Cleanup();
                this.activeLegacySubTool_ = null;

                this.activeSubTool_?.DeactivateTool();
                this.activeSubTool_ = null;
                this.toolMode_ = ToolMode.None;
            }
        }

        /// <summary>
        /// Sets new active tool. Resets selected segment and node. Calls activate on tools.
        /// Also shows advisor.
        /// </summary>
        /// <param name="newToolMode">New mode.</param>
        private void SetToolMode_Activate(ToolMode newToolMode) {
            this.toolMode_ = newToolMode;
            SelectedNodeId = 0;
            SelectedSegmentId = 0;

            this.activeLegacySubTool_?.OnActivate();
            this.activeSubTool_?.ActivateTool();

            if (this.activeLegacySubTool_ != null) {
                ShowAdvisor(this.activeLegacySubTool_.GetTutorialKey());
                this.Guide.DeactivateAll();
            }
        }

        // Overridden to disable base class behavior
        protected override void OnEnable() {
            // If TMPE was enabled by switching back from another tool (eg: buldozer, free camera), show main menue panel.
            if (ModUI.Instance != null && !ModUI.Instance.IsVisible()) {
                ModUI.Instance.ShowMainMenu();
            }

            if (this.legacySubTools_ != null) {
                Log._Debug("TrafficManagerTool.OnEnable(): Performing cleanup");
                foreach (KeyValuePair<ToolMode, LegacySubTool> e in this.legacySubTools_) {
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
                this.m_toolController.RenderCollidingNotifications(
                    cameraInfo: cameraInfo,
                    ignoreSegment: 0,
                    ignoreBuilding: 0);
            }
        }

        public override void RenderOverlay(RenderManager.CameraInfo cameraInfo) {
            this.RenderOverlayImpl(cameraInfo);

            if (this.GetToolMode() == ToolMode.None) {
                this.DefaultRenderOverlay(cameraInfo);
            }
        }

        /// <summary>
        /// renders presistent overlay.
        /// if any subtool is active it renders overlay for that subtool (e.g. node selection, segment selection, etc.)
        /// Must not call base.RenderOverlay() . Doing so may cause infinite recursion with Postfix of base.RenderOverlay()
        /// </summary>
        public void RenderOverlayImpl(RenderManager.CameraInfo cameraInfo) {
            if (!(this.isActiveAndEnabled || SubTools.PrioritySigns.MassEditOverlay.IsActive)) {
                return;
            }

            this.activeLegacySubTool_?.RenderOverlay(cameraInfo);
            this.activeSubTool_?.RenderActiveToolOverlay(cameraInfo);

            ToolMode currentMode = this.GetToolMode();

            // For all _other_ legacy subtools let them render something too
            foreach (KeyValuePair<ToolMode, LegacySubTool> e in this.legacySubTools_) {
                if (e.Key == currentMode) {
                    continue;
                }

                e.Value?.RenderOverlayForOtherTools(cameraInfo);
            }

            foreach (var st in this.subTools_) {
                if (st.Key != this.GetToolMode()) {
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

            if (this.ReadjustPathMode) {
                if (Input.GetMouseButton(0)) {
                    color = this.GetToolColor(Input.GetMouseButton(0), false);
                }

                bool isRoundabout = RoundaboutMassEdit.Instance.TraverseLoop(
                    HoveredSegmentId,
                    out var segmentList);
                if (!isRoundabout) {
                    SegmentTraverser.Traverse(
                        initialSegmentId: HoveredSegmentId,
                        direction: TraverseDirection.AnyDirection,
                        side: TraverseSide.Straight,
                        stopCrit: SegmentStopCriterion.None,
                        visitorFun: (_) => true);
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

        /// <summary>Primarily handles click events on hovered nodes/segments.</summary>
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

            this.ToolCursor = null;

            bool elementsHovered =
                this.DetermineHoveredElements(activeLegacySubTool_ is not LaneConnectorTool);

            if (this.activeLegacySubTool_ != null && this.NetTool != null && elementsHovered) {
                this.ToolCursor = this.NetTool.m_upgradeCursor;

                if (this.activeLegacySubTool_ is LaneConnectorTool lcs
                    && HoveredNodeId != 0
                    && !Highlight.IsNodeVisible(HoveredNodeId)
                    && lcs.CanShowNopeCursor) {
                    this.ToolCursor = this.nopeCursor_;
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
                this.activeLegacySubTool_ != null && this.activeLegacySubTool_.IsCursorInPanel();

            if (!mouseInsideAnyPanel) {
                if (primaryMouseClicked) {
                    this.activeLegacySubTool_?.OnPrimaryClickOverlay();
                    this.activeSubTool_?.OnToolLeftClick();
                }

                if (secondaryMouseClicked) {
                    if (this.GetToolMode() == ToolMode.None) {
                        RoadSelectionPanels roadSelectionPanels =
                            UIView.GetAView().GetComponent<RoadSelectionPanels>();

                        if (roadSelectionPanels && roadSelectionPanels.RoadWorldInfoPanelExt &&
                            roadSelectionPanels.RoadWorldInfoPanelExt.isVisible) {
                            RoadSelectionPanels.RoadWorldInfoPanel.Hide();
                        } else {
                            ModUI.Instance.CloseMainMenu();
                        }
                    } else {
                        this.activeLegacySubTool_?.OnSecondaryClickOverlay();
                        this.activeSubTool_?.OnToolRightClick();
                    }
                }
            }
        }

        protected override void OnToolGUI(Event e) {
            this.OnToolGUIImpl(e);

            if (this.GetToolMode() == ToolMode.None) {
                this.DefaultOnToolGUI(e);
            }
        }

        /// <summary>
        /// Immediate mode GUI (IMGUI) handler called every frame for input and IMGUI rendering (persistent overlay).
        /// If any subtool is active it calls OnToolGUI for that subtool
        /// Must not call base.OnToolGUI(). Doing so may cause infinite recursion with Postfix of DefaultTool.OnToolGUI().
        /// </summary>
        /// <param name="e">Event to handle.</param>
        public void OnToolGUIImpl(Event e) {
            try {
                if (!Input.GetMouseButtonDown(0)) {
                    mouseClickProcessed = false;
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
                ToolMode toolMode = this.GetToolMode();

                foreach (KeyValuePair<ToolMode, LegacySubTool> en in this.legacySubTools_) {
                    en.Value.ShowGUIOverlay(
                        toolMode: en.Key,
                        viewOnly: en.Key != toolMode);
                }

                foreach (KeyValuePair<ToolMode, TrafficManagerSubTool> st in this.subTools_) {
                    if (st.Key == toolMode) {
                        st.Value.RenderActiveToolOverlay_GUI();
                    } else {
                        st.Value.RenderGenericInfoOverlay_GUI();
                    }
                }

                Color guiColor = GUI.color;
                guiColor.a = 1f;
                GUI.color = guiColor;

                if (this.activeLegacySubTool_ != null) {
                    this.activeLegacySubTool_.OnToolGUI(e);
                } else {
                    this.activeSubTool_?.UpdateEveryFrame();
                }
            }
            catch (Exception ex) {
                Log.Error("GUI Error: " + ex);
            }
        }

        private void DefaultOnToolGUI(Event e) {
            if (!TMPELifecycle.PlayMode) {
                return; // world info view panels are not availble in edit mode
            }

            if (e.type == EventType.MouseDown && e.button == 0) {
                bool isRoad = HoveredSegmentId != 0 &&
                              HoveredSegmentId.ToSegment().Info.m_netAI is RoadBaseAI;
                if (!isRoad) {
                    return;
                }

                if (this.ReadjustPathMode) {
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

        // /// <summary>
        // /// Gets the coordinates of the given node.
        // /// </summary>
        // private static Vector3 GetNodePos(ushort nodeId) {
        //     NetNode[] nodeBuffer = Singleton<NetManager>.instance.m_nodes.m_buffer;
        //     Vector3 pos = nodeBuffer[nodeId].m_position;
        //     float terrainY = Singleton<TerrainManager>.instance.SampleDetailHeightSmooth(pos);
        //     if (terrainY > pos.y) {
        //         pos.y = terrainY;
        //     }
        //
        //     return pos;
        // }

        // /// <returns>the average half width of all connected segments</returns>
        // private static float CalculateNodeRadius(ushort nodeId) {
        //     float sumHalfWidth = 0;
        //     int count = 0;
        //     ref NetNode node = ref nodeId.ToNode();
        //     for (int i = 0; i < 8; ++i) {
        //         ushort segmentId = node.GetSegment(i);
        //         if (segmentId != 0) {
        //             sumHalfWidth += segmentId.ToSegment().Info.m_halfWidth;
        //             count++;
        //         }
        //     }
        //
        //     return sumHalfWidth / count;
        // }

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
            if (prevMousePosition == this.m_mousePosition) {
                // if mouse ray is not changing use cached results.
                // the assumption is that its practically impossible to change mouse ray
                // without changing m_mousePosition.
                return HoveredNodeId != 0 || HoveredSegmentId != 0;
            }

            HoveredSegmentId = 0;
            HoveredNodeId = 0;
            HitPos = this.m_mousePosition;

            bool mouseRayValid = !UIView.IsInsideUI() && Cursor.visible &&
                                 (this.activeLegacySubTool_ == null ||
                                  !this.activeLegacySubTool_.IsCursorInPanel());

            if (mouseRayValid) {
                // find currently hovered node
                var nodeInput = new RaycastInput(this.m_mouseRay, this.m_mouseRayLength) {
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
                var segmentInput = new RaycastInput(this.m_mouseRay, this.m_mouseRayLength) {
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
                    HoveredSegmentId = this.GetHoveredSegmentFromNode(segmentOutput.m_hitPos);
                }
            }

            return HoveredNodeId != 0 || HoveredSegmentId != 0;
        }

        /// <summary>
        /// returns the node(HoveredNodeId) segment that is closest to the input position.
        /// </summary>
        private ushort GetHoveredSegmentFromNode(Vector3 hitPos) {
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

        private static float prevH;
        private static float prevHFixed;

        /// <summary>
        /// Calculates accurate vertical element of raycast hit position.
        /// </summary>
        internal static float GetAccurateHitHeight() {
            // cache result.
            if (FloatUtil.NearlyEqual(HitPos.y, prevH)) {
                return prevHFixed;
            }

            prevH = HitPos.y;

            if (Shortcuts.GetSeg(HoveredSegmentId).GetClosestLanePosition(
                point: HitPos,
                laneTypes: NetInfo.LaneType.All,
                vehicleTypes: VehicleInfo.VehicleType.All,
                position: out Vector3 pos,
                laneID: out uint _,
                laneIndex: out int _,
                laneOffset: out float _)) {
                return prevHFixed = pos.y;
            }

            return prevHFixed = HitPos.y + 0.5f;
        }

        internal new Color GetToolColor(bool warning, bool error) {
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
            if (Input.GetMouseButtonDown(0) && !mouseClickProcessed) {
                mouseClickProcessed = true;
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

            var activeLegacyOsd = this.activeLegacySubTool_ as IOnscreenDisplayProvider;
            activeLegacyOsd?.UpdateOnscreenDisplayPanel();

            var activeOsd = this.activeSubTool_ as IOnscreenDisplayProvider;
            activeOsd?.UpdateOnscreenDisplayPanel();

            if (activeOsd == null && activeLegacyOsd == null) {
                // No tool hint support was available means we have to show the default
                OnscreenDisplay.DisplayIdle();
            }
        }
    }
}