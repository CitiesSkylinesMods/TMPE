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
    using ColossalFramework.Math;
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using JetBrains.Annotations;
    using TrafficManager.Manager.Impl;
    using TrafficManager.State;
#if DEBUG
    using TrafficManager.State.ConfigData;
#endif
    using TrafficManager.UI.MainMenu;
    using TrafficManager.UI.SubTools;
    using TrafficManager.UI.SubTools.SpeedLimits;
    using TrafficManager.Util;
    using UnityEngine;
    using TrafficManager.UI.Helpers;
    using TrafficManager.UI.SubTools.LaneArrows;

    [UsedImplicitly]
    public class TrafficManagerTool
        : DefaultTool,
          IObserver<GlobalConfig>
    {
        public GuideHandler Guide;

        private ToolMode toolMode_;

        private NetTool netTool_;

        /// <summary>Maximum error of HitPos field.</summary>
        internal const float MAX_HIT_ERROR = 2.5f;

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

        protected override void OnDestroy() {
            Log.Info("TrafficManagerTool.OnDestroy() called");
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
            bool ret = false;
            Constants.ServiceFactory.NetService.ProcessNode(
                nodeId,
                (ushort nId, ref NetNode node) => {
                    ret = IsPosWithinOverlayDistance(node.m_position);
                    return true;
                });
            return ret;
        }

        // Not used
        // TODO: Move to UI.Helpers
        [UsedImplicitly]
        internal static bool IsSegmentWithinViewDistance(ushort segmentId) {
            bool ret = false;
            Constants.ServiceFactory.NetService.ProcessSegment(
                segmentId,
                (ushort segId, ref NetSegment segment) => {
                    Vector3 centerPos = segment.m_bounds.center;
                    ret = IsPosWithinOverlayDistance(centerPos);
                    return true;
                });
            return ret;
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
                    netTool_ = ToolsModifierControl.toolController.Tools.OfType<NetTool>().FirstOrDefault();
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

            LegacySubTool timedLightsTool = new TimedTrafficLightsTool(this);

            subTools_ = new TinyDictionary<ToolMode, TrafficManagerSubTool> {
                [ToolMode.LaneArrows] = new LaneArrowTool(this),
            };
            legacySubTools_ = new TinyDictionary<ToolMode, LegacySubTool> {
                [ToolMode.ToggleTrafficLight] = new ToggleTrafficLightsTool(this),
                [ToolMode.AddPrioritySigns] = new PrioritySignsTool(this),
                [ToolMode.ManualSwitch] = new ManualTrafficLightsTool(this),
                [ToolMode.TimedLightsAddNode] = timedLightsTool,
                [ToolMode.TimedLightsRemoveNode] = timedLightsTool,
                [ToolMode.TimedLightsSelectNode] = timedLightsTool,
                [ToolMode.TimedLightsShowLights] = timedLightsTool,
                [ToolMode.TimedLightsCopyLights] = timedLightsTool,
                [ToolMode.VehicleRestrictions] = new VehicleRestrictionsTool(this),
                [ToolMode.SpeedLimits] = new SpeedLimitsTool(this),
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
            Log._Debug($"TrafficLightTool: Awake {GetHashCode()}");
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

            // ToolModeChanged does not count timed traffic light submodes as a same tool
            bool toolModeChanged = newToolMode != toolMode_;
            if (IsTimedTrafficLightsSubtool(oldToolMode)
                && IsTimedTrafficLightsSubtool(newToolMode)) {
                toolModeChanged = false;
            }

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
                return;
            }

            SetToolMode_Activate(newToolMode);
            Log._Debug($"SetToolMode: changed old={oldToolMode} new={newToolMode}");
        }

        /// <summary>Resets the tool and calls deactivate on it.</summary>
        private void SetToolMode_DeactivateTool() {
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

        private static bool IsTimedTrafficLightsSubtool(ToolMode a) {
            return a == ToolMode.TimedLightsSelectNode
                   || a == ToolMode.TimedLightsShowLights
                   || a == ToolMode.TimedLightsAddNode
                   || a == ToolMode.TimedLightsRemoveNode
                   || a == ToolMode.TimedLightsCopyLights;
        }

        // Overridden to disable base class behavior
        protected override void OnEnable() {
            if (legacySubTools_ != null) {
                Log._Debug("TrafficManagerTool.OnEnable(): Performing cleanup");
                foreach (KeyValuePair<ToolMode, LegacySubTool> e in legacySubTools_) {
                    e.Value.Cleanup();
                }
            }
        }

        // Overridden to disable base class behavior
        protected override void OnDisable() {
        }

        public override void RenderGeometry(RenderManager.CameraInfo cameraInfo) {
            if (HoveredNodeId != 0) {
                m_toolController.RenderCollidingNotifications(cameraInfo, 0, 0);
            }
        }

        /// <summary>
        /// Renders overlays (node selection, segment selection, etc.)
        /// </summary>
        /// <param name="cameraInfo">The camera to use</param>
        public override void RenderOverlay(RenderManager.CameraInfo cameraInfo) {
            if (!isActiveAndEnabled) {
                return;
            }

            activeLegacySubTool_?.RenderOverlay(cameraInfo);
            activeSubTool_?.RenderOverlay(cameraInfo);

            ToolMode currentMode = GetToolMode();

            // For all _other_ legacy subtools let them render something too
            foreach (KeyValuePair<ToolMode, LegacySubTool> e in legacySubTools_) {
                if (e.Key == currentMode) {
                    continue;
                }

                e.Value.RenderOverlayForOtherTools(cameraInfo);
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
                    InfoManager.InfoMode.Traffic,
                    InfoManager.SubInfoMode.Default);
                UIView.library.Hide("TrafficInfoViewPanel");
            } else if (Input.GetKeyUp(KeyCode.PageUp)) {
                InfoManager.instance.SetCurrentMode(
                    InfoManager.InfoMode.None,
                    InfoManager.SubInfoMode.Default);
            }
            ToolCursor = null;
            bool elementsHovered = DetermineHoveredElements();
            if (activeLegacySubTool_ != null && NetTool != null && elementsHovered) {
                ToolCursor = NetTool.m_upgradeCursor;
            }

            bool primaryMouseClicked = Input.GetMouseButtonDown(0);
            bool secondaryMouseClicked = Input.GetMouseButtonDown(1);

            // check if clicked
            if (!primaryMouseClicked && !secondaryMouseClicked) {
                return;
            }

            // check if mouse is inside panel
            if (ModUI.Instance.GetMenu().containsMouse
#if DEBUG
                || ModUI.Instance.GetDebugMenu().containsMouse
#endif
            ) {
                Log._Debug(
                    "TrafficManagerTool: OnToolUpdate: Menu contains mouse. Ignoring click.");
                return;
            }

            // !elementsHovered ||
            if (activeLegacySubTool_ != null && activeLegacySubTool_.IsCursorInPanel()) {
                Log._Debug("TrafficManagerTool: OnToolUpdate: Subtool contains mouse. Ignoring click.");
                return;
            }

            if (primaryMouseClicked) {
                activeLegacySubTool_?.OnPrimaryClickOverlay();
                activeSubTool_?.OnToolLeftClick();
            }

            if (secondaryMouseClicked) {
                activeLegacySubTool_?.OnSecondaryClickOverlay();
                activeSubTool_?.OnToolRightClick();
            }
        }

        /// <summary>
        /// Immediate mode GUI (IMGUI) handler called every frame for input and IMGUI rendering.
        /// </summary>
        /// <param name="e">Event to handle.</param>
        protected override void OnToolGUI(Event e) {
            try {
                if (!Input.GetMouseButtonDown(0)) {
                    _mouseClickProcessed = false;
                }

                if (Options.nodesOverlay) {
                    DebugGuiDisplaySegments();
                    DebugGuiDisplayNodes();
                }

                if (Options.vehicleOverlay) {
                    DebugGuiDisplayVehicles();
                }

                if (Options.citizenOverlay) {
                    DebugGuiDisplayCitizens();
                }

                if (Options.buildingOverlay) {
                    DebugGuiDisplayBuildings();
                }

                foreach (KeyValuePair<ToolMode, LegacySubTool> en in legacySubTools_) {
                    en.Value.ShowGUIOverlay(en.Key, en.Key != GetToolMode());
                }

                Color guiColor = GUI.color;
                guiColor.a = 1f;
                GUI.color = guiColor;

                if (activeLegacySubTool_ != null) {
                    activeLegacySubTool_.OnToolGUI(e);
                } else if (activeSubTool_ != null) {
                    activeSubTool_.UpdateEveryFrame();
                } else {
                    base.OnToolGUI(e);
                }
            } catch (Exception ex) {
                Log.Error("GUI Error: " + ex);
            }
        }

        public void DrawNodeCircle(RenderManager.CameraInfo cameraInfo,
                                   ushort nodeId,
                                   bool warning = false,
                                   bool alpha = false) {
            DrawNodeCircle(cameraInfo, nodeId, GetToolColor(warning, false), alpha);
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
            Constants.ServiceFactory.NetService.IterateNodeSegments(
                nodeId,
                (ushort segmentId, ref NetSegment segment) => {
                    sumHalfWidth += segment.Info.m_halfWidth;
                    count++;
                    return true;
                });
            return sumHalfWidth / count;
        }

        // TODO: move to UI.Helpers (Highlight)
        public void DrawNodeCircle(RenderManager.CameraInfo cameraInfo,
                                   ushort nodeId,
                                   Color color,
                                   bool alpha = false) {
            float r = CalculateNodeRadius(nodeId);
            Vector3 pos = Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId].m_position;
            DrawOverlayCircle(cameraInfo, color, pos, r * 2, alpha);
        }

        /// <summary>
        /// Draws a half sausage at segment end.
        /// </summary>
        /// <param name="segmentId"></param>
        /// <param name="cut">The lenght of the highlight [0~1] </param>
        /// <param name="bStartNode">Determines the direction of the half sausage.</param>
        // TODO: move to UI.Helpers (Highlight)
        public void DrawCutSegmentEnd(RenderManager.CameraInfo cameraInfo,
                       ushort segmentId,
                       float cut,
                       bool bStartNode,
                       Color color,
                       bool alpha = false) {
            if( segmentId == 0) {
                return;
            }
            ref NetSegment segment = ref Singleton<NetManager>.instance.m_segments.m_buffer[segmentId];
            float width = segment.Info.m_halfWidth;

            NetNode[] nodeBuffer = Singleton<NetManager>.instance.m_nodes.m_buffer;
            bool IsMiddle(ushort nodeId) => (nodeBuffer[nodeId].m_flags & NetNode.Flags.Middle) != 0;

            Bezier3 bezier;
            bezier.a = GetNodePos(segment.m_startNode);
            bezier.d = GetNodePos(segment.m_endNode);

            NetSegment.CalculateMiddlePoints(
                bezier.a,
                segment.m_startDirection,
                bezier.d,
                segment.m_endDirection,
                IsMiddle(segment.m_startNode),
                IsMiddle(segment.m_endNode),
                out bezier.b,
                out bezier.c);

            if (bStartNode) {
                bezier = bezier.Cut(0, cut);
            } else {
                bezier = bezier.Cut(1 - cut, 1);
            }

            Singleton<ToolManager>.instance.m_drawCallData.m_overlayCalls++;
            Singleton<RenderManager>.instance.OverlayEffect.DrawBezier(
                cameraInfo,
                color,
                bezier,
                width * 2f,
                bStartNode ? 0 : width,
                bStartNode ? width : 0,
                -1f,
                1280f,
                false,
                alpha);
        }

        /// <summary>
        /// similar to NetTool.RenderOverlay()
        /// but with additional control over alphaBlend.
        /// </summary>
        // TODO: move to UI.Helpers (Highlight)
        internal static void DrawSegmentOverlay(
            RenderManager.CameraInfo cameraInfo,
            ushort segmentId,
            Color color,
            bool alphaBlend) {
            if (segmentId == 0) {
                return;
            }

            ref NetSegment segment = ref Singleton<NetManager>.instance.m_segments.m_buffer[segmentId];
            float width = segment.Info.m_halfWidth;

            NetNode[] nodeBuffer = Singleton<NetManager>.instance.m_nodes.m_buffer;
            bool IsMiddle(ushort nodeId) => (nodeBuffer[nodeId].m_flags & NetNode.Flags.Middle) != 0;

            Bezier3 bezier;
            bezier.a = GetNodePos(segment.m_startNode);
            bezier.d = GetNodePos(segment.m_endNode);

            NetSegment.CalculateMiddlePoints(
                bezier.a,
                segment.m_startDirection,
                bezier.d,
                segment.m_endDirection,
                IsMiddle(segment.m_startNode),
                IsMiddle(segment.m_endNode),
                out bezier.b,
                out bezier.c);

            Singleton<ToolManager>.instance.m_drawCallData.m_overlayCalls++;
            Singleton<RenderManager>.instance.OverlayEffect.DrawBezier(
                cameraInfo,
                color,
                bezier,
                width * 2f,
                0,
                0,
                -1f,
                1280f,
                false,
                alphaBlend);
        }

        [UsedImplicitly]
        // TODO: move to UI.Helpers (Highlight)
        private static void DrawOverlayCircle(RenderManager.CameraInfo cameraInfo,
                                              Color color,
                                              Vector3 position,
                                              float width,
                                              bool alpha) {
            Singleton<ToolManager>.instance.m_drawCallData.m_overlayCalls++;
            Singleton<RenderManager>.instance.OverlayEffect.DrawCircle(
                cameraInfo,
                color,
                position,
                width,
                position.y - 100f,
                position.y + 100f,
                false,
                alpha);
        }

        // TODO: move to UI.Helpers (Highlight)
        public void DrawStaticSquareOverlayGridTexture(Texture2D texture,
                                                       Vector3 camPos,
                                                       Vector3 gridOrigin,
                                                       float cellSize,
                                                       Vector3 xu,
                                                       Vector3 yu,
                                                       uint x,
                                                       uint y,
                                                       float size) {
            DrawGenericSquareOverlayGridTexture(
                texture,
                camPos,
                gridOrigin,
                cellSize,
                xu,
                yu,
                x,
                y,
                size,
                false);
        }

        [UsedImplicitly]
        // TODO: move to UI.Helpers (Highlight)
        public bool DrawHoverableSquareOverlayGridTexture(Texture2D texture,
                                                          Vector3 camPos,
                                                          Vector3 gridOrigin,
                                                          float cellSize,
                                                          Vector3 xu,
                                                          Vector3 yu,
                                                          uint x,
                                                          uint y,
                                                          float size) {
            return DrawGenericSquareOverlayGridTexture(
                texture,
                camPos,
                gridOrigin,
                cellSize,
                xu,
                yu,
                x,
                y,
                size,
                true);
        }

        // TODO: move to UI.Helpers (Highlight)
        public bool DrawGenericSquareOverlayGridTexture(Texture2D texture,
                                                        Vector3 camPos,
                                                        Vector3 gridOrigin,
                                                        float cellSize,
                                                        Vector3 xu,
                                                        Vector3 yu,
                                                        uint x,
                                                        uint y,
                                                        float size,
                                                        bool canHover) {
            return DrawGenericOverlayGridTexture(
                texture,
                camPos,
                gridOrigin,
                cellSize,
                cellSize,
                xu,
                yu,
                x,
                y,
                size,
                size,
                canHover);
        }

        // TODO: move to UI.Helpers (Highlight)
        public void DrawStaticOverlayGridTexture(Texture2D texture,
                                                 Vector3 camPos,
                                                 Vector3 gridOrigin,
                                                 float cellWidth,
                                                 float cellHeight,
                                                 Vector3 xu,
                                                 Vector3 yu,
                                                 uint x,
                                                 uint y,
                                                 float width,
                                                 float height) {
            DrawGenericOverlayGridTexture(
                texture,
                camPos,
                gridOrigin,
                cellWidth,
                cellHeight,
                xu,
                yu,
                x,
                y,
                width,
                height,
                false);
        }

        [UsedImplicitly]
        // TODO: move to UI.Helpers (Highlight)
        public bool DrawHoverableOverlayGridTexture(Texture2D texture,
                                                    Vector3 camPos,
                                                    Vector3 gridOrigin,
                                                    float cellWidth,
                                                    float cellHeight,
                                                    Vector3 xu,
                                                    Vector3 yu,
                                                    uint x,
                                                    uint y,
                                                    float width,
                                                    float height) {
            return DrawGenericOverlayGridTexture(
                texture,
                camPos,
                gridOrigin,
                cellWidth,
                cellHeight,
                xu,
                yu,
                x,
                y,
                width,
                height,
                true);
        }

        // TODO: move to UI.Helpers (Highlight)
        public bool DrawGenericOverlayGridTexture(Texture2D texture,
                                                  Vector3 camPos,
                                                  Vector3 gridOrigin,
                                                  float cellWidth,
                                                  float cellHeight,
                                                  Vector3 xu,
                                                  Vector3 yu,
                                                  uint x,
                                                  uint y,
                                                  float width,
                                                  float height,
                                                  bool canHover) {
            Vector3 worldPos =
                gridOrigin + (cellWidth * x * xu) +
                (cellHeight * y * yu); // grid position in game coordinates
            return DrawGenericOverlayTexture(texture, camPos, worldPos, width, height, canHover);
        }

        // TODO: move to UI.Helpers (Highlight)
        public void DrawStaticSquareOverlayTexture(Texture2D texture,
                                                   Vector3 camPos,
                                                   Vector3 worldPos,
                                                   float size) {
            DrawGenericOverlayTexture(texture, camPos, worldPos, size, size, false);
        }

        // TODO: move to UI.Helpers (Highlight)
        public bool DrawHoverableSquareOverlayTexture(Texture2D texture,
                                                      Vector3 camPos,
                                                      Vector3 worldPos,
                                                      float size) {
            return DrawGenericOverlayTexture(texture, camPos, worldPos, size, size, true);
        }

        // TODO: move to UI.Helpers (Highlight)
        public bool DrawGenericSquareOverlayTexture(Texture2D texture,
                                                    Vector3 camPos,
                                                    Vector3 worldPos,
                                                    float size,
                                                    bool canHover) {
            return DrawGenericOverlayTexture(texture, camPos, worldPos, size, size, canHover);
        }

        // TODO: move to UI.Helpers (Highlight)
        public void DrawStaticOverlayTexture(Texture2D texture,
                                             Vector3 camPos,
                                             Vector3 worldPos,
                                             float width,
                                             float height) {
            DrawGenericOverlayTexture(texture, camPos, worldPos, width, height, false);
        }

        [UsedImplicitly]
        // TODO: move to UI.Helpers (Highlight)
        public bool DrawHoverableOverlayTexture(Texture2D texture,
                                                Vector3 camPos,
                                                Vector3 worldPos,
                                                float width,
                                                float height) {
            return DrawGenericOverlayTexture(texture, camPos, worldPos, width, height, true);
        }

        // TODO: move to UI.Helpers (Highlight)
        public bool DrawGenericOverlayTexture(Texture2D texture,
                                              Vector3 camPos,
                                              Vector3 worldPos,
                                              float width,
                                              float height,
                                              bool canHover) {
            // Is point in screen?
            if (!GeometryUtil.WorldToScreenPoint(worldPos, out Vector3 screenPos)) {
                return false;
            }

            float zoom = 1.0f / (worldPos - camPos).magnitude * 100f * GetBaseZoom();
            width *= zoom;
            height *= zoom;

            Rect boundingBox = new Rect(
                screenPos.x - (width / 2f),
                screenPos.y - (height / 2f),
                width,
                height);

            Color guiColor = GUI.color;
            bool hovered = false;

            if (canHover) {
                hovered = IsMouseOver(boundingBox);
            }

            guiColor.a = GetHandleAlpha(hovered);

            GUI.color = guiColor;
            GUI.DrawTexture(boundingBox, texture);

            return hovered;
        }

        /// <summary>Shows a tutorial message. Must be called by a Unity thread.</summary>
        /// <param name="localeKey">Tutorial key.</param>
        public static void ShowAdvisor(string localeKey) {
            if (!GlobalConfig.Instance.Main.EnableTutorial) {
                return;
            }

            if (!Translation.Tutorials.HasString(Translation.TUTORIAL_BODY_KEY_PREFIX + localeKey)) {
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

        // Does nothing
        public override void SimulationStep() {
            base.SimulationStep();

            // currentFrame = Singleton<SimulationManager>.instance.m_currentFrameIndex >> 2;
            //
            // string displayToolTipText = tooltipText;
            // if (displayToolTipText != null) {
            //        if (currentFrame <= tooltipStartFrame + 50) {
            //                ShowToolInfo(true, displayToolTipText, (Vector3)tooltipWorldPos);
            //        } else {
            //                //ShowToolInfo(false, tooltipText, (Vector3)tooltipWorldPos);
            //                //ShowToolInfo(false, null, Vector3.zero);
            //                tooltipStartFrame = 0;
            //                tooltipText = null;
            //                tooltipWorldPos = null;
            //        }
            // }
        }

        // public bool DoRayCast(RaycastInput input, out RaycastOutput output) {
        //     return RayCast(input, out output);
        // }

        private static Vector3 prevMousePosition;

        private bool DetermineHoveredElements() {
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

                // find currently hovered segment
                var segmentInput = new RaycastInput(m_mouseRay, m_mouseRayLength) {
                    m_netService = {
                        // find road segments
                        m_itemLayers = ItemClass.Layer.Default | ItemClass.Layer.MetroTunnels,
                        m_service = ItemClass.Service.Road
                    },
                    m_ignoreTerrain = true,
                    m_ignoreSegmentFlags = NetSegment.Flags.None
                };
                // segmentInput.m_ignoreSegmentFlags = NetSegment.Flags.Untouchable;

                if (RayCast(segmentInput, out RaycastOutput segmentOutput)) {
                    HoveredSegmentId = segmentOutput.m_netSegment;
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
                        HoveredSegmentId = segmentOutput.m_netSegment;
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
                            HoveredSegmentId = segmentOutput.m_netSegment;
                        }
                    }
                }

                if(HoveredSegmentId != 0) {
                    HitPos = segmentOutput.m_hitPos;
                }

                if (HoveredNodeId <= 0 && HoveredSegmentId > 0) {
                    // alternative way to get a node hit: check distance to start and end nodes
                    // of the segment
                    ushort startNodeId = Singleton<NetManager>
                                         .instance.m_segments.m_buffer[HoveredSegmentId]
                                         .m_startNode;
                    ushort endNodeId = Singleton<NetManager>
                                       .instance.m_segments.m_buffer[HoveredSegmentId].m_endNode;

                    NetNode[] nodesBuffer = Singleton<NetManager>.instance.m_nodes.m_buffer;
                    float startDist = (segmentOutput.m_hitPos - nodesBuffer[startNodeId]
                                                                .m_position).magnitude;
                    float endDist = (segmentOutput.m_hitPos - nodesBuffer[endNodeId]
                                                              .m_position).magnitude;
                    if (startDist < endDist && startDist < 75f) {
                        HoveredNodeId = startNodeId;
                    } else if (endDist < startDist && endDist < 75f) {
                        HoveredNodeId = endNodeId;
                    }
                }

                if (HoveredNodeId != 0 && HoveredSegmentId != 0) {
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
            NetNode node = NetManager.instance.m_nodes.m_buffer[HoveredNodeId];
            float minDistance = float.MaxValue;
            Constants.ServiceFactory.NetService.IterateNodeSegments(
                HoveredNodeId,
                (ushort segmentId, ref NetSegment segment) =>
                {
                    Vector3 pos = segment.GetClosestPosition(hitPos);
                    float distance = (hitPos - pos).sqrMagnitude;
                    if (distance < minDistance) {
                        minDistance = distance;
                        minSegId = segmentId;
                    }
                    return true;
                });
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
                HitPos,
                NetInfo.LaneType.All,
                VehicleInfo.VehicleType.All,
                out Vector3 pos,
                out uint laneId,
                out int laneIndex,
                out float laneOffset)) {
                return prev_H_Fixed = pos.y;
            }
            return prev_H_Fixed = HitPos.y + 0.5f;
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
            NetLane[] lanesBuffer = Singleton<NetManager>.instance.m_lanes.m_buffer;

            for (int i = 0; i < segmentInfo.m_lanes.Length; ++i) {
                if (curLaneId == 0) {
                    break;
                }

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
                    ", in: {0}, out: {1}, f: {2}, l: {3} km/h, rst: {4}, dir: {5}, fnl: {6}, " +
                    "pos: {7:0.##}, sim: {8} for {9}/{10}",
                    RoutingManager.Instance.CalcInnerSimilarLaneIndex(segmentId, i),
                    RoutingManager.Instance.CalcOuterSimilarLaneIndex(segmentId, i),
                    (NetLane.Flags)lanesBuffer[curLaneId].m_flags,
                    SpeedLimitManager.Instance.GetCustomSpeedLimit(curLaneId),
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

                labelSb.AppendFormat(", nd: {0}", lanesBuffer[curLaneId].m_nodes);
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

                curLaneId = lanesBuffer[curLaneId].m_nextLane;
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
            NetSegment[] segmentsBuffer = netManager.m_segments.m_buffer;

            for (int i = 1; i < NetManager.MAX_SEGMENT_COUNT; ++i) {
                if ((segmentsBuffer[i].m_flags & NetSegment.Flags.Created) ==
                    NetSegment.Flags.None) {
                    // segment is unused
                    continue;
                }

                ItemClass.Service service = segmentsBuffer[i].Info.GetService();
                ItemClass.SubService subService = segmentsBuffer[i].Info.GetSubService();
#if !DEBUG
                if ((netManager.m_segments.m_buffer[i].m_flags & NetSegment.Flags.Untouchable) !=
                    NetSegment.Flags.None) {
                    continue;
                }
#endif
                NetInfo segmentInfo = segmentsBuffer[i].Info;

                Vector3 centerPos = segmentsBuffer[i].m_bounds.center;
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
                labelSb.AppendFormat("Segment {0}", i);
#if DEBUG
                labelSb.AppendFormat(", flags: {0}", segmentsBuffer[i].m_flags);
                labelSb.AppendFormat("\nsvc: {0}, sub: {1}", service, subService);

                uint startVehicles = endMan.GetRegisteredVehicleCount(
                    ref endMan.ExtSegmentEnds[endMan.GetIndex((ushort)i, true)]);

                uint endVehicles = endMan.GetRegisteredVehicleCount(
                    ref endMan.ExtSegmentEnds[endMan.GetIndex((ushort)i, false)]);

                labelSb.AppendFormat( "\nstart veh.: {0}, end veh.: {1}", startVehicles, endVehicles);
#endif
                labelSb.AppendFormat("\nTraffic: {0} %", segmentsBuffer[i].m_trafficDensity);

#if DEBUG
                int fwdSegIndex = trafficMeasurementManager.GetDirIndex(
                    (ushort)i,
                    NetInfo.Direction.Forward);
                int backSegIndex = trafficMeasurementManager.GetDirIndex(
                    (ushort)i,
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
                    segmentsBuffer[i].m_startNode,
                    segmentsBuffer[i].m_endNode);
#endif

                var labelStr = labelSb.ToString();
                Vector2 dim = counterStyle.CalcSize(new GUIContent(labelStr));
                Rect labelRect = new Rect(screenPos.x - (dim.x / 2f), screenPos.y, dim.x, dim.y);

                GUI.Label(labelRect, labelStr, counterStyle);

                if (Options.showLanes) {
                    DebugGuiDisplayLanes(
                        (ushort)i,
                        ref segmentsBuffer[i],
                        ref segmentInfo);
                }
            }
        }

        /// <summary>Displays node ids over nodes.</summary>
        // TODO: Extract into a Debug Tool GUI class
        private void DebugGuiDisplayNodes() {
            var counterStyle = new GUIStyle();
            NetManager netManager = Singleton<NetManager>.instance;

            for (int i = 1; i < NetManager.MAX_NODE_COUNT; ++i) {
                if ((netManager.m_nodes.m_buffer[i].m_flags & NetNode.Flags.Created) ==
                    NetNode.Flags.None) {
                    // node is unused
                    continue;
                }

                Vector3 pos = netManager.m_nodes.m_buffer[i].m_position;
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
                    netManager.m_nodes.m_buffer[i].m_flags,
                    netManager.m_nodes.m_buffer[i].m_lane);
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
            VehicleManager vehicleManager = Singleton<VehicleManager>.instance;

            int startVehicleId = 1;
            int endVehicleId = Constants.ServiceFactory.VehicleService.MaxVehicleCount - 1;
#if DEBUG
            if (DebugSettings.VehicleId != 0) {
                startVehicleId = endVehicleId = DebugSettings.VehicleId;
            }
#endif
            Vehicle[] vehiclesBuffer = Singleton<VehicleManager>.instance.m_vehicles.m_buffer;

            for (int i = startVehicleId; i <= endVehicleId; ++i) {
                if (vehicleManager.m_vehicles.m_buffer[i].m_flags == 0) {
                    // node is unused
                    continue;
                }

                Vector3 vehPos = vehicleManager.m_vehicles.m_buffer[i].GetSmoothPosition((ushort)i);
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
                                     ref vehiclesBuffer[i])];
                // bool startNode = vState.currentStartNode;
                // ushort segmentId = vState.currentSegmentId;

                // Converting magnitudes into game speed float, and then into km/h
                SpeedValue vehSpeed = SpeedValue.FromVelocity(vehicleManager.m_vehicles.m_buffer[i].GetLastFrameVelocity().magnitude);
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
                    ExtCitizenInstanceManager.Instance.GetCitizenId(driverInst.instanceId),
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
            CitizenManager citManager = Singleton<CitizenManager>.instance;
            Citizen[] citizensBuffer = Singleton<CitizenManager>.instance.m_citizens.m_buffer;
            VehicleParked[] parkedVehiclesBuffer = Singleton<VehicleManager>.instance.m_parkedVehicles.m_buffer;
            Vehicle[] vehiclesBuffer = Singleton<VehicleManager>.instance.m_vehicles.m_buffer;

            for (int i = 1; i < CitizenManager.MAX_INSTANCE_COUNT; ++i) {
                if ((citManager.m_instances.m_buffer[i].m_flags &
                     CitizenInstance.Flags.Character) == CitizenInstance.Flags.None) {
                    continue;
                }
#if DEBUG
                if (DebugSwitch.NoValidPathCitizensOverlay.Get()) {
#endif
                    if (citManager.m_instances.m_buffer[i].m_path != 0) {
                        continue;
                    }
#if DEBUG
                }
#endif

                Vector3 pos = citManager.m_instances.m_buffer[i].GetSmoothPosition((ushort)i);
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
                    ExtCitizenInstanceManager.Instance.ExtInstances[i].pathMode !=
                    GlobalConfig.Instance.Debug.ExtPathMode) {
                    continue;
                }
