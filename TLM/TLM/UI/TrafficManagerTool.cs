namespace TrafficManager.UI {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using TrafficManager.API.Manager;
    using TrafficManager.API.Traffic.Data;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.API.Util;
    using ColossalFramework;
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using JetBrains.Annotations;
    using TrafficManager.Manager.Impl;
    using TrafficManager.State;
    using TrafficManager.State.ConfigData;
    using TrafficManager.UI.MainMenu;
    using TrafficManager.UI.SubTools;
    using TrafficManager.UI.SubTools.SpeedLimits;
    using TrafficManager.Util;
    using UnityEngine;
    using TrafficManager.UI.Helpers;
    using TrafficManager.UI.MainMenu.OSD;
    using TrafficManager.UI.SubTools.LaneArrows;
    using TrafficManager.UI.SubTools.PrioritySigns;
    using TrafficManager.UI.SubTools.TTL;
    using TrafficManager.Lifecycle;
    using UnifiedUI.Helpers;

    using static TrafficManager.Util.Shortcuts;
    using static TrafficManager.Util.SegmentTraverser;
    using TrafficManager.UI.Textures;
    using TrafficManager.State.Keybinds;
    using TrafficManager.Util.Extensions;
    using static InfoManager;
    using TrafficManager.UI.SubTools.RoutingDetector;

    [UsedImplicitly]
    public class TrafficManagerTool
        : DefaultTool,
          IObserver<GlobalConfig>
    {
        // TODO [issue #710] Road adjust mechanism seem to have changed in Sunset Harbor DLC.
        // activate when we know the mechanism.
        private bool ReadjustPathMode => false; //ShiftIsPressed;

        private bool NodeSelectionMode => AltIsPressed;

        // /// <summary>Set this to true to once call <see cref="RequestOnscreenDisplayUpdate"/>.</summary>
        // public bool InvalidateOnscreenDisplayFlag { get; set; }

        public GuideHandler Guide;

        public UIComponent UUIButton;

        private ToolMode toolMode_;

        private NetTool netTool_;

        public const float DEBUG_CLOSE_LOD = 300f;

        /// <summary>Square of the distance, where overlays are not rendered.</summary>
        public const float MAX_OVERLAY_DISTANCE_SQR = 600f * 600f;

        internal const float MAX_ZOOM = 0.05f;

        /// <summary>Maximum error of HitPos field.</summary>
        internal const float MAX_HIT_ERROR = 2.5f;

        /// <summary>Maximum detection radius of segment raycast hit position.</summary>
        internal const float NODE_DETECTION_RADIUS = 75f;
        internal const float PRECISE_NODE_DETECTION_RADIUS = 15f;

        /// <summary>Convert 0..100 opacity value to 0..1f alpha value.</summary>
        internal const float TO_ALPHA = 0.01f;

        /// <summary>Minimum opacity value. Also affects sliders in mod options.</summary>
        internal const byte MINIMUM_OPACITY = 10;

        /// <summary>Maximum opacity value. Also affects sliders in mod options.</summary>
        internal const byte MAXIMUM_OPACITY = 100;

        internal static ushort HoveredNodeId;

        internal static ushort HoveredSegmentId;

        /// <summary>The hit position of the mouse raycast.</summary>
        internal static Vector3 HitPos;

        internal Vector3 MousePosition => m_mousePosition; //expose protected member.

        private static bool _mouseClickProcessed;

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

        /// <summary>
        /// Returns <c>true</c> if game is in an underground mode which allows
        /// tunnels to be customised.
        /// </summary>
        public static bool IsUndergroundMode => IsValidUndergroundMode(InfoManager.instance.CurrentMode);

        internal static float OverlayAlpha
            => TO_ALPHA * Mathf.Clamp(
                GlobalConfig.Instance.Main.OverlayOpacity,
                MINIMUM_OPACITY,
                MAXIMUM_OPACITY);

        static TrafficManagerTool() { }

        /// <summary>
        /// Determines if the current tool is traffic manager tool.
        /// </summary>
        public static bool IsCurrentTool =>
            ToolsModifierControl.toolController?.CurrentTool != null
            && ToolsModifierControl.toolController.CurrentTool is TrafficManagerTool;

        /// <summary>
        /// Determine if TM:PE can be used in specified info view <paramref name="mode"/>.
        /// </summary>
        /// <param name="mode">The <see cref="InfoManager.InfoMode"/> to test.</param>
        /// <returns>Returns <c>true</c> if <paramref name="mode"/> is permitted, otherwise <c>false</c>.</returns>
        public static bool IsValidUndergroundMode(InfoMode mode) => mode is
            InfoManager.InfoMode.Underground or
            InfoManager.InfoMode.Traffic or
            InfoManager.InfoMode.TrafficRoutes;

        protected override void OnDestroy() {
            Log.Info("TrafficManagerTool.OnDestroy() called");

            InfoManager.instance.EventInfoModeChanged -= OnInfoModeChanged;

            RemoveUUIButton();

            foreach (var eachLegacyTool in legacySubTools_) {
                eachLegacyTool.Value.OnDestroy();
            }
            foreach (var eachTool in subTools_) {
                eachTool.Value.OnDestroy();
            }
            legacySubTools_.Clear();
            legacySubTools_ = null;
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
            return new Rect(
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

        [Obsolete("Use U.UIScaler and U size and position logic")]
        internal float GetBaseZoom() {
            return Screen.height / 1200f;
        }

        internal static float GetWindowAlpha()
            => TO_ALPHA * Mathf.Clamp(
                GlobalConfig.Instance.Main.GuiOpacity,
                MINIMUM_OPACITY,
                MAXIMUM_OPACITY);

        /// <summary>
        /// Get alpha value for an overlay icon, taking in to account hovered state.
        /// </summary>
        /// <param name="hovered">Set <c>true</c> if mouse is over handle.</param>
        /// <returns>Returns alpha value in range 0.1..1f.</returns>
        internal static float GetHandleAlpha(bool hovered) => hovered
            ? TO_ALPHA * MAXIMUM_OPACITY
            : OverlayAlpha;

        /// <summary>Gives convenient access to NetTool from the original game.</summary>
        private NetTool NetTool {
            get {
                if (netTool_ == null) {
                    Log._Debug("NetTool field value is null. Searching for instance...");
                    netTool_ = ToolsModifierControl.toolController.Tools.OfType<NetTool>().FirstOrDefault();
                }

                return netTool_;
            }
        }

        internal void Initialize() {
            Log.Info("TrafficManagerTool: Initialization running now.");
            Guide = new GuideHandler();

            LegacySubTool timedLightsTool = new TimedTrafficLightsTool(this);

            subTools_ = new Dictionary<ToolMode, TrafficManagerSubTool> {
                [ToolMode.LaneArrows] = new LaneArrowTool(this),
                [ToolMode.SpeedLimits] = new SpeedLimitsTool(this),
                [ToolMode.RoutingDetector] = new RoutingDetectorTool(this),
            };
            legacySubTools_ = new Dictionary<ToolMode, LegacySubTool> {
                [ToolMode.ToggleTrafficLight] = new ToggleTrafficLightsTool(this),
                [ToolMode.AddPrioritySigns] = new PrioritySignsTool(this),
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

            AddUUIButton();

            InfoManager.instance.EventInfoModeChanged += OnInfoModeChanged;

            Log.Info("TrafficManagerTool: Initialization completed.");
        }

        /// <summary>When (most) info views are opened, close TM:PE toolbar.</summary>
        /// <param name="mode">The <see cref="InfoMode"/> which just became active.</param>
        /// <param name="_">Not used.</param>
        private void OnInfoModeChanged(InfoManager.InfoMode mode, InfoManager.SubInfoMode _) {
            Log._Debug($"OnInfoModeChanged: Info manager mode changed to: {mode}");

            if (mode == InfoManager.InfoMode.None || IsValidUndergroundMode(mode)) {
                return; // TM:PE toolbar can persist in these modes
            }

            ModUI.Instance.CloseMainMenu();
        }

        public void OnUpdate(GlobalConfig config) {
            InitializeSubTools();
        }

        internal void InitializeSubTools() {
            Log.Info("TrafficManagerTool.InitializeSubTools()");
            foreach (KeyValuePair<ToolMode, LegacySubTool> e in legacySubTools_) {
                e.Value.Initialize();
            }
        }

        protected override void Awake() {
            try {
                Log._Debug($"TrafficManagerTool: Awake {GetHashCode()}");
                base.Awake();
                Initialize();
            } catch(Exception ex) {
                ex.LogException();
            }
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

            if(toolMode_ != ToolMode.None && TMPELifecycle.PlayMode) {
                // Make it impossible for user to undo changes performed by Road selection panels
                // after changing traffic rule vis other tools.
                // TODO: This code will not be necessary when we implement intent.
                if(RoadSelectionPanels.RoadWorldInfoPanel != null)
                    SimulationManager.instance.m_ThreadingWrapper.QueueMainThread(RoadSelectionPanels.RoadWorldInfoPanel.Hide);
                if(RoadSelectionPanels.Root != null)
                    RoadSelectionPanels.Root.Function = RoadSelectionPanels.FunctionModes.None;
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
                ScreenDisplay();
                ModUI.Instance?.MainMenu?.UpdateButtons();
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

                activeSubTool_?.OnDeactivateTool();
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
            activeSubTool_?.OnActivateTool();

            if (activeLegacySubTool_ != null) {
                ShowAdvisor(activeLegacySubTool_.GetTutorialKey());
                Guide.DeactivateAll();
            }
        }

        // Overridden to disable base class behavior
        protected override void OnEnable() {
            // If TMPE was enabled by switching back from another tool (eg: bulldozer, free camera), show main menu panel.
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
            // If TMPE was disabled by switching to another tool, hide main menu panel.
            if (ModUI.Instance != null && ModUI.Instance.IsVisible())
                ModUI.Instance.CloseMainMenu();

            // revert to normal mode if underground
            if (IsUndergroundMode) {
                Log._Debug("Toolbar disable: Restoring overground mode");
                InfoManager.instance.SetCurrentMode(InfoManager.InfoMode.None, InfoManager.SubInfoMode.Default);
            }

            MassEditOverlay.Show = RoadSelectionPanels.Root?.ShouldShowMassEditOverlay() ?? false;
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
        /// renders persistent overlay.
        /// if any subtool is active it renders overlay for that subtool (e.g. node selection, segment selection, etc.)
        /// Must not call base.RenderOverlay() . Doing so may cause infinite recursion with Postfix of base.RenderOverlay()
        /// </summary>
        public void RenderOverlayImpl(RenderManager.CameraInfo cameraInfo) {
            try {
                if(!(isActiveAndEnabled || SubTools.PrioritySigns.MassEditOverlay.IsActive)) {
                    return;
                }

                activeLegacySubTool_?.RenderOverlay(cameraInfo);
                activeSubTool_?.RenderActiveToolOverlay(cameraInfo);

                ToolMode currentMode = this.GetToolMode();

                // For all _other_ legacy subtools let them render something too
                foreach (var legacySubtool in this.legacySubTools_) {
                    if (legacySubtool.Key == currentMode) {
                        continue;
                    }

                    legacySubtool.Value?.RenderOverlayForOtherTools(cameraInfo);
                }

                foreach (var subtool in this.subTools_) {
                  if (subtool.Key != this.GetToolMode()) {
                      subtool.Value.RenderGenericInfoOverlay(cameraInfo);
                  }
                }
            } catch(Exception ex) {
                ex.LogException();
            }
        }

        /// <summary>
        /// Renders overlay when no subtool is active.
        /// May call base.RenderOverlay() without risk of infinite recursion.
        /// </summary>
        void DefaultRenderOverlay(RenderManager.CameraInfo cameraInfo)
        {
            if (!TMPELifecycle.PlayMode) {
                return; // world info view panels are not available in edit mode
            }
            MassEditOverlay.Show = ControlIsPressed || RoadSelectionPanels.Root.ShouldShowMassEditOverlay();

            NetManager.instance.NetAdjust.PathVisible =
                RoadSelectionPanels.Root.ShouldPathBeVisible();
            if (NetManager.instance.NetAdjust.PathVisible) {
                base.RenderOverlay(cameraInfo); // render path.
            }

            var netAdjust = NetManager.instance?.NetAdjust;

            if (netAdjust == null) {
                return;
            }

            // use the same color as in NetAdjust
            ref NetSegment segment = ref HoveredSegmentId.ToSegment();
            var color = ToolsModifierControl.toolController.m_validColorInfo;
            float alpha = 1f;
            if (HoveredSegmentId != 0) {
                NetTool.CheckOverlayAlpha(ref segment, ref alpha);
            }

            color.a *= alpha;
            if (SelectedNodeId != 0) {
                Highlight.DrawNodeCircle(cameraInfo, SelectedNodeId, color);
            }

            if (ReadjustPathMode) {
                if (Input.GetMouseButton(0)) {
                    color = GetToolColor(Input.GetMouseButton(0), false);
                }
                bool isRoundabout = RoundaboutMassEdit.Instance.TraverseLoop(HoveredSegmentId, out var segmentList);
                if (!isRoundabout) {
                    var segments = SegmentTraverser.Traverse(
                        initialSegmentId: HoveredSegmentId,
                        direction: TraverseDirection.AnyDirection,
                        side: TraverseSide.Straight,
                        stopCrit: SegmentStopCriterion.None,
                        visitorFun: (_) => true);
                    segmentList = new List<ushort>(segmentList);
                }
                foreach (ushort segmentId in segmentList ?? Enumerable.Empty<ushort>()) {
                    NetTool.RenderOverlay(
                        cameraInfo: cameraInfo,
                        segment: ref segmentId.ToSegment(),
                        importantColor: color,
                        nonImportantColor: color);
                }
            } else if (NodeSelectionMode) {
                if (HoveredNodeId != SelectedNodeId && HoveredNodeId != 0) {
                    Highlight.DrawNodeCircle(cameraInfo, HoveredNodeId, color);
                }
            } else if (HoveredSegmentId != 0) {
                NetTool.RenderOverlay(cameraInfo, ref segment, color, color);
            }
        }

        /// <summary>
        /// Primarily handles click events on hovered nodes/segments
        /// </summary>
        protected override void OnToolUpdate() {
            try {
                base.OnToolUpdate();

                // Log._Debug($"OnToolUpdate");
                if (KeybindSettingsBase.ElevationDown.KeyUp()) {
                    InfoManager.instance.SetCurrentMode(
                        InfoManager.InfoMode.Underground,
                        InfoManager.SubInfoMode.Default);
                    UIView.library.Hide("TrafficInfoViewPanel");
                } else if (KeybindSettingsBase.ElevationUp.KeyUp()) {
                    InfoManager.instance.SetCurrentMode(
                        InfoManager.InfoMode.None,
                        InfoManager.SubInfoMode.Default);
                }
                ToolCursor = null;
                bool elementsHovered = DetermineHoveredElements(activeLegacySubTool_ is not LaneConnectorTool);
                if (activeLegacySubTool_?.OverrideCursor != null) {
                    ToolCursor = activeLegacySubTool_.OverrideCursor;
                } else {
                    if (activeLegacySubTool_ != null && NetTool != null && elementsHovered) {
                        ToolCursor = NetTool.m_upgradeCursor;
                    }
                }

                bool primaryMouseClicked = Input.GetMouseButtonDown(0);
                bool secondaryMouseClicked = Input.GetMouseButtonUp(1);

                // check if clicked
                if(!primaryMouseClicked && !secondaryMouseClicked) {
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

                if(!mouseInsideAnyPanel) {
                    if(primaryMouseClicked) {
                        activeLegacySubTool_?.OnPrimaryClickOverlay();
                        activeSubTool_?.OnToolLeftClick();
                    }

                    if(secondaryMouseClicked) {
                        if(GetToolMode() == ToolMode.None) {
                            RoadSelectionPanels roadSelectionPanels = UIView.GetAView().GetComponent<RoadSelectionPanels>();
                            if (roadSelectionPanels && roadSelectionPanels.RoadWorldInfoPanelExt && roadSelectionPanels.RoadWorldInfoPanelExt.isVisible) {
                                RoadSelectionPanels.RoadWorldInfoPanel.Hide();
                            } else if (SelectedNodeId != 0) {
                                SelectedNodeId = 0;
                                RequestOnscreenDisplayUpdate();
                            } else {
                                ModUI.Instance.CloseMainMenu();
                            }
                        } else {
                            activeLegacySubTool_?.OnSecondaryClickOverlay();
                            activeSubTool_?.OnToolRightClick();
                        }
                    }
                }
            } catch(Exception ex) {
                ex.LogException();
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

                if (SavedGameOptions.Instance.nodesOverlay) {
                    DebugGuiDisplaySegments();
                    DebugGuiDisplayNodes();
                }

                if (SavedGameOptions.Instance.vehicleOverlay) {
                    DebugGuiDisplayVehicles();
                }

                if (SavedGameOptions.Instance.citizenOverlay) {
                    DebugGuiDisplayCitizens();
                }

                if (SavedGameOptions.Instance.buildingOverlay) {
                    DebugGuiDisplayBuildings();
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

                if (activeLegacySubTool_ != null) {
                    activeLegacySubTool_.OnToolGUI(e);
                } else if (activeSubTool_ != null) {
                    activeSubTool_.UpdateEveryFrame();
                }
            } catch (Exception ex) {
                Log.Error("GUI Error: " + ex);
            }
        }

        private void DefaultOnToolGUI(Event e) {
            if (!TMPELifecycle.PlayMode) {
                return; // world info view panels are not available in edit mode
            }
            if (e.type == EventType.MouseDown && e.button == 0) {
                if (NodeSelectionMode) {
                    SelectedNodeId = HoveredNodeId;
                    InstanceManager.instance.SelectInstance(new InstanceID { NetNode = SelectedNodeId });
                    RequestOnscreenDisplayUpdate();
                } else {
                    bool isRoad = HoveredSegmentId != 0 && HoveredSegmentId.ToSegment().Info.m_netAI is RoadBaseAI;
                    if (!isRoad)
                        return;

                    if (ReadjustPathMode) {
                        bool isRoundabout = RoundaboutMassEdit.Instance.TraverseLoop(HoveredSegmentId, out var segmentList);
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

                    OpenWorldInfoPanel(new InstanceID { NetSegment = HoveredSegmentId }, HitPos);
                }
            } else if (SelectedNodeId != 0 && KeybindSettingsBase.RestoreDefaultsKey.KeyDown(e)) {
                ushort nodeId = SelectedNodeId;
                SimulationManager.instance.m_ThreadingWrapper.QueueSimulationThread(() => {
                    PriorityRoad.EraseAllTrafficRoadsForNode(nodeId);
                    SimulationManager.instance.m_ThreadingWrapper.QueueMainThread(RoadSelectionUtil.ShowMassEditOverlay);
                });
            }
        }

        public bool IsNodeVisible(ushort node) {
            return node.IsUndergroundNode() == IsUndergroundMode;
        }

        // public void DrawNodeCircle(RenderManager.CameraInfo cameraInfo,
        //                            ushort nodeId,
        //                            bool warning = false,
        //                            bool alpha = false,
        //                            bool overrideRenderLimits = false) {
        //     DrawNodeCircle(
        //         cameraInfo: cameraInfo,
        //         nodeId: nodeId,
        //         color: GetToolColor(warning: warning, error: false),
        //         alpha: alpha,
        //         overrideRenderLimits: overrideRenderLimits);
        // }

        /// <summary>
        /// Gets the coordinates of the given node.
        /// </summary>
        private static Vector3 GetNodePos(ushort nodeId) {
            ref NetNode netNode = ref nodeId.ToNode();
            Vector3 pos = netNode.m_position;
            float terrainY = Singleton<TerrainManager>.instance.SampleDetailHeightSmooth(pos);
            if (terrainY > pos.y) {
                pos.y = terrainY;
            }
            return pos;
        }

        /// <summary>Shows a tutorial message. Must be called by a Unity thread.</summary>
        /// <param name="localeKey">Tutorial key.</param>
        public static void ShowAdvisor(string localeKey) {
            if (!GlobalConfig.Instance.Main.EnableTutorial || !TMPELifecycle.PlayMode) {
                return;
            }

            if (!Translation.Tutorials.HasString(Translation.TUTORIAL_BODY_KEY_PREFIX + localeKey)) {
                Log.Warning($"ShowAdvisor: localeKey:{localeKey} does not exist");
                return;
            }

            Log._Debug($"TrafficManagerTool.ShowAdvisor({localeKey}) called.");
            TutorialAdvisorPanel tutorialPanel = ToolsModifierControl.advisorPanel;
            string key = Translation.TUTORIAL_KEY_PREFIX + localeKey;

            if (GlobalConfig.Instance.Main.DisplayedTutorialMessages.Contains(localeKey)) {
                tutorialPanel.Refresh(key, "ToolbarIconZoomOutGlobe", string.Empty);
            } else {
                tutorialPanel.Show(key, "ToolbarIconZoomOutGlobe", string.Empty, 0f);
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
                                 (activeLegacySubTool_ == null || !activeLegacySubTool_.IsCursorInPanel());

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

                if(segmentId != 0 && raycastSegment) {
                    HitPos = segmentOutput.m_hitPos;
                    HoveredSegmentId = segmentId;
                }

                if (HoveredNodeId <= 0 && segmentId > 0) {
                    // alternative way to get a node hit: check distance to start and end nodes
                    // of the segment

                    ref NetSegment netSegment = ref segmentId.ToSegment();
                    ushort startNodeId = netSegment.m_startNode;
                    ushort endNodeId = netSegment.m_endNode;

                    float startDist = (segmentOutput.m_hitPos - startNodeId.ToNode().m_position).magnitude;
                    float endDist = (segmentOutput.m_hitPos - endNodeId.ToNode().m_position).magnitude;
                    float detectionRadius = raycastSegment ? NODE_DETECTION_RADIUS : PRECISE_NODE_DETECTION_RADIUS;
                    if (startDist < endDist && startDist < detectionRadius) {
                        HoveredNodeId = startNodeId;
                    } else if (endDist < startDist && endDist < detectionRadius) {
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

        /// <summary>Displays lane ids over lanes.</summary>
        // TODO: Extract into a Debug Tool GUI class
        private void DebugGuiDisplayLanes(ushort segmentId,
                                          ref NetSegment segment,
                                          ref NetInfo segmentInfo)
        {
            var _counterStyle = new GUIStyle();
            Vector3 centerPos = segment.m_bounds.center;
            bool visible = GeometryUtil.WorldToScreenPoint(centerPos, out Vector3 screenPos);

            if (!visible) {
                return;
            }

            screenPos.y -= 200;

            if (screenPos.z < 0) {
                return;
            }

            Vector3 camPos = Singleton<SimulationManager>.instance.m_simulationView.m_position;
            Vector3 diff = centerPos - camPos;

            if (diff.magnitude > DEBUG_CLOSE_LOD) {
                return; // do not draw if too distant
            }

            float zoom = 1.0f / diff.magnitude * 150f;

            _counterStyle.fontSize = (int)(11f * zoom);
            _counterStyle.normal.textColor = new Color(1f, 1f, 0f);

            // uint totalDensity = 0u;
            // for (int i = 0; i < segmentInfo.m_lanes.Length; ++i) {
            //        if (CustomRoadAI.currentLaneDensities[segmentId] != null &&
            //         i < CustomRoadAI.currentLaneDensities[segmentId].Length)
            //                totalDensity += CustomRoadAI.currentLaneDensities[segmentId][i];
            // }

            uint curLaneId = segment.m_lanes;
            var labelSb = new StringBuilder();

            for (int i = 0; i < segmentInfo.m_lanes.Length; ++i) {
                if (curLaneId == 0) {
                    break;
                }

                ref NetLane netLane = ref curLaneId.ToLane();

                bool laneTrafficDataLoaded =
                    TrafficMeasurementManager.Instance.GetLaneTrafficData(
                        segmentId,
                        (byte)i,
                        out LaneTrafficData laneTrafficData);

                NetInfo.Lane laneInfo = segmentInfo.m_lanes[i];

#if PFTRAFFICSTATS
                uint pfTrafficBuf =
                    TrafficMeasurementManager
                        .Instance.segmentDirTrafficData[
                            TrafficMeasurementManager.Instance.GetDirIndex(
                                segmentId,
                                laneInfo.m_finalDirection)]
                        .totalPathFindTrafficBuffer;
#endif
                // TrafficMeasurementManager.Instance.GetTrafficData(segmentId,
                // laneInfo.m_finalDirection, out dirTrafficData);
                // int dirIndex = laneInfo.m_finalDirection == NetInfo.Direction.Backward ? 1 : 0;

                labelSb.AppendFormat("L idx {0}, id {1}", i, curLaneId);
#if DEBUG
                labelSb.AppendFormat(
                    ", in: {0}, out: {1}, f: {2}, l: {3}, rst: {4}, dir: {5}, fnl: {6}, " +
                    "pos: {7:0.##}, sim: {8} for {9}/{10}",
                    RoutingManager.Instance.CalcInnerSimilarLaneIndex(segmentId, i),
                    RoutingManager.Instance.CalcOuterSimilarLaneIndex(segmentId, i),
                    (NetLane.Flags)curLaneId.ToLane().m_flags,
                    SpeedLimitManager.Instance.CalculateCustomSpeedLimit(curLaneId).ToString(),
                    VehicleRestrictionsManager.Instance.GetAllowedVehicleTypes(
                        segmentId,
                        segmentInfo,
                        (uint)i,
                        laneInfo,
                        VehicleRestrictionsMode.Configured),
                    laneInfo.m_direction,
                    laneInfo.m_finalDirection,
                    laneInfo.m_position,
                    laneInfo.m_similarLaneIndex,
                    laneInfo.m_vehicleType,
                    laneInfo.m_laneType);
#endif
                if (laneTrafficDataLoaded) {
                    labelSb.AppendFormat(
                        ", sp: {0}%",
                        TrafficMeasurementManager.Instance.CalcLaneRelativeMeanSpeed(
                            segmentId,
                            (byte)i,
                            curLaneId,
                            laneInfo) / 100);
#if DEBUG
                    labelSb.AppendFormat(
                        ", buf: {0}, max: {1}, acc: {2}",
                        laneTrafficData.trafficBuffer,
                        laneTrafficData.maxTrafficBuffer,
                        laneTrafficData.accumulatedSpeeds);

#if PFTRAFFICSTATS
                    labelSb.AppendFormat(
                        ", pfBuf: {0}/{1}, ({2} %)",
                        laneTrafficData.pathFindTrafficBuffer,
                        laneTrafficData.lastPathFindTrafficBuffer,
                        pfTrafficBuf > 0
                            ? "" + ((laneTrafficData.lastPathFindTrafficBuffer * 100u) /
                                    pfTrafficBuf)
                            : "n/a");
#endif
#endif
#if MEASUREDENSITY
                    if (dirTrafficDataLoaded) {
                        labelSb.AppendFormat(
                            ", rel. dens.: {0}%",
                            dirTrafficData.accumulatedDensities > 0
                                ? "" + Math.Min(
                                      laneTrafficData[i].accumulatedDensities * 100 /
                                      dirTrafficData.accumulatedDensities,
                                      100)
                                : "?");
                    }

                    labelSb.AppendFormat(
                        ", acc: {0}",
                        laneTrafficData[i].accumulatedDensities);
#endif
                }

                labelSb.AppendFormat(", nd: {0}", netLane.m_nodes);
#if DEBUG
                //    labelSb.AppendFormat(
                //        " ({0}/{1}/{2})",
                //        CustomRoadAI.currentLaneDensities[segmentId] != null &&
                //        i < CustomRoadAI.currentLaneDensities[segmentId].Length
                //            ? string.Empty + CustomRoadAI.currentLaneDensities[segmentId][i]
                //            : "?",
                //        CustomRoadAI.maxLaneDensities[segmentId] != null &&
                //        i < CustomRoadAI.maxLaneDensities[segmentId].Length
                //            ? string.Empty + CustomRoadAI.maxLaneDensities[segmentId][i]
                //            : "?",
                //        totalDensity);
                //    labelSb.AppendFormat(
                //        " ({0}/{1})",
                //        CustomRoadAI.currentLaneDensities[segmentId] != null &&
                //        i < CustomRoadAI.currentLaneDensities[segmentId].Length
                //            ? string.Empty + CustomRoadAI.currentLaneDensities[segmentId][i]
                //            : "?",
                //        totalDensity);
#endif
                //    labelSb.AppendFormat(
                //        ", abs. dens.: {0} %",
                //        CustomRoadAI.laneMeanAbsDensities[segmentId] != null &&
                //        i < CustomRoadAI.laneMeanAbsDensities[segmentId].Length
                //            ? "" + CustomRoadAI.laneMeanAbsDensities[segmentId][i]
                //            : "?");
                labelSb.Append("\n");

                curLaneId = netLane.m_nextLane;
            }

            var labelStr = labelSb.ToString();
            Vector2 dim = _counterStyle.CalcSize(new GUIContent(labelStr));
            Rect labelRect = new Rect(screenPos.x - (dim.x / 2f), screenPos.y, dim.x, dim.y);

            GUI.Label(labelRect, labelStr, _counterStyle);
        }

        /// <summary>Displays segment ids over segments.</summary>
        // TODO: Extract into a Debug Tool GUI class
        private void DebugGuiDisplaySegments() {
            TrafficMeasurementManager trafficMeasurementManager = TrafficMeasurementManager.Instance;
            NetManager netManager = Singleton<NetManager>.instance;
            GUIStyle counterStyle = new GUIStyle();
            IExtSegmentEndManager endMan = Constants.ManagerFactory.ExtSegmentEndManager;

            for (int segmentId = 1; segmentId < NetManager.MAX_SEGMENT_COUNT; ++segmentId) {
                ref NetSegment netSegment = ref ((ushort)segmentId).ToSegment();

                if ((netSegment.m_flags & NetSegment.Flags.Created) == NetSegment.Flags.None) {
                    continue;
                }

                NetInfo segmentInfo = netSegment.Info;

                ItemClass.Service service = segmentInfo.GetService();
                ItemClass.SubService subService = segmentInfo.GetSubService();
#if !DEBUG
                if ((netSegment.m_flags & NetSegment.Flags.Untouchable) != NetSegment.Flags.None) {
                    continue;
                }
#endif
                Vector3 centerPos = netSegment.m_bounds.center;
                bool visible = GeometryUtil.WorldToScreenPoint(centerPos, out Vector3 screenPos);
                if (!visible) {
                    continue;
                }

                Vector3 camPos = Singleton<SimulationManager>.instance.m_simulationView.m_position;
                Vector3 diff = centerPos - camPos;

                if (diff.magnitude > DEBUG_CLOSE_LOD) {
                    continue; // do not draw if too distant
                }

                float zoom = 1.0f / diff.magnitude * 150f;
                counterStyle.fontSize = (int)(12f * zoom);
                counterStyle.normal.textColor = new Color(1f, 0f, 0f);

                var labelSb = new StringBuilder();
                labelSb.AppendFormat("Segment {0}", segmentId);
#if DEBUG
                labelSb.AppendFormat(", flags: {0}", netSegment.m_flags);
                labelSb.AppendFormat("\nsvc: {0}, sub: {1}", service, subService);

                ref ExtSegmentEnd startSegmentEnd = ref endMan.ExtSegmentEnds[endMan.GetIndex((ushort)segmentId, true)];
                uint startVehicles = endMan.GetRegisteredVehicleCount(ref startSegmentEnd);

                ref ExtSegmentEnd endSegmentEnd = ref endMan.ExtSegmentEnds[endMan.GetIndex((ushort)segmentId, false)];
                uint endVehicles = endMan.GetRegisteredVehicleCount(ref endSegmentEnd);

                labelSb.AppendFormat( "\nstart veh.: {0}, end veh.: {1}", startVehicles, endVehicles);
                labelSb.AppendFormat( "\nstart arrows: {0}, end arrows: {1}", startSegmentEnd.laneArrows, endSegmentEnd.laneArrows);
#endif
                labelSb.AppendFormat("\nTraffic: {0} %", netSegment.m_trafficDensity);

#if DEBUG
                int fwdSegIndex = trafficMeasurementManager.GetDirIndex(
                    (ushort)segmentId,
                    NetInfo.Direction.Forward);
                int backSegIndex = trafficMeasurementManager.GetDirIndex(
                    (ushort)segmentId,
                    NetInfo.Direction.Backward);

                labelSb.Append("\n");

#if MEASURECONGESTION
                float fwdCongestionRatio =
                    trafficMeasurementManager
                        .segmentDirTrafficData[fwdSegIndex].numCongestionMeasurements > 0
                        ? ((uint)trafficMeasurementManager.segmentDirTrafficData[fwdSegIndex].numCongested * 100u) /
                          (uint)trafficMeasurementManager.segmentDirTrafficData[fwdSegIndex].numCongestionMeasurements
                        : 0; // now in %
                float backCongestionRatio =
                    trafficMeasurementManager
                        .segmentDirTrafficData[backSegIndex].numCongestionMeasurements > 0
                        ? ((uint)trafficMeasurementManager.segmentDirTrafficData[backSegIndex].numCongested * 100u) /
                          (uint)trafficMeasurementManager.segmentDirTrafficData[backSegIndex].numCongestionMeasurements
                        : 0; // now in %


                labelSb.Append("min speeds: ");
                labelSb.AppendFormat(
                        " {0}%/{1}%",
                        trafficMeasurementManager.segmentDirTrafficData[fwdSegIndex].minSpeed / 100,
                        trafficMeasurementManager.segmentDirTrafficData[backSegIndex].minSpeed /
                        100);
                labelSb.Append(", ");
#endif
                labelSb.Append("mean speeds: ");
                labelSb.AppendFormat(
                        " {0}%/{1}%",
                        trafficMeasurementManager.SegmentDirTrafficData[fwdSegIndex].meanSpeed /
                        100,
                        trafficMeasurementManager.SegmentDirTrafficData[backSegIndex].meanSpeed /
                        100);
#if PFTRAFFICSTATS || MEASURECONGESTION
                labelSb.Append("\n");
#endif
#if PFTRAFFICSTATS
                labelSb.Append("pf bufs: ");
                labelSb.AppendFormat(
                    " {0}/{1}",
                    trafficMeasurementManager.segmentDirTrafficData[fwdSegIndex].totalPathFindTrafficBuffer,
                    trafficMeasurementManager.segmentDirTrafficData[backSegIndex].totalPathFindTrafficBuffer);
#endif
#if PFTRAFFICSTATS && MEASURECONGESTION
                labelSb.Append(", ");
#endif
#if MEASURECONGESTION
                labelSb.Append("cong: ");
                labelSb.AppendFormat(
                    " {0}% ({1}/{2})/{3}% ({4}/{5})",
                    fwdCongestionRatio,
                    trafficMeasurementManager.segmentDirTrafficData[fwdSegIndex].numCongested,
                    trafficMeasurementManager.segmentDirTrafficData[fwdSegIndex].numCongestionMeasurements,
                    backCongestionRatio,
                    trafficMeasurementManager.segmentDirTrafficData[backSegIndex].numCongested,
                    trafficMeasurementManager.segmentDirTrafficData[backSegIndex].numCongestionMeasurements);
#endif
                labelSb.AppendFormat(
                    "\nstart: {0}, end: {1}",
                    netSegment.m_startNode,
                    netSegment.m_endNode);
#endif

                var labelStr = labelSb.ToString();
                Vector2 dim = counterStyle.CalcSize(new GUIContent(labelStr));
                Rect labelRect = new Rect(screenPos.x - (dim.x / 2f), screenPos.y, dim.x, dim.y);

                GUI.Label(labelRect, labelStr, counterStyle);

                if (SavedGameOptions.Instance.showLanes) {
                    DebugGuiDisplayLanes(
                        (ushort)segmentId,
                        ref netSegment,
                        ref segmentInfo);
                }
            }
        }

        /// <summary>Displays node ids over nodes.</summary>
        // TODO: Extract into a Debug Tool GUI class
        private void DebugGuiDisplayNodes() {
            var counterStyle = new GUIStyle();

            for (int i = 1; i < NetManager.MAX_NODE_COUNT; ++i) {
                ref NetNode netNode = ref ((ushort)i).ToNode();
                if ((netNode.m_flags & NetNode.Flags.Created) == NetNode.Flags.None) {
                    continue;
                }

                Vector3 pos = netNode.m_position;
                bool visible = GeometryUtil.WorldToScreenPoint(pos, out Vector3 screenPos);
                if (!visible) {
                    continue;
                }

                Vector3 camPos = Singleton<SimulationManager>.instance.m_simulationView.m_position;
                Vector3 diff = pos - camPos;
                if (diff.magnitude > DEBUG_CLOSE_LOD) {
                    continue; // do not draw if too distant
                }

                float zoom = 1.0f / diff.magnitude * 150f;

                counterStyle.fontSize = (int)(15f * zoom);
                counterStyle.normal.textColor = new Color(0f, 0f, 1f);

                string labelStr = "Node " + i;
#if DEBUG
                labelStr += string.Format(
                    "\nflags: {0}\nlane: {1}",
                    netNode.m_flags,
                    netNode.m_lane);
#endif
                Vector2 dim = counterStyle.CalcSize(new GUIContent(labelStr));
                var labelRect = new Rect(screenPos.x - (dim.x / 2f), screenPos.y, dim.x, dim.y);

                GUI.Label(labelRect, labelStr, counterStyle);
            }
        }

        /// <summary>Displays vehicle ids over vehicles.</summary>
        // TODO: Extract into a Debug Tool GUI class
        private void DebugGuiDisplayVehicles() {
            GUIStyle _counterStyle = new GUIStyle();
            SimulationManager simManager = Singleton<SimulationManager>.instance;
            ExtVehicleManager vehStateManager = ExtVehicleManager.Instance;
            CitizenManager citizenManager = CitizenManager.instance;
            CitizenInstance[] citizenInstancesBuf = citizenManager.m_instances.m_buffer;

            int startVehicleId = 1;
            int endVehicleId = Singleton<VehicleManager>.instance.m_vehicles.m_buffer.Length - 1;
#if DEBUG
            if (DebugSettings.VehicleId != 0)
            {
                startVehicleId = DebugSettings.VehicleId;
                endVehicleId = DebugSettings.VehicleId;
            }
#endif
            for (int i = startVehicleId; i <= endVehicleId; ++i) {
                ushort vehicleId = (ushort)i;
                ref Vehicle vehicle = ref vehicleId.ToVehicle();

                if (vehicle.m_flags == 0) {
                    // node is unused
                    continue;
                }

                Vector3 vehPos = vehicle.GetSmoothPosition(vehicleId);
                bool visible = GeometryUtil.WorldToScreenPoint(vehPos, out Vector3 screenPos);

                if (!visible) {
                    continue;
                }

                Vector3 camPos = simManager.m_simulationView.m_position;
                Vector3 diff = vehPos - camPos;
                if (diff.magnitude > DEBUG_CLOSE_LOD) {
                    continue; // do not draw if too distant
                }

                float zoom = 1.0f / diff.magnitude * 150f;

                _counterStyle.fontSize = (int)(10f * zoom);
                _counterStyle.normal.textColor = new Color(1f, 1f, 1f);
                // _counterStyle.normal.background = MakeTex(1, 1, new Color(0f, 0f, 0f, 0.4f));

                ExtVehicle vState = vehStateManager.ExtVehicles[(ushort)i];
                ExtCitizenInstance driverInst =
                    ExtCitizenInstanceManager.Instance.ExtInstances[
                        Constants.ManagerFactory.ExtVehicleManager
                                 .GetDriverInstanceId(
                                     (ushort)i,
                                     ref vehicle)];
                // bool startNode = vState.currentStartNode;
                // ushort segmentId = vState.currentSegmentId;

                // Converting magnitudes into game speed float, and then into km/h
                SpeedValue vehSpeed = SpeedValue.FromVelocity(vehicle.GetLastFrameVelocity().magnitude);
#if DEBUG
                if (GlobalConfig.Instance.Debug.ExtPathMode != ExtPathMode.None &&
                    driverInst.pathMode != GlobalConfig.Instance.Debug.ExtPathMode) {
                    continue;
                }
#endif
                string labelStr = string.Format(
                    "V #{0} is a {1}{2} {3} @ ~{4} (len: {5:0.0}, {6} @ {7} ({8}), l. {9} " +
                    "-> {10}, l. {11}), w: {12}\n" +
                    "di: {13} dc: {14} m: {15} f: {16} l: {17} lid: {18} ltsu: {19} lpu: {20} " +
                    "als: {21} srnd: {22} trnd: {23}",
                    i,
                    vState.recklessDriver ? "reckless " : string.Empty,
                    vState.flags,
                    vState.vehicleType,
                    vehSpeed.ToKmphPrecise().ToString(),
                    vState.totalLength,
                    vState.junctionTransitState,
                    vState.currentSegmentId,
                    vState.currentStartNode,
                    vState.currentLaneIndex,
                    vState.nextSegmentId,
                    vState.nextLaneIndex,
                    vState.waitTime,
                    driverInst.instanceId,
                    citizenInstancesBuf[driverInst.instanceId].m_citizen,
                    driverInst.pathMode,
                    driverInst.failedParkingAttempts,
                    driverInst.parkingSpaceLocation,
                    driverInst.parkingSpaceLocationId,
                    vState.lastTransitStateUpdate,
                    vState.lastPositionUpdate,
                    vState.lastAltLaneSelSegmentId,
                    Constants.ManagerFactory.ExtVehicleManager.GetStaticVehicleRand((ushort)i),
                    Constants.ManagerFactory.ExtVehicleManager.GetTimedVehicleRand((ushort)i));

                Vector2 dim = _counterStyle.CalcSize(new GUIContent(labelStr));
                Rect labelRect = new Rect(
                    screenPos.x - (dim.x / 2f),
                    screenPos.y - dim.y - 50f,
                    dim.x,
                    dim.y);

                GUI.Box(labelRect, labelStr, _counterStyle);
            }
        }

        /// <summary>Displays debug data over citizens. </summary>
        // TODO: Extract into a Debug Tool GUI class
        private void DebugGuiDisplayCitizens() {
            GUIStyle counterStyle = new GUIStyle();

            ExtCitizen[] extCitizensBuf = ExtCitizenManager.Instance.ExtCitizens;
            CitizenManager citizenManager = CitizenManager.instance;
            CitizenInstance[] citizenInstancesBuf = citizenManager.m_instances.m_buffer;
            Citizen[] citizensBuf = citizenManager.m_citizens.m_buffer;
            uint maxCitizenInstanceCount = citizenManager.m_instances.m_size;

            for (uint citizenInstanceId = 1; citizenInstanceId < maxCitizenInstanceCount; ++citizenInstanceId) {
                ref CitizenInstance citizenInstance = ref citizenInstancesBuf[citizenInstanceId];

                if (!citizenInstance.IsCreated()) {
                    continue;
                }
#if DEBUG
                if (DebugSwitch.NoValidPathCitizensOverlay.Get()) {
#endif
                    if (citizenInstance.m_path != 0) {
                        continue;
                    }
#if DEBUG
                }
#endif

                Vector3 pos = citizenInstance.GetSmoothPosition((ushort)citizenInstanceId);
                bool visible = GeometryUtil.WorldToScreenPoint(pos, out Vector3 screenPos);

                if (!visible) {
                    continue;
                }

                Vector3 camPos = Singleton<SimulationManager>.instance.m_simulationView.m_position;
                Vector3 diff = pos - camPos;

                if (diff.magnitude > DEBUG_CLOSE_LOD) {
                    continue; // do not draw if too distant
                }

                float zoom = 1.0f / diff.magnitude * 150f;

                counterStyle.fontSize = (int)(10f * zoom);
                counterStyle.normal.textColor = new Color(1f, 0f, 1f);
                // _counterStyle.normal.background = MakeTex(1, 1, new Color(0f, 0f, 0f, 0.4f));

#if DEBUG
                if (GlobalConfig.Instance.Debug.ExtPathMode != ExtPathMode.None &&
                    ExtCitizenInstanceManager.Instance.ExtInstances[citizenInstanceId].pathMode !=
                    GlobalConfig.Instance.Debug.ExtPathMode) {
                    continue;
                }
#endif

                var labelSb = new StringBuilder();
                uint citizenId = citizenInstance.m_citizen;
                labelSb.AppendFormat(
                    "Inst. {0}, Cit. {1},\nm: {2}, tm: {3}, ltm: {4}, ll: {5}",
                    citizenInstanceId,
                    citizenId,
                    ExtCitizenInstanceManager.Instance.ExtInstances[citizenInstanceId].pathMode,
                    extCitizensBuf[citizenId].transportMode,
                    extCitizensBuf[citizenId].lastTransportMode,
                    extCitizensBuf[citizenId].lastLocation);

                if (citizenId != 0) {
                    ref Citizen citizen = ref citizensBuf[citizenId];
                    if (citizen.m_parkedVehicle != 0) {
                        labelSb.AppendFormat(
                            "\nparked: {0} dist: {1}",
                            citizen.m_parkedVehicle,
                            (citizen.m_parkedVehicle.ToParkedVehicle().m_position - pos).magnitude);
                    }

                    if (citizen.m_vehicle != 0) {
                        labelSb.AppendFormat(
                            "\nveh: {0} dist: {1}",
                            citizen.m_vehicle,
                            (citizen.m_vehicle.ToVehicle().GetLastFramePosition() - pos).magnitude);
                    }
                }

                string labelStr = labelSb.ToString();
                Vector2 dim = counterStyle.CalcSize(new GUIContent(labelStr));
                Rect labelRect = new Rect(
                    screenPos.x - (dim.x / 2f),
                    screenPos.y - dim.y - 50f,
                    dim.x,
                    dim.y);

                GUI.Box(labelRect, labelStr, counterStyle);
            }
        }

        // TODO: Extract into a Debug Tool GUI class
        private void DebugGuiDisplayBuildings() {
            GUIStyle _counterStyle = new GUIStyle();

            for (int buildingId = 1; buildingId < BuildingManager.MAX_BUILDING_COUNT; ++buildingId) {
                ref Building building = ref ((ushort)buildingId).ToBuilding();

                if ((building.m_flags & Building.Flags.Created) == Building.Flags.None) {
                    continue;
                }

                bool visible = GeometryUtil.WorldToScreenPoint(building.m_position, out Vector3 screenPos);

                if (!visible) {
                    continue;
                }

                Vector3 camPos = Singleton<SimulationManager>.instance.m_simulationView.m_position;
                Vector3 diff = building.m_position - camPos;
                if (diff.magnitude > DEBUG_CLOSE_LOD) {
                    continue; // do not draw if too distant
                }

                float zoom = 150f / diff.magnitude;

                _counterStyle.fontSize = (int)(10f * zoom);
                _counterStyle.normal.textColor = new Color(0f, 1f, 0f);
                // _counterStyle.normal.background = MakeTex(1, 1, new Color(0f, 0f, 0f, 0.4f));

                string labelStr = string.Format(
                    "Building {0}, PDemand: {1}, IncTDem: {2}, OutTDem: {3}",
                    buildingId,
                    ExtBuildingManager.Instance.ExtBuildings[buildingId].parkingSpaceDemand,
                    ExtBuildingManager.Instance.ExtBuildings[buildingId].incomingPublicTransportDemand,
                    ExtBuildingManager.Instance.ExtBuildings[buildingId].outgoingPublicTransportDemand);

                Vector2 dim = _counterStyle.CalcSize(new GUIContent(labelStr));
                Rect labelRect = new Rect(
                    screenPos.x - (dim.x / 2f),
                    screenPos.y - dim.y - 50f,
                    dim.x,
                    dim.y);

                GUI.Box(labelRect, labelStr, _counterStyle);
            }
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
        [Obsolete("Avoid using LegacyTool, Immediate Mode GUI and OnToolGUI, use new U GUI instead.")]
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
                ScreenDisplay();
            }
        }

        /// <summary>Clear the OSD panel and display the idle hint.</summary>
        public void ScreenDisplay() {
            var items = new List<OsdItem>();
            items.Add(new MainMenu.OSD.Label(
                          localizedText: Translation.Menu.Get("Onscreen.Idle:Choose a tool")));
            items.Add(new MainMenu.OSD.HardcodedMouseShortcut(
                button: ColossalFramework.UI.UIMouseButton.Left,
                shift: false,
                ctrl: false,
                alt: false,
                localizedText: Translation.Menu.Get("Onscreen.Default:Select a road"))); // select a road to set as main road.
            items.Add(new MainMenu.OSD.HardcodedMouseShortcut(
                button: ColossalFramework.UI.UIMouseButton.Left,
                shift: false,
                ctrl: false,
                alt: true,
                localizedText: Translation.Menu.Get("Onscreen.Default:Select a node"))); // select a node to erase all traffic rules.

            if (SelectedNodeId != 0) {
                items.Add(new MainMenu.OSD.Shortcut(
                    keybindSetting: KeybindSettingsBase.RestoreDefaultsKey,
                    localizedText: Translation.Menu.Get("Onscreen.Default:Erase"))); // Erase all traffic rules from selected node.
                items.Add(new MainMenu.OSD.HardcodedMouseShortcut(
                    button: ColossalFramework.UI.UIMouseButton.Right,
                    shift: false,
                    ctrl: false,
                    alt: false,
                    localizedText: Translation.Menu.Get("Onscreen.Default:Deselect node")));
            }

            items.Add(new MainMenu.OSD.HoldModifier(
                shift: false,
                ctrl: true,
                alt: false,
                localizedText: Translation.Menu.Get("Onscreen.Default:Show traffic rules"))); // show traffic rules for high priority roads

            OnscreenDisplay.Display(items);
        }

        public void AddUUIButton() {
            try {
                var hotkeys = new UUIHotKeys { ActivationKey = KeybindSettingsBase.ToggleMainMenu.Key };
                hotkeys.AddInToolKey(KeybindSettingsBase.ToggleTrafficLightTool.Key);
                hotkeys.AddInToolKey(KeybindSettingsBase.LaneArrowTool.Key);
                hotkeys.AddInToolKey(KeybindSettingsBase.LaneConnectionsTool.Key, () => SavedGameOptions.Instance.laneConnectorEnabled);
                hotkeys.AddInToolKey(KeybindSettingsBase.PrioritySignsTool.Key, () => SavedGameOptions.Instance.prioritySignsEnabled);
                hotkeys.AddInToolKey(KeybindSettingsBase.JunctionRestrictionsTool.Key, () => SavedGameOptions.Instance.junctionRestrictionsEnabled);
                hotkeys.AddInToolKey(KeybindSettingsBase.SpeedLimitsTool.Key, () => SavedGameOptions.Instance.customSpeedLimitsEnabled);
                hotkeys.AddInToolKey(KeybindSettingsBase.LaneConnectorStayInLane.Key, () => activeLegacySubTool_ is LaneConnectorTool);

                UUIButton = UUIHelpers.RegisterToolButton(
                    name: "TMPE",
                    groupName: null, // default group
                    tooltip: "TMPE",
                    tool: this,
                    icon: TextureResources.LoadDllResource("MainMenu.MainMenuButton-fg-normal.png", new IntVector2(40)),
                    hotkeys: hotkeys);

                UUIButton.isVisible = GlobalConfig.Instance.Main.UseUUI;
            } catch(Exception ex) {
                ex.LogException();
            }
        }

        public void RemoveUUIButton() {
            if (UUIButton) {
                Destroy(UUIButton.gameObject);
            }
            UUIButton = null;
        }
    }
}