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
    using TrafficManager.State.ConfigData;
    using TrafficManager.UI.MainMenu;
    using TrafficManager.UI.SubTools;
    using TrafficManager.UI.SubTools.SpeedLimits;
    using TrafficManager.Util;
    using UnityEngine;
    using TrafficManager.UI.Helpers;

    [UsedImplicitly]
    public class TrafficManagerTool
        : DefaultTool,
          IObserver<GlobalConfig>
    {
        public GuideHandler Guide;

        private ToolMode toolMode_;
        private NetTool _netTool;


        /// <summary>
        /// Maximum error of HitPos field.
        /// </summary>
        internal const float MAX_HIT_ERROR = 2.5f;

        internal static ushort HoveredNodeId;
        internal static ushort HoveredSegmentId;

        /// <summary>
        /// the hit position of the mouse raycast in meters.
        /// </summary>
        internal static Vector3 HitPos;
        internal Vector3 MousePosition => m_mousePosition; //expose protected member.

        private static bool _mouseClickProcessed;

        public const float DEBUG_CLOSE_LOD = 300f;
        /// <summary>
        /// Square of the distance, where overlays are not rendered
        /// </summary>
        public const float MAX_OVERLAY_DISTANCE_SQR = 450f * 450f;

        private IDictionary<ToolMode, SubTool> subTools_;

        public static ushort SelectedNodeId { get; internal set; }

        public static ushort SelectedSegmentId { get; internal set; }

        public static TransportDemandViewMode CurrentTransportDemandViewMode { get; internal set; }
            = TransportDemandViewMode.Outgoing;

        internal static ExtVehicleType[] InfoSignsToDisplay = {
            ExtVehicleType.PassengerCar, ExtVehicleType.Bicycle, ExtVehicleType.Bus,
            ExtVehicleType.Taxi, ExtVehicleType.Tram, ExtVehicleType.CargoTruck,
            ExtVehicleType.Service, ExtVehicleType.RailVehicle
        };

        private SubTool _activeSubTool;

        private static IDisposable _confDisposable;

        static TrafficManagerTool() { }

        protected override void OnDestroy() {
            Log.Info("TrafficManagerTool.OnDestroy() called");
            base.OnDestroy();
        }

        internal ToolController GetToolController() {
            return m_toolController;
        }

        internal static Rect MoveGUI(Rect rect) {
            // x := main menu x + rect.x
            // y := main menu y + main menu height + rect.y
            // TODO use current size profile
            return new Rect(
                MainMenuPanel.DEFAULT_MENU_X + rect.x,
                MainMenuPanel.DEFAULT_MENU_Y + MainMenuPanel.SIZE_PROFILES[1].MENU_HEIGHT + rect.y,
                rect.width,
                rect.height);
        }

        internal bool IsNodeWithinViewDistance(ushort nodeId) {
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
        [UsedImplicitly]
        internal bool IsSegmentWithinViewDistance(ushort segmentId) {
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

        internal bool IsPosWithinOverlayDistance(Vector3 position) {
            return (position - Singleton<SimulationManager>.instance.m_simulationView.m_position)
                   .sqrMagnitude <= MAX_OVERLAY_DISTANCE_SQR;
        }

        internal static float AdaptWidth(float originalWidth) {
            return originalWidth;
            // return originalWidth * ((float)Screen.width / 1920f);
        }

        internal float GetBaseZoom() {
            return Screen.height / 1200f;
        }

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

        internal NetTool NetTool {
            get {
                if (_netTool == null) {
                    Log._Debug("NetTool field value is null. Searching for instance...");
                    _netTool = ToolsModifierControl.toolController.Tools.OfType<NetTool>().FirstOrDefault();
                }

                return _netTool;
            }
        }

        private static float TransparencyToAlpha(byte transparency) {
            return Mathf.Clamp(100 - transparency, 0f, 100f) / 100f;
        }

        internal void Initialize() {
            Log.Info("TrafficManagerTool: Initialization running now.");
            Guide = new GuideHandler();

            SubTool timedLightsTool = new TimedTrafficLightsTool(this);

            subTools_ = new TinyDictionary<ToolMode, SubTool> {
                [ToolMode.SwitchTrafficLight] = new ToggleTrafficLightsTool(this),
                [ToolMode.AddPrioritySigns] = new PrioritySignsTool(this),
                [ToolMode.ManualSwitch] = new ManualTrafficLightsTool(this),
                [ToolMode.TimedLightsAddNode] = timedLightsTool,
                [ToolMode.TimedLightsRemoveNode] = timedLightsTool,
                [ToolMode.TimedLightsSelectNode] = timedLightsTool,
                [ToolMode.TimedLightsShowLights] = timedLightsTool,
                [ToolMode.TimedLightsCopyLights] = timedLightsTool,
                [ToolMode.VehicleRestrictions] = new VehicleRestrictionsTool(this),
                [ToolMode.SpeedLimits] = new SpeedLimitsTool(this),
                [ToolMode.LaneChange] = new LaneArrowTool(this),
                [ToolMode.LaneConnector] = new LaneConnectorTool(this),
                [ToolMode.JunctionRestrictions] = new JunctionRestrictionsTool(this),
                [ToolMode.ParkingRestrictions] = new ParkingRestrictionsTool(this)
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
            foreach (KeyValuePair<ToolMode, SubTool> e in subTools_) {
                e.Value.Initialize();
            }
        }

        protected override void Awake() {
            Log._Debug($"TrafficLightTool: Awake {GetHashCode()}");
            base.Awake();
        }

        public SubTool GetSubTool(ToolMode mode) {
            if (subTools_.TryGetValue(mode, out SubTool ret)) {
                return ret;
            }

            return null;
        }

        public ToolMode GetToolMode() {
            return toolMode_;
        }

        public void SetToolMode(ToolMode mode) {
            Log._Debug($"SetToolMode: {mode}");

            bool toolModeChanged = (mode != toolMode_);
            ToolMode oldToolMode = toolMode_;
            subTools_.TryGetValue(oldToolMode, out SubTool oldSubTool);
            toolMode_ = mode;

            if (!subTools_.TryGetValue(toolMode_, out _activeSubTool)) {
                _activeSubTool = null;
            }

            bool realToolChange = toolModeChanged;

            if (oldSubTool != null) {
                if (oldToolMode == ToolMode.TimedLightsSelectNode
                    || oldToolMode == ToolMode.TimedLightsShowLights
                    || oldToolMode == ToolMode.TimedLightsAddNode
                    || oldToolMode == ToolMode.TimedLightsRemoveNode
                    || oldToolMode == ToolMode.TimedLightsCopyLights) {
                    // TODO refactor to SubToolMode
                    if (mode != ToolMode.TimedLightsSelectNode
                        && mode != ToolMode.TimedLightsShowLights
                        && mode != ToolMode.TimedLightsAddNode
                        && mode != ToolMode.TimedLightsRemoveNode
                        && mode != ToolMode.TimedLightsCopyLights) {
                        oldSubTool.Cleanup();
                    }
                } else {
                    oldSubTool.Cleanup();
                }
            }

            if (toolModeChanged && _activeSubTool != null) {
                if (oldToolMode == ToolMode.TimedLightsSelectNode
                    || oldToolMode == ToolMode.TimedLightsShowLights
                    || oldToolMode == ToolMode.TimedLightsAddNode
                    || oldToolMode == ToolMode.TimedLightsRemoveNode
                    || oldToolMode == ToolMode.TimedLightsCopyLights) {
                    // TODO refactor to SubToolMode
                    if (mode != ToolMode.TimedLightsSelectNode
                        && mode != ToolMode.TimedLightsShowLights
                        && mode != ToolMode.TimedLightsAddNode
                        && mode != ToolMode.TimedLightsRemoveNode
                        && mode != ToolMode.TimedLightsCopyLights) {
                        _activeSubTool.Cleanup();
                    } else {
                        realToolChange = false;
                    }
                } else {
                    _activeSubTool.Cleanup();
                }
            }

            SelectedNodeId = 0;
            SelectedSegmentId = 0;

            // Log._Debug($"Getting activeSubTool for mode {_toolMode} {subTools.Count}");
            // subTools.TryGetValue((int)_toolMode, out activeSubTool);
            // Log._Debug($"activeSubTool is now {activeSubTool}");

            if (toolModeChanged && _activeSubTool != null) {
                _activeSubTool.OnActivate();

                if (realToolChange) {
                    ShowAdvisor(_activeSubTool.GetTutorialKey());
                    Guide.DeactivateAll();
                }
            }
        }

        // Overridden to disable base class behavior
        protected override void OnEnable() {
            if (subTools_ != null) {
                Log._Debug("TrafficManagerTool.OnEnable(): Performing cleanup");
                foreach (KeyValuePair<ToolMode, SubTool> e in subTools_) {
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
            // Log._Debug($"RenderOverlay");
            // Log._Debug($"RenderOverlay: {_toolMode} {activeSubTool} {this.GetHashCode()}");
            if (!isActiveAndEnabled) {
                return;
            }

            // Log._Debug($"Rendering overlay in {_toolMode}");
            _activeSubTool?.RenderOverlay(cameraInfo);

            foreach (KeyValuePair<ToolMode, SubTool> e in subTools_) {
                if (e.Key == GetToolMode()) {
                    continue;
                }

                e.Value.RenderInfoOverlay(cameraInfo);
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
            if (_activeSubTool != null && NetTool != null && elementsHovered) {
                ToolCursor = NetTool.m_upgradeCursor;
            }

            bool primaryMouseClicked = Input.GetMouseButtonDown(0);
            bool secondaryMouseClicked = Input.GetMouseButtonDown(1);

            // check if clicked
            if (!primaryMouseClicked && !secondaryMouseClicked) {
                return;
            }

            // check if mouse is inside panel
            if (LoadingExtension.BaseUI.GetMenu().containsMouse
#if DEBUG
                || LoadingExtension.BaseUI.GetDebugMenu().containsMouse
#endif
            ) {
                Log._Debug(
                    "TrafficManagerTool: OnToolUpdate: Menu contains mouse. Ignoring click.");
                return;
            }

            // !elementsHovered ||
            if (_activeSubTool != null && _activeSubTool.IsCursorInPanel()) {
                Log._Debug("TrafficManagerTool: OnToolUpdate: Subtool contains mouse. Ignoring click.");

                // Log.Message("inside ui: " + m_toolController.IsInsideUI + " visible: "
                //     + Cursor.visible + " in secondary panel: " + _cursorInSecondaryPanel);
                return;
            }

            // if (HoveredSegmentId == 0 && HoveredNodeId == 0) {
            //        //Log.Message("no hovered segment");
            //        return;
            // }

            if (_activeSubTool != null) {

                if (primaryMouseClicked) {
                    _activeSubTool.OnPrimaryClickOverlay();
                }

                if (secondaryMouseClicked) {
                    _activeSubTool.OnSecondaryClickOverlay();
                }
            }
        }

        protected override void OnToolGUI(Event e) {
            try {
                if (!Input.GetMouseButtonDown(0)) {
                    _mouseClickProcessed = false;
                }

                if (Options.nodesOverlay) {
                    GuiDisplaySegments();
                    GuiDisplayNodes();
                }

                if (Options.vehicleOverlay) {
                    GuiDisplayVehicles();
                }

                if (Options.citizenOverlay) {
                    GuiDisplayCitizens();
                }

                if (Options.buildingOverlay) {
                    GuiDisplayBuildings();
                }

                foreach (KeyValuePair<ToolMode, SubTool> en in subTools_) {
                    en.Value.ShowGUIOverlay(en.Key, en.Key != GetToolMode());
                }

                Color guiColor = GUI.color;
                guiColor.a = 1f;
                GUI.color = guiColor;

                if (_activeSubTool != null) {
                    _activeSubTool.OnToolGUI(e);
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

            float sum_half_width = 0;
            int count = 0;
            Constants.ServiceFactory.NetService.IterateNodeSegments(
                nodeId,
                (ushort segmentId, ref NetSegment segment) => {
                    sum_half_width += segment.Info.m_halfWidth;
                    count++;
                    return true;
                });
            return sum_half_width / count;
        }

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
        private void DrawOverlayCircle(RenderManager.CameraInfo cameraInfo,
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

        public void DrawStaticSquareOverlayTexture(Texture2D texture,
                                                   Vector3 camPos,
                                                   Vector3 worldPos,
                                                   float size) {
            DrawGenericOverlayTexture(texture, camPos, worldPos, size, size, false);
        }

        public bool DrawHoverableSquareOverlayTexture(Texture2D texture,
                                                      Vector3 camPos,
                                                      Vector3 worldPos,
                                                      float size) {
            return DrawGenericOverlayTexture(texture, camPos, worldPos, size, size, true);
        }

        public bool DrawGenericSquareOverlayTexture(Texture2D texture,
                                                    Vector3 camPos,
                                                    Vector3 worldPos,
                                                    float size,
                                                    bool canHover) {
            return DrawGenericOverlayTexture(texture, camPos, worldPos, size, size, canHover);
        }

        public void DrawStaticOverlayTexture(Texture2D texture,
                                             Vector3 camPos,
                                             Vector3 worldPos,
                                             float width,
                                             float height) {
            DrawGenericOverlayTexture(texture, camPos, worldPos, width, height, false);
        }

        [UsedImplicitly]
        public bool DrawHoverableOverlayTexture(Texture2D texture,
                                                Vector3 camPos,
                                                Vector3 worldPos,
                                                float width,
                                                float height) {
            return DrawGenericOverlayTexture(texture, camPos, worldPos, width, height, true);
        }

        public bool DrawGenericOverlayTexture(Texture2D texture,
                                              Vector3 camPos,
                                              Vector3 worldPos,
                                              float width,
                                              float height,
                                              bool canHover) {
            // Is point in screen?
            if (!WorldToScreenPoint(worldPos, out Vector3 screenPos)) {
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

        /// <summary>
        /// Transforms a world point into a screen point
        /// </summary>
        /// <param name="worldPos"></param>
        /// <param name="screenPos"></param>
        /// <returns></returns>
        public bool WorldToScreenPoint(Vector3 worldPos, out Vector3 screenPos) {
            screenPos = Camera.main.WorldToScreenPoint(worldPos);
            screenPos.y = Screen.height - screenPos.y;

            return screenPos.z >= 0;
        }

        /// <summary>
        /// Shows a tutorial message. Must be called by a Unity thread.
        /// </summary>
        /// <param name="localeKey"></param>
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

        public bool DoRayCast(RaycastInput input, out RaycastOutput output) {
            return RayCast(input, out output);
        }

        private static Vector3 prev_mousePosition;
        private bool DetermineHoveredElements() {            
            if(prev_mousePosition == m_mousePosition) {
                // if mouse ray is not changing use cached results.
                // the assumption is that its practically impossible to change mouse ray
                // without changing m_mousePosition.
                return HoveredNodeId != 0 || HoveredSegmentId != 0;
            }

            HoveredSegmentId = 0;
            HoveredNodeId = 0;
            HitPos = m_mousePosition;

            bool mouseRayValid = !UIView.IsInsideUI() && Cursor.visible &&
                                 (_activeSubTool == null || !_activeSubTool.IsCursorInPanel());

            if (mouseRayValid) {
                // find currently hovered node
                var nodeInput = new RaycastInput(m_mouseRay, m_mouseRayLength) {
                    m_netService = {
                        // find road nodes
                        m_itemLayers = ItemClass.Layer.Default | ItemClass.Layer.MetroTunnels,
                        m_service = ItemClass.Service.Road
                    },
                    m_ignoreTerrain = true,
                    m_ignoreNodeFlags = NetNode.Flags.None
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
            if (HitPos.y == prev_H) {
                return prev_H_Fixed;
            }
            prev_H = HitPos.y;

            if (Shortcuts.GetSeg(HoveredSegmentId).GetClosestLanePosition(
                HitPos, NetInfo.LaneType.All, VehicleInfo.VehicleType.All,
                out Vector3 pos, out uint laneID, out int laneIndex, out float laneOffset)) {
                
                return prev_H_Fixed = pos.y;
            }
            return prev_H_Fixed = HitPos.y + 0.5f;
        }

        /// <summary>
        /// Displays lane ids over lanes
        /// </summary>
        private void GuiDisplayLanes(ushort segmentId,
                                     ref NetSegment segment,
                                     ref NetInfo segmentInfo)
        {
            var _counterStyle = new GUIStyle();
            Vector3 centerPos = segment.m_bounds.center;
            bool visible = WorldToScreenPoint(centerPos, out Vector3 screenPos);

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

        /// <summary>
        /// Displays segment ids over segments
        /// </summary>
        private void GuiDisplaySegments() {
            TrafficMeasurementManager trafficMeasurementManager = TrafficMeasurementManager.Instance;
            NetManager netManager = Singleton<NetManager>.instance;
            GUIStyle _counterStyle = new GUIStyle();
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
                bool visible = WorldToScreenPoint(centerPos, out Vector3 screenPos);

                if (!visible) {
                    continue;
                }

                Vector3 camPos = Singleton<SimulationManager>.instance.m_simulationView.m_position;
                Vector3 diff = centerPos - camPos;

                if (diff.magnitude > DEBUG_CLOSE_LOD) {
                    continue; // do not draw if too distant
                }

                float zoom = 1.0f / diff.magnitude * 150f;
                _counterStyle.fontSize = (int)(12f * zoom);
                _counterStyle.normal.textColor = new Color(1f, 0f, 0f);

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
                Vector2 dim = _counterStyle.CalcSize(new GUIContent(labelStr));
                Rect labelRect = new Rect(screenPos.x - (dim.x / 2f), screenPos.y, dim.x, dim.y);

                GUI.Label(labelRect, labelStr, _counterStyle);

                if (Options.showLanes) {
                    GuiDisplayLanes(
                        (ushort)i,
                        ref segmentsBuffer[i],
                        ref segmentInfo);
                }
            }
        }

        /// <summary>
        /// Displays node ids over nodes
        /// </summary>
        private void GuiDisplayNodes() {
            var counterStyle = new GUIStyle();
            NetManager netManager = Singleton<NetManager>.instance;

            for (int i = 1; i < NetManager.MAX_NODE_COUNT; ++i) {
                if ((netManager.m_nodes.m_buffer[i].m_flags & NetNode.Flags.Created) ==
                    NetNode.Flags.None) {
                    // node is unused
                    continue;
                }

                Vector3 pos = netManager.m_nodes.m_buffer[i].m_position;
                bool visible = WorldToScreenPoint(pos, out Vector3 screenPos);

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

        /// <summary>
        /// Displays vehicle ids over vehicles
        /// </summary>
        private void GuiDisplayVehicles() {
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
                bool visible = WorldToScreenPoint(vehPos, out Vector3 screenPos);

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

        /// <summary>
        /// Displays debug data over citizens
        /// </summary>
        private void GuiDisplayCitizens() {
            GUIStyle _counterStyle = new GUIStyle();
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
                bool visible = WorldToScreenPoint(pos, out Vector3 screenPos);

                if (!visible) {
                    continue;
                }

                Vector3 camPos = Singleton<SimulationManager>.instance.m_simulationView.m_position;
                Vector3 diff = pos - camPos;

                if (diff.magnitude > DEBUG_CLOSE_LOD) {
                    continue; // do not draw if too distant
                }

                float zoom = 1.0f / diff.magnitude * 150f;

                _counterStyle.fontSize = (int)(10f * zoom);
                _counterStyle.normal.textColor = new Color(1f, 0f, 1f);
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
                Vector2 dim = _counterStyle.CalcSize(new GUIContent(labelStr));
                Rect labelRect = new Rect(
                    screenPos.x - (dim.x / 2f),
                    screenPos.y - dim.y - 50f,
                    dim.x,
                    dim.y);

                GUI.Box(labelRect, labelStr, _counterStyle);
            }
        }

        private void GuiDisplayBuildings() {
            GUIStyle _counterStyle = new GUIStyle();
            BuildingManager buildingManager = Singleton<BuildingManager>.instance;

            for (int i = 1; i < BuildingManager.MAX_BUILDING_COUNT; ++i) {
                if ((buildingManager.m_buildings.m_buffer[i].m_flags & Building.Flags.Created)
                    == Building.Flags.None) {
                    continue;
                }

                Vector3 pos = buildingManager.m_buildings.m_buffer[i].m_position;
                bool visible = WorldToScreenPoint(pos, out Vector3 screenPos);

                if (!visible) {
                    continue;
                }

                Vector3 camPos = Singleton<SimulationManager>.instance.m_simulationView.m_position;
                Vector3 diff = pos - camPos;
                if (diff.magnitude > DEBUG_CLOSE_LOD) {
                    continue; // do not draw if too distant
                }

                float zoom = 1.0f / diff.magnitude * 150f;

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

        internal static int GetSegmentNumVehicleLanes(ushort segmentId,
                                                      ushort? nodeId,
                                                      out int numDirections,
                                                      VehicleInfo.VehicleType vehicleTypeFilter)
        {
            NetManager netManager = Singleton<NetManager>.instance;

            NetInfo info = netManager.m_segments.m_buffer[segmentId].Info;
            uint curLaneId = netManager.m_segments.m_buffer[segmentId].m_lanes;
            var laneIndex = 0;

            NetInfo.Direction? dir2 = null;
            // NetInfo.Direction? dir3 = null;

            numDirections = 0;
            HashSet<NetInfo.Direction> directions = new HashSet<NetInfo.Direction>();

            if (nodeId != null) {
                NetInfo.Direction? dir = (netManager.m_segments.m_buffer[segmentId].m_startNode == nodeId)
                                             ? NetInfo.Direction.Backward
                                             : NetInfo.Direction.Forward;
                dir2 =
                    ((netManager.m_segments.m_buffer[segmentId].m_flags &
                      NetSegment.Flags.Invert) == NetSegment.Flags.None)
                        ? dir
                        : NetInfo.InvertDirection((NetInfo.Direction)dir);

                // dir3 = TrafficPriorityManager.IsLeftHandDrive()
                //      ? NetInfo.InvertDirection((NetInfo.Direction)dir2) : dir2;
           }

            var numLanes = 0;

            while (laneIndex < info.m_lanes.Length && curLaneId != 0u) {
                if (((info.m_lanes[laneIndex].m_laneType &
                      (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) != NetInfo.LaneType.None
                     && (info.m_lanes[laneIndex].m_vehicleType & vehicleTypeFilter) != VehicleInfo.VehicleType.None)
                    && (dir2 == null || info.m_lanes[laneIndex].m_finalDirection == dir2))
                {
                    if (!directions.Contains(info.m_lanes[laneIndex].m_finalDirection)) {
                        directions.Add(info.m_lanes[laneIndex].m_finalDirection);
                        ++numDirections;
                    }

                    numLanes++;
                }

                curLaneId = netManager.m_lanes.m_buffer[curLaneId].m_nextLane;
                laneIndex++;
            }

            return numLanes;
        }

        internal static void CalculateSegmentCenterByDir(
            ushort segmentId,
            Dictionary<NetInfo.Direction, Vector3> segmentCenterByDir)
        {
            segmentCenterByDir.Clear();
            NetManager netManager = Singleton<NetManager>.instance;

            NetInfo segmentInfo = netManager.m_segments.m_buffer[segmentId].Info;
            uint curLaneId = netManager.m_segments.m_buffer[segmentId].m_lanes;
            var numCentersByDir =
                new Dictionary<NetInfo.Direction, int>();
            uint laneIndex = 0;

            while (laneIndex < segmentInfo.m_lanes.Length && curLaneId != 0u) {
                if ((segmentInfo.m_lanes[laneIndex].m_laneType &
                     (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) == NetInfo.LaneType.None) {
                    goto nextIter;
                }

                NetInfo.Direction dir = segmentInfo.m_lanes[laneIndex].m_finalDirection;
                Vector3 bezierCenter =
                    netManager.m_lanes.m_buffer[curLaneId].m_bezier.Position(0.5f);

                if (!segmentCenterByDir.ContainsKey(dir)) {
                    segmentCenterByDir[dir] = bezierCenter;
                    numCentersByDir[dir] = 1;
                } else {
                    segmentCenterByDir[dir] += bezierCenter;
                    numCentersByDir[dir]++;
                }

                nextIter:

                curLaneId = netManager.m_lanes.m_buffer[curLaneId].m_nextLane;
                laneIndex++;
            }

            foreach (KeyValuePair<NetInfo.Direction, int> e in numCentersByDir) {
                segmentCenterByDir[e.Key] /= (float)e.Value;
            }
        }

        /// <summary>
        /// Creates a texture width x height, filled with color
        /// </summary>
        /// <param name="width">Size</param>
        /// <param name="height">Size</param>
        /// <param name="col">Fill color</param>
        /// <returns>Texture 2D</returns>
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

        /// <summary>
        /// Creates new texture with changed alpha transparency for every pixel
        /// </summary>
        /// <param name="tex">Copy from</param>
        /// <param name="alpha">New alpha</param>
        /// <returns>New texture</returns>
        public static Texture2D AdjustAlpha(Texture2D tex, float alpha) {
            Color[] texColors = tex.GetPixels();
            Color[] retPixels = new Color[texColors.Length];

            for (int i = 0; i < texColors.Length; ++i) {
                retPixels[i] = new Color(
                    texColors[i].r,
                    texColors[i].g,
                    texColors[i].b,
                    texColors[i].a * alpha);
            }

            Texture2D ret = new Texture2D(tex.width, tex.height, TextureFormat.ARGB32, false);

            ret.SetPixels(retPixels);
            ret.Apply();

            return ret;
        }

        internal static bool IsMouseOver(Rect boundingBox) {
            return boundingBox.Contains(Event.current.mousePosition);
        }


        /// <summary>
        /// Sometimes (eg when clicking overlay sprites - TODO why?) this method should
        /// be used instead of Input.GetMouseButtonDown(0)
        /// </summary>
        internal bool CheckClicked() {
            if (Input.GetMouseButtonDown(0) && !_mouseClickProcessed) {
                _mouseClickProcessed = true;
                return true;
            }

            return false;
        }

        /// <summary>Displays modal popup with an error</summary>
        /// <param name="text">The localized message</param>
        public void ShowError(string text) {
            if (text == null) {
                return;
            }

            UIView.library
                  .ShowModal<ExceptionPanel>("ExceptionPanel")
                  .SetMessage("Info", text, false);
        }
    }
}