#endif

                var labelSb = new StringBuilder();
                ExtCitizen[] extCitizensBuf = ExtCitizenManager.Instance.ExtCitizens;
                labelSb.AppendFormat(
                    "Inst. {0}, Cit. {1},\nm: {2}, tm: {3}, ltm: {4}, ll: {5}",
                    i,
                    citManager.m_instances.m_buffer[i].m_citizen,
                    ExtCitizenInstanceManager.Instance.ExtInstances[i].pathMode,
                    extCitizensBuf[citManager.m_instances.m_buffer[i].m_citizen].transportMode,
                    extCitizensBuf[citManager.m_instances.m_buffer[i].m_citizen].lastTransportMode,
                    extCitizensBuf[citManager.m_instances.m_buffer[i].m_citizen].lastLocation);

                if (citManager.m_instances.m_buffer[i].m_citizen != 0) {
                    Citizen citizen = citizensBuffer[citManager.m_instances.m_buffer[i].m_citizen];
                    if (citizen.m_parkedVehicle != 0) {
                        labelSb.AppendFormat(
                            "\nparked: {0} dist: {1}",
                            citizen.m_parkedVehicle,
                            (parkedVehiclesBuffer[citizen.m_parkedVehicle].m_position - pos).magnitude);
                    }

                    if (citizen.m_vehicle != 0) {
                        labelSb.AppendFormat(
                            "\nveh: {0} dist: {1}",
                            citizen.m_vehicle,
                            (vehiclesBuffer[citizen.m_vehicle].GetLastFramePosition() - pos).magnitude);
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
            BuildingManager buildingManager = Singleton<BuildingManager>.instance;

            for (int i = 1; i < BuildingManager.MAX_BUILDING_COUNT; ++i) {
                if ((buildingManager.m_buildings.m_buffer[i].m_flags & Building.Flags.Created)
                    == Building.Flags.None) {
                    continue;
                }

                Vector3 pos = buildingManager.m_buildings.m_buffer[i].m_position;
                bool visible = GeometryUtil.WorldToScreenPoint(pos, out Vector3 screenPos);

                if (!visible) {
                    continue;
                }

                Vector3 camPos = Singleton<SimulationManager>.instance.m_simulationView.m_position;
                Vector3 diff = pos - camPos;
                if (diff.magnitude > DEBUG_CLOSE_LOD) {
                    continue; // do not draw if too distant
                }

                float zoom = 150f / diff.magnitude;

                _counterStyle.fontSize = (int)(10f * zoom);
                _counterStyle.normal.textColor = new Color(0f, 1f, 0f);
                // _counterStyle.normal.background = MakeTex(1, 1, new Color(0f, 0f, 0f, 0.4f));

                string labelStr = string.Format(
                    "Building {0}, PDemand: {1}, IncTDem: {2}, OutTDem: {3}",
                    i,
                    ExtBuildingManager.Instance.ExtBuildings[i].parkingSpaceDemand,
                    ExtBuildingManager.Instance.ExtBuildings[i].incomingPublicTransportDemand,
                    ExtBuildingManager.Instance.ExtBuildings[i].outgoingPublicTransportDemand);

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
    }
}