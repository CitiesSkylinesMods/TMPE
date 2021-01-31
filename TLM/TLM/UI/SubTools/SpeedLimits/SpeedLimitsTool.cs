namespace TrafficManager.UI.SubTools.SpeedLimits {
    using ColossalFramework;
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using GenericGameBridge.Service;
    using System;
    using System.Collections.Generic;
    using TrafficManager.API.Traffic.Data;
    using TrafficManager.Manager.Impl;
    using TrafficManager.State;
    using TrafficManager.Traffic;
    using TrafficManager.UI.Helpers;
    using TrafficManager.UI.SubTools.PrioritySigns;
    using TrafficManager.UI.Textures;
    using TrafficManager.Util;
    using TrafficManager.Util.Caching;
    using UnityEngine;
    using static TrafficManager.Util.Shortcuts;

    public class SpeedLimitsTool : LegacySubTool {
        public const int
            BREAK_PALETTE_COLUMN_KMPH = 8; // palette shows N in a row, then break and another row

        public const int
            BREAK_PALETTE_COLUMN_MPH = 10; // palette shows M in a row, then break and another row

        private const ushort LOWER_KMPH = 10;
        public const ushort UPPER_KMPH = 140;
        public const ushort KMPH_STEP = 10;

        private const ushort LOWER_MPH = 5;
        public const ushort UPPER_MPH = 90;
        public const ushort MPH_STEP = 5;

        /// <summary>Visible sign size, slightly reduced from 100 to accomodate another column for MPH</summary>
        private const int GUI_SPEED_SIGN_SIZE = 80;
        private readonly float speedLimitSignSize = 70f;

        private bool cursorInSecondaryPanel;

        /// <summary>Currently selected speed limit on the limits palette
        /// negative = invalid
        /// 0 = no limit
        /// null = default
        /// </summary>
        private SpeedValue? currentPaletteSpeedLimit = new SpeedValue(-1f);

        private readonly Dictionary<ushort, Dictionary<NetInfo.Direction, Vector3>> segmentCenterByDir =
            new Dictionary<ushort, Dictionary<NetInfo.Direction, Vector3>>();

        private Rect paletteWindowRect =
            TrafficManagerTool.GetDefaultScreenPositionForRect(new Rect(0, 0, 10 * (GUI_SPEED_SIGN_SIZE + 5), 150));

        private Rect defaultsWindowRect = TrafficManagerTool.GetDefaultScreenPositionForRect(new Rect(0, 80, 50, 50));

        /// <summary>
        /// Stores potentially visible segment ids while the camera did not move
        /// </summary>
        private GenericArrayCache<ushort> CachedVisibleSegmentIds { get; }

        /// <summary>
        /// Stores last cached camera position in <see cref="CachedVisibleSegmentIds"/>
        /// </summary>
        private CameraTransformValue LastCachedCamera { get; set; }

        private bool defaultsWindowVisible;
        private int currentInfoIndex = -1;
        private SpeedValue currentSpeedLimit = new SpeedValue(-1f);

        private Texture2D RoadTexture {
            get {
                if (roadTexture == null) {
                    roadTexture = new Texture2D(GUI_SPEED_SIGN_SIZE, GUI_SPEED_SIGN_SIZE);
                }

                return roadTexture;
            }
        }

        private Texture2D roadTexture;
        private bool showLimitsPerLane_;

        private bool ShowLimitsPerLane => showLimitsPerLane_ ^ ControlIsPressed;

        private bool multiSegmentMode_;
        private bool MultiSegmentMode => multiSegmentMode_ ^ ShiftIsPressed;

        private struct RenderData {
            internal ushort SegmentId;
            internal uint LaneId;
            internal byte LaneIndex;
            internal int SortedLaneIndex;
            internal NetInfo.Lane LaneInfo;
            internal NetInfo.Direction FinalDirection;
        }
        private RenderData renderData_;

        public SpeedLimitsTool(TrafficManagerTool mainTool)
            : base(mainTool) {
            CachedVisibleSegmentIds = new GenericArrayCache<ushort>(NetManager.MAX_SEGMENT_COUNT);
            LastCachedCamera = new CameraTransformValue();
        }

        internal static void SetSpeedLimit(LanePos lane, SpeedValue? speed) {
            ushort segmentId = lane.laneId.ToLane().m_segment;
            SpeedLimitManager.Instance.SetSpeedLimit(
                segmentId: segmentId,
                laneIndex: lane.laneIndex,
                laneInfo: segmentId.ToSegment().Info.m_lanes[lane.laneIndex],
                laneId: lane.laneId,
                speedLimit: speed?.GameUnits);
        }

        public override bool IsCursorInPanel() {
            return base.IsCursorInPanel() || cursorInSecondaryPanel;
        }

        public override void OnActivate() {
            base.OnActivate();
            LastCachedCamera = new CameraTransformValue();
        }

        public override void OnPrimaryClickOverlay() { }

        public override void OnSecondaryClickOverlay() {
            MainTool.SetToolMode(ToolMode.None);
        }

        public override void OnToolGUI(Event e) {
            base.OnToolGUI(e);

            string unitTitle = string.Format(
                " ({0})",
                GlobalConfig.Instance.Main.DisplaySpeedLimitsMph
                    ? Translation.SpeedLimits.Get("Miles per hour")
                    : Translation.SpeedLimits.Get("Kilometers per hour"));

            paletteWindowRect.width = GlobalConfig.Instance.Main.DisplaySpeedLimitsMph
                                          ? 10 * (GUI_SPEED_SIGN_SIZE + 5)
                                          : 8 * (GUI_SPEED_SIGN_SIZE + 5);

            paletteWindowRect = GUILayout.Window(
                id: 254,
                screenRect: paletteWindowRect,
                func: GuiSpeedLimitsWindow,
                text: Translation.Menu.Get("Tooltip:Speed limits") + unitTitle,
                style: WindowStyle);

            if (defaultsWindowVisible) {
                defaultsWindowRect = GUILayout.Window(
                    id: 258,
                    screenRect: defaultsWindowRect,
                    func: GuiDefaultsWindow,
                    text: Translation.SpeedLimits.Get("Window.Title:Default speed limits"),
                    style: WindowStyle);
            }

            cursorInSecondaryPanel = paletteWindowRect.Contains(Event.current.mousePosition)
                                     || (defaultsWindowVisible
                                         && defaultsWindowRect.Contains(
                                             Event.current.mousePosition));

            // overlayHandleHovered = false;
            // ShowSigns(false);
        }

        private static NetLane[] laneBuffer => NetManager.instance.m_lanes.m_buffer;

        private void RenderLaneOverlay(RenderManager.CameraInfo cameraInfo, uint laneId) {
            var marker = new SegmentLaneMarker(laneBuffer[laneId].m_bezier);
            bool pressed = Input.GetMouseButton(0);
            Color color = MainTool.GetToolColor(pressed, false);
            if (!ShowLimitsPerLane) {
                marker.Size = 3f; // lump the lanes together.
            }
            marker.RenderOverlay(cameraInfo, color, pressed);
        }

        /// <summary>
        /// Renders all lanes with the given <paramref name="finalDirection"/>
        /// if NetInfo.Direction.None, all lanes are rendered.
        /// </summary>
        private int RenderSegmentSideOverlay(
            RenderManager.CameraInfo cameraInfo,
            ushort segmentId,
            NetInfo.Direction finalDirection = NetInfo.Direction.None) {
            int count = 0;
            bool pressed = Input.GetMouseButton(0);
            Color color = MainTool.GetToolColor(pressed, false);
            netService.IterateSegmentLanes(
                segmentId,
                (uint laneId,
                ref NetLane lane,
                NetInfo.Lane laneInfo,
                ushort _,
                ref NetSegment segment,
                byte laneIndex) => {
                    bool render = (laneInfo.m_laneType & SpeedLimitManager.LANE_TYPES) != 0;
                    render &= (laneInfo.m_vehicleType & SpeedLimitManager.VEHICLE_TYPES) != 0;
                    render &= laneInfo.m_finalDirection == finalDirection || finalDirection == NetInfo.Direction.None;
                    if (render) {
                        RenderLaneOverlay(cameraInfo, laneId);
                        count++;
                    }
                    return true;
                });
            return count;
        }

        private void RenderLanes(RenderManager.CameraInfo cameraInfo) {
            if (!MultiSegmentMode) {
                RenderLaneOverlay(cameraInfo, renderData_.LaneId);
            } else if (RoundaboutMassEdit.Instance.TraverseLoop(renderData_.SegmentId, out var segmentList)) {
                var lanes = FollowRoundaboutLane(segmentList, renderData_.SegmentId, renderData_.SortedLaneIndex);
                foreach (var lane in lanes)
                    RenderLaneOverlay(cameraInfo, lane.laneId);
            } else {
                bool LaneVisitorFun(SegmentLaneTraverser.SegmentLaneVisitData data) {
                    if (data.SortedLaneIndex == renderData_.SortedLaneIndex) {
                        RenderLaneOverlay(cameraInfo, data.CurLanePos.laneId);
                    }
                    return true;
                }
                {
                    SegmentLaneTraverser.Traverse(
                        renderData_.SegmentId,
                        SegmentTraverser.TraverseDirection.AnyDirection,
                        SegmentTraverser.TraverseSide.AnySide,
                        SegmentLaneTraverser.LaneStopCriterion.LaneCount,
                        SegmentTraverser.SegmentStopCriterion.Junction,
                        SpeedLimitManager.LANE_TYPES,
                        SpeedLimitManager.VEHICLE_TYPES,
                        LaneVisitorFun);
                }
            }
        }

        /// <summary>
        /// iterates through the given roundabout <paramref name="segmentList"/> returning an enumeration
        /// of all lanes with a matching <paramref name="sortedLaneIndex"/> based on <paramref name="segmentId0"/>
        /// </summary>
        /// <param name="segmentList">input list of roundabout segments (must be oneway, and in the same direction).</param>
        /// <param name="segmentId0">The segment to match lane agaisnt</param>
        /// <param name="sortedLaneIndex"></param>
        private IEnumerable<LanePos> FollowRoundaboutLane(List<ushort> segmentList, ushort segmentId0, int sortedLaneIndex) {
            bool invert0 = segmentId0.ToSegment().m_flags.IsFlagSet(NetSegment.Flags.Invert);
            int count0 = netService.GetSortedLanes(
               segmentId: segmentId0,
               segment: ref segmentId0.ToSegment(),
               startNode: null,
               laneTypeFilter: SpeedLimitManager.LANE_TYPES,
               vehicleTypeFilter: SpeedLimitManager.VEHICLE_TYPES,
               sort: false).Count;
            foreach (ushort segmentId in segmentList) {
                bool invert = segmentId.ToSegment().m_flags.IsFlagSet(NetSegment.Flags.Invert);
                var lanes = netService.GetSortedLanes(
                    segmentId: segmentId,
                    segment: ref segmentId.ToSegment(),
                    startNode: null,
                    laneTypeFilter: SpeedLimitManager.LANE_TYPES,
                    vehicleTypeFilter: SpeedLimitManager.VEHICLE_TYPES,
                    reverse: invert != invert0,
                    sort: true);
                int index = sortedLaneIndex;

                // if lane count does not match, assume segments are connected from outer side of the roundabout.
                if (invert0) {
                    int diff = lanes.Count - count0;
                    index += diff;
                }
                if (0 <= index && index < lanes.Count) {
                    yield return lanes[index];
                }
            } // foreach
        }

        private void RenderSegmentsSide(RenderManager.CameraInfo cameraInfo) {
            if (!MultiSegmentMode) {
                RenderSegmentSideOverlay(cameraInfo, renderData_.SegmentId, renderData_.FinalDirection);
            } else if (RoundaboutMassEdit.Instance.TraverseLoop(renderData_.SegmentId, out var segmentList)) {
                foreach (ushort segmentId in segmentList)
                    RenderSegmentSideOverlay(cameraInfo, segmentId);
            } else {
                SegmentTraverser.Traverse(
                    renderData_.SegmentId,
                    SegmentTraverser.TraverseDirection.AnyDirection,
                    SegmentTraverser.TraverseSide.AnySide,
                    SegmentTraverser.SegmentStopCriterion.Junction,
                    data => {
                        NetInfo.Direction finalDirection = renderData_.FinalDirection;
                        if (data.IsReversed(renderData_.SegmentId)) {
                            finalDirection = NetInfo.InvertDirection(finalDirection);
                        }
                        RenderSegmentSideOverlay(cameraInfo, data.CurSeg.segmentId, finalDirection);
                        return true;
                    });
            }
        }

        public override void RenderOverlay(RenderManager.CameraInfo cameraInfo) {
            if (renderData_.SegmentId == 0) {
                return;
            }

            if (ShowLimitsPerLane) {
                RenderLanes(cameraInfo);
            } else {
                RenderSegmentsSide(cameraInfo);
            }
        }

        public override void ShowGUIOverlay(ToolMode toolMode, bool viewOnly) {
            if (viewOnly && !Options.speedLimitsOverlay && !MassEditOverlay.IsActive) {
                return;
            }

            ShowSigns(viewOnly);
        }

        public override void Cleanup() {
            segmentCenterByDir.Clear();
            CachedVisibleSegmentIds.Clear();
            currentInfoIndex = -1;
            currentSpeedLimit = new SpeedValue(-1f);
        }

        private void ShowSigns(bool viewOnly) {
            NetManager netManager = Singleton<NetManager>.instance;
            SpeedLimitManager speedLimitManager = SpeedLimitManager.Instance;

            var currentCamera = new CameraTransformValue(Camera.main);
            Transform currentCameraTransform = Camera.main.transform;
            Vector3 camPos = currentCameraTransform.position;

            if (!LastCachedCamera.Equals(currentCamera)) {
                // cache visible segments
                LastCachedCamera = currentCamera;
                CachedVisibleSegmentIds.Clear();

                for (uint segmentId = 1; segmentId < NetManager.MAX_SEGMENT_COUNT; ++segmentId) {
                    if (!Constants.ServiceFactory.NetService.IsSegmentValid((ushort)segmentId)) {
                        continue;
                    }

                    // if ((netManager.m_segments.m_buffer[segmentId].m_flags &
                    // NetSegment.Flags.Untouchable) != NetSegment.Flags.None) continue;
                    Vector3 distToCamera = netManager.m_segments.m_buffer[segmentId].m_bounds.center - camPos;
                    if (distToCamera.sqrMagnitude > TrafficManagerTool.MAX_OVERLAY_DISTANCE_SQR) {
                        continue; // do not draw if too distant
                    }

                    bool visible = GeometryUtil.WorldToScreenPoint(
                        netManager.m_segments.m_buffer[segmentId].m_bounds.center,
                        out Vector3 _);

                    if (!visible) {
                        continue;
                    }

                    if (!speedLimitManager.MayHaveCustomSpeedLimits(
                            (ushort)segmentId,
                            ref netManager.m_segments.m_buffer[segmentId])) {
                        continue;
                    }

                    CachedVisibleSegmentIds.Add((ushort)segmentId);
                } // end for all segments
            }

            bool hover = false;
            for (int segmentIdIndex = CachedVisibleSegmentIds.Size - 1;
                 segmentIdIndex >= 0;
                 segmentIdIndex--) {
                ushort segmentId = CachedVisibleSegmentIds.Values[segmentIdIndex];
                // draw speed limits
                if ((MainTool.GetToolMode() == ToolMode.VehicleRestrictions) &&
                    (segmentId == SelectedSegmentId)) {
                    continue;
                }

                // no speed limit overlay on selected segment when in vehicle restrictions mode
                hover |= DrawSpeedLimitHandles(
                    segmentId,
                    ref netManager.m_segments.m_buffer[segmentId],
                    viewOnly,
                    ref camPos);
            }
            if (!hover) {
                renderData_.SegmentId = 0;
            }
        }

        /// <summary>
        /// The window for setting the defaullt speeds per road type
        /// </summary>
        /// <param name="num"></param>
        private void GuiDefaultsWindow(int num) {
            List<NetInfo> mainNetInfos = SpeedLimitManager.Instance.GetCustomizableNetInfos();

            if ((mainNetInfos == null) || (mainNetInfos.Count <= 0)) {
                Log._Debug($"mainNetInfos={mainNetInfos?.Count}");
                DragWindow(ref defaultsWindowRect);
                return;
            }

            bool updateRoadTex = false;

            if ((currentInfoIndex < 0) || (currentInfoIndex >= mainNetInfos.Count)) {
                currentInfoIndex = 0;
                updateRoadTex = true;
                Log._Debug($"set currentInfoIndex to 0");
            }

            NetInfo info = mainNetInfos[currentInfoIndex];

            if (updateRoadTex) {
                UpdateRoadTex(info);
            }

            if (currentSpeedLimit.GameUnits < 0f) {
                currentSpeedLimit = new SpeedValue(
                    SpeedLimitManager.Instance.GetCustomNetInfoSpeedLimit(info));
                Log._Debug($"set currentSpeedLimit to {currentSpeedLimit}");
            }

            // Log._Debug($"currentInfoIndex={currentInfoIndex} currentSpeedLimitIndex={currentSpeedLimitIndex}");
            // Road type label
            GUILayout.BeginVertical();
            GUILayout.Space(10);
            GUILayout.Label(Translation.SpeedLimits.Get("Defaults.Label:Road type") + ":");
            GUILayout.EndVertical();

            // switch between NetInfos
            GUILayout.BeginHorizontal();

            GUILayout.BeginVertical();
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("←", GUILayout.Width(50))) {
                currentInfoIndex =
                    ((currentInfoIndex + mainNetInfos.Count) - 1) % mainNetInfos.Count;
                info = mainNetInfos[currentInfoIndex];
                currentSpeedLimit = new SpeedValue(
                    SpeedLimitManager.Instance.GetCustomNetInfoSpeedLimit(info));
                UpdateRoadTex(info);
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndVertical();

            GUILayout.FlexibleSpace();
            GUILayout.BeginVertical();
            GUILayout.FlexibleSpace();

            // NetInfo thumbnail
            GUILayout.Box(RoadTexture, GUILayout.Height(GUI_SPEED_SIGN_SIZE));
            GUILayout.FlexibleSpace();

            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();

            GUILayout.BeginVertical();
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("→", GUILayout.Width(50))) {
                currentInfoIndex = (currentInfoIndex + 1) % mainNetInfos.Count;
                info = mainNetInfos[currentInfoIndex];
                currentSpeedLimit = new SpeedValue(
                    SpeedLimitManager.Instance.GetCustomNetInfoSpeedLimit(info));
                UpdateRoadTex(info);
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();

            var centeredTextStyle = new GUIStyle("label") { alignment = TextAnchor.MiddleCenter };

            // NetInfo name
            GUILayout.Label(info.name, centeredTextStyle);

            // Default speed limit label
            GUILayout.BeginVertical();
            GUILayout.Space(10);
            GUILayout.Label(Translation.SpeedLimits.Get("Label:Default speed limit") + ":");
            GUILayout.EndVertical();

            // switch between speed limits
            GUILayout.BeginHorizontal();

            GUILayout.BeginVertical();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("←", GUILayout.Width(50))) {
                // currentSpeedLimit = (currentSpeedLimitIndex +
                //     SpeedLimitManager.Instance.AvailableSpeedLimits.Count - 1)
                //     % SpeedLimitManager.Instance.AvailableSpeedLimits.Count;
                currentSpeedLimit = GetPrevious(currentSpeedLimit);
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndVertical();

            GUILayout.FlexibleSpace();

            GUILayout.BeginVertical();
            GUILayout.FlexibleSpace();

            // speed limit sign
            GUILayout.Box(SpeedLimitTextures.GetSpeedLimitTexture(currentSpeedLimit),
                          GUILayout.Width(GUI_SPEED_SIGN_SIZE),
                          GUILayout.Height(GUI_SPEED_SIGN_SIZE));
            GUILayout.Label(GlobalConfig.Instance.Main.DisplaySpeedLimitsMph
                                ? Translation.SpeedLimits.Get("Miles per hour")
                                : Translation.SpeedLimits.Get("Kilometers per hour"));

            GUILayout.FlexibleSpace();
            GUILayout.EndVertical();

            GUILayout.FlexibleSpace();

            GUILayout.BeginVertical();
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("→", GUILayout.Width(50))) {
                // currentSpeedLimitIndex = (currentSpeedLimitIndex + 1) %
                //     SpeedLimitManager.Instance.AvailableSpeedLimits.Count;
                currentSpeedLimit = GetNext(currentSpeedLimit);
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();

            // Save & Apply
            GUILayout.BeginVertical();
            GUILayout.Space(10);

            GUILayout.BeginHorizontal();

            // Close button. TODO: Make more visible or obey 'Esc' pressed or something
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("X", GUILayout.Width(80))) {
                defaultsWindowVisible = false;
            }

            GUILayout.FlexibleSpace();

            if (GUILayout.Button(Translation.SpeedLimits.Get("Button:Save"),
                                 GUILayout.Width(70))) {
                SpeedLimitManager.Instance.FixCurrentSpeedLimits(info);
                SpeedLimitManager.Instance.SetCustomNetInfoSpeedLimit(info, currentSpeedLimit.GameUnits);
            }

            GUILayout.FlexibleSpace();

            if (GUILayout.Button(
                Translation.SpeedLimits.Get("Button:Save & Apply"),
                GUILayout.Width(160))) {
                SpeedLimitManager.Instance.SetCustomNetInfoSpeedLimit(info, currentSpeedLimit.GameUnits);
                SpeedLimitManager.Instance.ClearCurrentSpeedLimits(info);
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();

            DragWindow(ref defaultsWindowRect);
        }

        private void UpdateRoadTex(NetInfo info) {
            if (info != null) {
                if ((info.m_Atlas != null) && (info.m_Atlas.material != null) &&
                    (info.m_Atlas.material.mainTexture != null) &&
                    info.m_Atlas.material.mainTexture is Texture2D mainTex) {
                    UITextureAtlas.SpriteInfo spriteInfo = info.m_Atlas[info.m_Thumbnail];

                    if ((spriteInfo != null) && (spriteInfo.texture != null) &&
                        (spriteInfo.texture.width > 0) && (spriteInfo.texture.height > 0)) {
                        try {
                            roadTexture = new Texture2D(
                                spriteInfo.texture.width,
                                spriteInfo.texture.height,
                                TextureFormat.ARGB32,
                                false);

                            roadTexture.SetPixels(
                                0,
                                0,
                                roadTexture.width,
                                roadTexture.height,
                                mainTex.GetPixels(
                                    (int)(spriteInfo.region.x * mainTex.width),
                                    (int)(spriteInfo.region.y * mainTex.height),
                                    (int)(spriteInfo.region.width * mainTex.width),
                                    (int)(spriteInfo.region.height * mainTex.height)));

                            roadTexture.Apply();
                            return;
                        }
                        catch (Exception e) {
                            Log.Warning(
                                $"Could not get texture from NetInfo {info.name}: {e.ToString()}");
                        }
                    }
                }
            }

            // fallback to "noimage" texture
            roadTexture = null;
        }

        /// <summary>
        /// The window for selecting and applying a speed limit
        /// </summary>
        /// <param name="num"></param>
        private void GuiSpeedLimitsWindow(int num) {
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            Color oldColor = GUI.color;
            List<SpeedValue> allSpeedLimits = EnumerateSpeedLimits(SpeedUnit.CurrentlyConfigured);
            allSpeedLimits.Add(new SpeedValue(0)); // add last item: no limit

            bool showMph = GlobalConfig.Instance.Main.DisplaySpeedLimitsMph;
            var column = 0u; // break palette to a new line at breakColumn
            int breakColumn = showMph ? BREAK_PALETTE_COLUMN_MPH : BREAK_PALETTE_COLUMN_KMPH;

            foreach (SpeedValue speedLimit in allSpeedLimits) {
                // Highlight palette item if it is very close to its float speed
                if (currentPaletteSpeedLimit != null &&
                    FloatUtil.NearlyEqual(
                        (float)currentPaletteSpeedLimit?.GameUnits,
                        speedLimit.GameUnits)) {
                    GUI.color = Color.gray;
                }

                GuiSpeedLimitsWindow_AddButton(showMph, speedLimit);
                GUI.color = oldColor;

                // TODO: This can be calculated from SpeedLimit MPH or KMPH limit constants
                column++;
                if (column % breakColumn == 0) {
                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                }
            }
            {
                if (currentPaletteSpeedLimit == null) {
                    GUI.color = Color.gray;
                }
                GuiSpeedLimitsWindow_AddClearButton();
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            //---------------------
            // UI buttons row
            //---------------------
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (GUILayout.Button(Translation.SpeedLimits.Get("Window.Title:Default speed limits"),
                                 GUILayout.Width(200))) {
                TrafficManagerTool.ShowAdvisor(this.GetType().Name + "_Defaults");
                defaultsWindowVisible = true;
            }

            GUILayout.FlexibleSpace();

            bool multiSegmentModeToggled = MultiSegmentMode != GUILayout.Toggle(
                MultiSegmentMode,
                Translation.SpeedLimits.Get("Checkbox:Apply to entire road") + " [shift]");
            if (multiSegmentModeToggled) {
                multiSegmentMode_ = !multiSegmentMode_;
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            //---------------------
            // Checkboxes row
            //---------------------
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            bool limitsPerLaneToggled = ShowLimitsPerLane != GUILayout.Toggle(
                ShowLimitsPerLane,
                Translation.SpeedLimits.Get("Checkbox:Show lane-wise speed limits") + " [ctrl]");
            if (limitsPerLaneToggled) {
                showLimitsPerLane_ = !showLimitsPerLane_;
            }

            GUILayout.FlexibleSpace();

            // Display MPH checkbox, if ticked will save global config
            bool displayMph = GlobalConfig.Instance.Main.DisplaySpeedLimitsMph;
            displayMph = GUILayout.Toggle(
                displayMph,
                Translation.SpeedLimits.Get("Checkbox:Display speed limits mph"));

            if (GlobalConfig.Instance.Main.DisplaySpeedLimitsMph != displayMph) {
                OptionsGeneralTab.SetDisplayInMph(displayMph);
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            DragWindow(ref paletteWindowRect);
        }

        /// <summary>Helper to create speed limit sign + label below converted to the opposite unit</summary>
        /// <param name="showMph">Config value from GlobalConfig.I.M.ShowMPH</param>
        /// <param name="speedLimit">The float speed to show</param>
        private void GuiSpeedLimitsWindow_AddButton(bool showMph, SpeedValue speedLimit) {
            // The button is wrapped in vertical sub-layout and a label for MPH/KMPH is added
            GUILayout.BeginVertical();

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            float signSize = GUI_SPEED_SIGN_SIZE;
            if (GUILayout.Button(
                SpeedLimitTextures.GetSpeedLimitTexture(speedLimit),
                GUILayout.Width(signSize),
                GUILayout.Height(signSize * GetVerticalTextureScale()))) {
                currentPaletteSpeedLimit = speedLimit;
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            // For MPH setting display KM/H below, for KM/H setting display MPH
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label(
                showMph
                    ? ToKmphPreciseString(speedLimit)
                    : ToMphPreciseString(speedLimit));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
        }

        private void GuiSpeedLimitsWindow_AddClearButton() {
            GUILayout.BeginVertical();

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            float signSize = TrafficManagerTool.AdaptWidth(GUI_SPEED_SIGN_SIZE);
            if (GUILayout.Button(
                SpeedLimitTextures.Clear,
                GUILayout.Width(signSize),
                GUILayout.Height(signSize * GetVerticalTextureScale()))) {
                currentPaletteSpeedLimit = null;
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            // For MPH setting display KM/H below, for KM/H setting display MPH
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label(Translation.SpeedLimits.Get("Button:Default") + " [del]");

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
        }

        private bool DrawSpeedLimitHandles(ushort segmentId,
                                           ref NetSegment segment,
                                           bool viewOnly,
                                           ref Vector3 camPos) {
            if (viewOnly && !Options.speedLimitsOverlay && !MassEditOverlay.IsActive) {
                return false;
            }
            bool ret = false;

            Vector3 center = segment.m_bounds.center;
            NetManager netManager = Singleton<NetManager>.instance;
            SpeedValue? speedLimitToSet = viewOnly
                                             ? new SpeedValue(-1f)
                                             : currentPaletteSpeedLimit;

            // US signs are rectangular, all other are round
            float speedLimitSignVerticalScale = GetVerticalTextureScale();

            bool showPerLane = viewOnly ? showLimitsPerLane_ : ShowLimitsPerLane;
            if (showPerLane) {
                // show individual speed limit handle per lane
                int numLanes = GeometryUtil.GetSegmentNumVehicleLanes(
                    segmentId,
                    null,
                    out int numDirections,
                    SpeedLimitManager.VEHICLE_TYPES);

                NetInfo segmentInfo = segment.Info;
                Vector3 yu = (segment.m_endDirection - segment.m_startDirection).normalized;
                Vector3 xu = Vector3.Cross(yu, new Vector3(0, 1f, 0)).normalized;

                // if ((segment.m_flags & NetSegment.Flags.Invert) == NetSegment.Flags.None) {
                //        xu = -xu; }
                float f = viewOnly ? 4f : 7f; // reserved sign size in game coordinates
                Vector3 zero = center - (0.5f * (((numLanes - 1) + numDirections) - 1) * f * xu);

                uint x = 0;
                IList<LanePos> sortedLanes = Constants.ServiceFactory.NetService.GetSortedLanes(
                    segmentId,
                    ref segment,
                    null,
                    SpeedLimitManager.LANE_TYPES,
                    SpeedLimitManager.VEHICLE_TYPES);
                bool onlyMonorailLanes = sortedLanes.Count > 0;

                if (!viewOnly) {
                    foreach (LanePos laneData in sortedLanes) {
                        byte laneIndex = laneData.laneIndex;
                        NetInfo.Lane laneInfo = segmentInfo.m_lanes[laneIndex];

                        if ((laneInfo.m_vehicleType & VehicleInfo.VehicleType.Monorail) ==
                            VehicleInfo.VehicleType.None) {
                            onlyMonorailLanes = false;
                            break;
                        }
                    }
                }

                var directions = new HashSet<NetInfo.Direction>();
                int sortedLaneIndex = -1;

                foreach (LanePos laneData in sortedLanes) {
                    ++sortedLaneIndex;
                    uint laneId = laneData.laneId;
                    byte laneIndex = laneData.laneIndex;

                    NetInfo.Lane laneInfo = segmentInfo.m_lanes[laneIndex];
                    if (!directions.Contains(laneInfo.m_finalDirection)) {
                        if (directions.Count > 0) {
                            ++x; // space between different directions
                        }

                        directions.Add(laneInfo.m_finalDirection);
                    }

                    SpeedValue laneSpeedLimit = new SpeedValue(
                        SpeedLimitManager.Instance.GetCustomSpeedLimit(laneId));

                    bool hoveredHandle = MainTool.DrawGenericOverlayGridTexture(
                        SpeedLimitTextures.GetSpeedLimitTexture(laneSpeedLimit),
                        camPos,
                        zero,
                        f,
                        f,
                        xu,
                        yu,
                        x,
                        0,
                        speedLimitSignSize,
                        speedLimitSignSize * speedLimitSignVerticalScale,
                        !viewOnly);

                    if (!viewOnly
                        && !onlyMonorailLanes
                        && ((laneInfo.m_vehicleType & VehicleInfo.VehicleType.Monorail) !=
                            VehicleInfo.VehicleType.None)) {
                        Texture2D tex1 = RoadUI.VehicleInfoSignTextures[
                            LegacyExtVehicleType.ToNew(ExtVehicleType.PassengerTrain)];
                        MainTool.DrawStaticSquareOverlayGridTexture(
                            tex1,
                            camPos,
                            zero,
                            f,
                            xu,
                            yu,
                            x,
                            1,
                            speedLimitSignSize);
                    }

                    if (hoveredHandle) {
                        renderData_.SegmentId = segmentId;
                        renderData_.LaneId = laneId;
                        renderData_.LaneIndex = laneIndex;
                        renderData_.LaneInfo = laneInfo;
                        renderData_.SortedLaneIndex = sortedLaneIndex;
                        ret = true;
                    }
                    if (hoveredHandle && Input.GetMouseButtonDown(0) && !IsCursorInPanel()) {
                        SpeedLimitManager.Instance.SetSpeedLimit(
                            segmentId,
                            laneIndex,
                            laneInfo,
                            laneId,
                            speedLimitToSet?.GameUnits);

                        if (MultiSegmentMode) {
                            if (new RoundaboutMassEdit().TraverseLoop(segmentId, out var segmentList)) {
                                var lanes = FollowRoundaboutLane(segmentList, segmentId, sortedLaneIndex);
                                foreach (var lane in lanes) {
                                    if (lane.laneId == laneId) // the speed limit for this lane has already been set.
                                        continue;
                                    SetSpeedLimit(lane, speedLimitToSet);
                                }
                            } else {
                                int slIndexCopy = sortedLaneIndex;
                                SegmentLaneTraverser.Traverse(
                                    segmentId,
                                    SegmentTraverser.TraverseDirection.AnyDirection,
                                    SegmentTraverser.TraverseSide.AnySide,
                                    SegmentLaneTraverser.LaneStopCriterion.LaneCount,
                                    SegmentTraverser.SegmentStopCriterion.Junction,
                                    SpeedLimitManager.LANE_TYPES,
                                    SpeedLimitManager.VEHICLE_TYPES,
                                    data => {
                                        if (data.SegVisitData.Initial) {
                                            return true;
                                        }

                                        if (slIndexCopy != data.SortedLaneIndex) {
                                            return true;
                                        }

                                        ushort visitSegmentId = data
                                            .SegVisitData
                                            .CurSeg
                                            .segmentId;

                                        NetInfo.Lane curLaneInfo = visitSegmentId
                                            .ToSegment()
                                            .Info
                                            .m_lanes[data.CurLanePos.laneIndex];

                                        SpeedLimitManager.Instance.SetSpeedLimit(
                                            visitSegmentId,
                                            data.CurLanePos.laneIndex,
                                            curLaneInfo,
                                            data.CurLanePos.laneId,
                                            speedLimitToSet?.GameUnits);

                                        return true;
                                    });
                            }
                        }
                    }

                    ++x;
                }
            } else {
                // draw speedlimits over mean middle points of lane beziers

                if (!segmentCenterByDir.TryGetValue(
                        segmentId,
                        out Dictionary<NetInfo.Direction, Vector3> segCenter)) {
                    segCenter = new Dictionary<NetInfo.Direction, Vector3>();
                    segmentCenterByDir.Add(segmentId, segCenter);
                    GeometryUtil.CalculateSegmentCenterByDir(
                        segmentId,
                        segCenter,
                        speedLimitSignSize * TrafficManagerTool.MAX_ZOOM);
                }

                foreach (KeyValuePair<NetInfo.Direction, Vector3> e in segCenter) {
                    bool visible = GeometryUtil.WorldToScreenPoint(e.Value, out Vector3 screenPos);

                    if (!visible) {
                        continue;
                    }

                    float zoom = (1.0f / (e.Value - camPos).magnitude) * 100f * MainTool.GetBaseZoom();
                    float size = (viewOnly ? 0.8f : 1f) * speedLimitSignSize * zoom;
                    Color guiColor = GUI.color;
                    var boundingBox = new Rect(screenPos.x - (size / 2),
                                               screenPos.y - (size / 2),
                                               size,
                                               size * speedLimitSignVerticalScale);
                    bool hoveredHandle = !viewOnly && TrafficManagerTool.IsMouseOver(boundingBox);

                    guiColor.a = TrafficManagerTool.GetHandleAlpha(hoveredHandle);


                    // Draw something right here, the road sign texture
                    GUI.color = guiColor;
                    SpeedValue displayLimit = new SpeedValue(
                        SpeedLimitManager.Instance.GetCustomSpeedLimit(segmentId, e.Key));
                    Texture2D tex = SpeedLimitTextures.GetSpeedLimitTexture(displayLimit);

                    GUI.DrawTexture(boundingBox, tex);

                    if (hoveredHandle) {
                        renderData_.SegmentId = segmentId;
                        renderData_.FinalDirection = e.Key;
                        ret = true;
                    }
                    if (hoveredHandle && Input.GetMouseButtonDown(0) && !IsCursorInPanel()) {
                        // change the speed limit to the selected one
                        // Log._Debug($"Setting speed limit of segment {segmentId}, dir {e.Key.ToString()}
                        //     to {speedLimitToSet}");
                        SpeedLimitManager.Instance.SetSpeedLimit(segmentId,
                                                                 e.Key,
                                                                 currentPaletteSpeedLimit?.GameUnits);

                        if (MultiSegmentMode) {
                            if (new RoundaboutMassEdit().TraverseLoop(segmentId, out var segmentList)) {
                                foreach (ushort segId in segmentList) {
                                    SpeedLimitManager.Instance.SetSpeedLimit(
                                        segId,
                                        currentPaletteSpeedLimit?.GameUnits);
                                }
                            } else {
                                NetInfo.Direction normDir = e.Key;
                                if ((netManager.m_segments.m_buffer[segmentId].m_flags & NetSegment.Flags.Invert) != NetSegment.Flags.None) {
                                    normDir = NetInfo.InvertDirection(normDir);
                                }

                                SegmentLaneTraverser.Traverse(
                                    segmentId,
                                    SegmentTraverser.TraverseDirection.AnyDirection,
                                    SegmentTraverser.TraverseSide.AnySide,
                                    SegmentLaneTraverser.LaneStopCriterion.LaneCount,
                                    SegmentTraverser.SegmentStopCriterion.Junction,
                                    SpeedLimitManager.LANE_TYPES,
                                    SpeedLimitManager.VEHICLE_TYPES,
                                    data => {
                                        if (data.SegVisitData.Initial) {
                                            return true;
                                        }

                                        bool reverse =
                                            data.SegVisitData.ViaStartNode
                                            == data.SegVisitData.ViaInitialStartNode;

                                        ushort otherSegmentId = data.SegVisitData.CurSeg.segmentId;
                                        NetInfo otherSegmentInfo =
                                            netManager.m_segments.m_buffer[otherSegmentId].Info;
                                        byte laneIndex = data.CurLanePos.laneIndex;
                                        NetInfo.Lane laneInfo = otherSegmentInfo.m_lanes[laneIndex];

                                        NetInfo.Direction otherNormDir = laneInfo.m_finalDirection;

                                        if (((netManager.m_segments.m_buffer[otherSegmentId].m_flags
                                              & NetSegment.Flags.Invert)
                                             != NetSegment.Flags.None) ^ reverse) {
                                            otherNormDir = NetInfo.InvertDirection(otherNormDir);
                                        }

                                        if (otherNormDir == normDir) {
                                            SpeedLimitManager.Instance.SetSpeedLimit(
                                                otherSegmentId,
                                                laneInfo.m_finalDirection,
                                                speedLimitToSet?.GameUnits);
                                        }

                                        return true;
                                    });
                            }
                        }
                    }

                    guiColor.a = 1f;
                    GUI.color = guiColor;
                }
            }
            return ret;
        }

        public static string ToMphPreciseString(SpeedValue speed) {
            return FloatUtil.IsZero(speed.GameUnits)
                       ? Translation.SpeedLimits.Get("Unlimited")
                       : speed.ToMphPrecise().ToString();
        }

        public static string ToKmphPreciseString(SpeedValue speed) {
            return FloatUtil.IsZero(speed.GameUnits)
                       ? Translation.SpeedLimits.Get("Unlimited")
                       : speed.ToKmphPrecise().ToString();
        }

        /// <summary>
        /// Produces list of speed limits to offer user in the palette
        /// </summary>
        /// <param name="unit">What kind of speed limit list is required</param>
        /// <returns>List from smallest to largest speed with the given unit. Zero (no limit) is not added to the list.
        /// The values are in-game speeds as float.</returns>
        public static List<SpeedValue> EnumerateSpeedLimits(SpeedUnit unit) {
            var result = new List<SpeedValue>();
            switch (unit) {
                case SpeedUnit.Kmph:
                    for (var km = LOWER_KMPH; km <= UPPER_KMPH; km += KMPH_STEP) {
                        result.Add(SpeedValue.FromKmph(km));
                    }

                    break;
                case SpeedUnit.Mph:
                    for (var mi = LOWER_MPH; mi <= UPPER_MPH; mi += MPH_STEP) {
                        result.Add(SpeedValue.FromMph(mi));
                    }

                    break;
                case SpeedUnit.CurrentlyConfigured:
                    // Automatically choose from the config
                    return GlobalConfig.Instance.Main.DisplaySpeedLimitsMph
                               ? EnumerateSpeedLimits(SpeedUnit.Mph)
                               : EnumerateSpeedLimits(SpeedUnit.Kmph);
            }

            return result;
        }

        /// <summary>
        /// Based on the MPH/KMPH settings round the current speed to the nearest STEP and
        /// then decrease by STEP.
        /// </summary>
        /// <param name="speed">Ingame speed</param>
        /// <returns>Ingame speed decreased by the increment for MPH or KMPH</returns>
        public static SpeedValue GetPrevious(SpeedValue speed) {
            if (speed.GameUnits < 0f) {
                return new SpeedValue(-1f);
            }

            if (GlobalConfig.Instance.Main.DisplaySpeedLimitsMph) {
                MphValue rounded = speed.ToMphRounded(MPH_STEP);
                if (rounded.Mph == LOWER_MPH) {
                    return new SpeedValue(0);
                }

                if (rounded.Mph == 0) {
                    return SpeedValue.FromMph(UPPER_MPH);
                }

                return SpeedValue.FromMph(rounded.Mph > LOWER_MPH
                                              ? (ushort)(rounded.Mph - MPH_STEP)
                                              : LOWER_MPH);
            } else {
                KmphValue rounded = speed.ToKmphRounded(KMPH_STEP);
                if (rounded.Kmph == LOWER_KMPH) {
                    return new SpeedValue(0);
                }

                if (rounded.Kmph == 0) {
                    return SpeedValue.FromKmph(UPPER_KMPH);
                }

                return SpeedValue.FromKmph(rounded.Kmph > LOWER_KMPH
                                               ? (ushort)(rounded.Kmph - KMPH_STEP)
                                               : LOWER_KMPH);
            }
        }

        /// <summary>
        /// Based on the MPH/KMPH settings round the current speed to the nearest STEP and
        /// then increase by STEP.
        /// </summary>
        /// <param name="speed">Ingame speed</param>
        /// <returns>Ingame speed increased by the increment for MPH or KMPH</returns>
        public static SpeedValue GetNext(SpeedValue speed) {
            if (speed.GameUnits < 0f) {
                return new SpeedValue(-1f);
            }

            if (GlobalConfig.Instance.Main.DisplaySpeedLimitsMph) {
                MphValue rounded = speed.ToMphRounded(MPH_STEP);
                rounded += MPH_STEP;

                if (rounded.Mph > UPPER_MPH) {
                    rounded = new MphValue(0);
                }

                return SpeedValue.FromMph(rounded);
            } else {
                KmphValue rounded = speed.ToKmphRounded(KMPH_STEP);
                rounded += KMPH_STEP;

                if (rounded.Kmph > UPPER_KMPH) {
                    rounded = new KmphValue(0);
                }

                return SpeedValue.FromKmph(rounded);
            }
        }

        /// <summary>
        /// For US signs and MPH enabled, scale textures vertically by 1.25f.
        /// Other signs are round.
        /// </summary>
        /// <returns>Multiplier for horizontal sign size</returns>
        public static float GetVerticalTextureScale() {
            return (GlobalConfig.Instance.Main.DisplaySpeedLimitsMph &&
                    (GlobalConfig.Instance.Main.MphRoadSignStyle == MphSignStyle.SquareUS))
                       ? 1.25f
                       : 1.0f;
        }
    } // end class
}
