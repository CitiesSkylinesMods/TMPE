namespace TrafficManager.UI.SubTools.SpeedLimits {
    using System;
    using System.Collections.Generic;
    using ColossalFramework;
    using GenericGameBridge.Service;
    using TrafficManager.API.Traffic.Data;
    using TrafficManager.Manager.Impl;
    using TrafficManager.State;
    using TrafficManager.Traffic;
    using TrafficManager.UI.Helpers;
    using TrafficManager.UI.Textures;
    using TrafficManager.Util;
    using TrafficManager.Util.Caching;
    using UnityEngine;

    /// <summary>
    /// Stores rendering state for Speed Limits overlay and provides rendering of speed limit signs
    /// overlay for segments/lanes.
    /// </summary>
    public class SpeedLimitsOverlay {
        [Obsolete("Moved to SpeedLimitsOverlaySign")]
        const float SMALL_ICON_SCALE = 0.5f;

        private ushort segmentId_ = 0;
        private NetInfo.Direction finalDirection_ = NetInfo.Direction.None;

        private TrafficManagerTool mainTool_;

        /// <summary>Used to pass options to the overlay rendering.</summary>
        public struct DrawArgs {
            // /// <summary>Set to true to allow bigger and clickable road signs.</summary>
            public bool InteractiveSigns;

            /// <summary>Set to true when operating entire road between two junctions.</summary>
            public bool MultiSegmentMode;

            /// <summary>Set to true to show speed limits for each lane.</summary>
            public bool ShowLimitsPerLane;

            /// <summary>
            /// Set this to true to additionally show the other PerLane/PerSegment mode as small
            /// icons together with the large icons.
            /// </summary>
            public bool ShowOtherPerLaneModeTemporary;

            /// <summary>
            /// If false, overrides will be rendered as main icon, and defaults as small icon.
            /// If true, defaults will be rendered large, and overrides small.
            /// </summary>
            public bool ShowDefaultsMode;

            /// <summary>Hovered SEGMENT speed limit handles (output after rendering).</summary>
            public List<OverlaySegmentSpeedlimitHandle> HoveredSegmentHandles;

            /// <summary>Hovered LANE speed limit handles (output after rendering).</summary>
            public List<OverlayLaneSpeedlimitHandle> HoveredLaneHandles;

            public static DrawArgs Create() {
                return new DrawArgs {
                    InteractiveSigns = false,
                    MultiSegmentMode = false,
                    ShowLimitsPerLane = false,
                    HoveredSegmentHandles = new List<OverlaySegmentSpeedlimitHandle>(capacity: 10),
                    HoveredLaneHandles = new List<OverlayLaneSpeedlimitHandle>(capacity: 10),
                    ShowDefaultsMode = false,
                    ShowOtherPerLaneModeTemporary = false,
                };
            }

            public void ClearHovered() {
                this.HoveredSegmentHandles.Clear();
                this.HoveredLaneHandles.Clear();
            }
        }

        /// <summary>
        /// Stores potentially visible segment ids while the camera did not move.
        /// </summary>
        private GenericArrayCache<ushort> cachedVisibleSegmentIds_;

        /// <summary>
        /// Stores last cached camera position in <see cref="cachedVisibleSegmentIds_"/>
        /// </summary>
        private CameraTransformValue lastCachedCamera_;

        private const float SIGN_SIZE_INTERACTIVE = 7f;
        private const float SIGN_SIZE_READONLY = 4f;
        private const float SPEED_LIMIT_SIGN_SIZE = 70f;

        private readonly Dictionary<ushort, Dictionary<NetInfo.Direction, Vector3>>
            segmentCenterByDir = new Dictionary<ushort, Dictionary<NetInfo.Direction, Vector3>>();

        public SpeedLimitsOverlay(TrafficManagerTool mainTool) {
            this.mainTool_ = mainTool;
            this.cachedVisibleSegmentIds_ = new GenericArrayCache<ushort>(NetManager.MAX_SEGMENT_COUNT);
            this.lastCachedCamera_ = new CameraTransformValue();
        }

        public void RenderHelperGraphics(RenderManager.CameraInfo cameraInfo,
                                         DrawArgs args) {
            if (args.ShowLimitsPerLane) {
                RenderLanes(cameraInfo);
            } else {
                RenderSegments(cameraInfo, args);
            }
        }

        private void RenderLanes(RenderManager.CameraInfo cameraInfo) {
        }

        private void RenderSegments(RenderManager.CameraInfo cameraInfo,
                                    DrawArgs args) {
            if (args.MultiSegmentMode) {
                RenderSegmentSideOverlay(cameraInfo: cameraInfo,
                                         segmentId: this.segmentId_,
                                         args: args,
                                         finalDirection: this.finalDirection_);
            } else if (RoundaboutMassEdit.Instance.TraverseLoop(
                segmentId: this.segmentId_,
                segList: out var segmentList)) {
                foreach (ushort segmentId in segmentList) {
                    RenderSegmentSideOverlay(
                        cameraInfo: cameraInfo,
                        segmentId: segmentId,
                        args: args);
                }
            } else {
                SegmentTraverser.Traverse(
                    initialSegmentId: this.segmentId_,
                    direction: SegmentTraverser.TraverseDirection.AnyDirection,
                    side: SegmentTraverser.TraverseSide.AnySide,
                    stopCrit: SegmentTraverser.SegmentStopCriterion.Junction,
                    visitorFun: data => {
                        NetInfo.Direction finalDirection = this.finalDirection_;
                        if (data.IsReversed(this.segmentId_)) {
                            finalDirection = NetInfo.InvertDirection(finalDirection);
                        }

                        RenderSegmentSideOverlay(
                            cameraInfo: cameraInfo,
                            segmentId: data.CurSeg.segmentId,
                            args: args,
                            finalDirection: finalDirection);
                        return true;
                    });
            }
        }

        /// <summary>
        /// Renders all lanes with the given <paramref name="finalDirection"/>
        /// if NetInfo.Direction.None, all lanes are rendered.
        /// </summary>
        private int RenderSegmentSideOverlay(
            RenderManager.CameraInfo cameraInfo,
            ushort segmentId,
            DrawArgs args,
            NetInfo.Direction finalDirection = NetInfo.Direction.None)
        {
            int count = 0;
            // bool pressed = Input.GetMouseButton(0);
            // Color color = this.mainTool_.GetToolColor(warning: pressed, error: false);

            Shortcuts.netService.IterateSegmentLanes(
                segmentId,
                handler: (uint laneId,
                          ref NetLane lane,
                          NetInfo.Lane laneInfo,
                          ushort _,
                          ref NetSegment segment,
                          byte laneIndex) => {
                    bool render = (laneInfo.m_laneType & SpeedLimitManager.LANE_TYPES) != 0;
                    render &= (laneInfo.m_vehicleType & SpeedLimitManager.VEHICLE_TYPES) != 0;
                    render &= laneInfo.m_finalDirection == finalDirection
                              || finalDirection == NetInfo.Direction.None;

                    if (render) {
                        RenderLaneOverlay(cameraInfo: cameraInfo, laneId: laneId, args: args);
                        count++;
                    }
                    return true;
                });
            return count;
        }

        private void RenderLaneOverlay(RenderManager.CameraInfo cameraInfo,
                                       uint laneId,
                                       DrawArgs args) {
            NetLane[] laneBuffer = NetManager.instance.m_lanes.m_buffer;
            SegmentLaneMarker marker = new SegmentLaneMarker(laneBuffer[laneId].m_bezier);
            bool pressed = Input.GetMouseButton(0);
            Color color = this.mainTool_.GetToolColor(warning: pressed, error: false);

            if (args.ShowLimitsPerLane) {
                marker.Size = 3f; // lump the lanes together.
            }

            marker.RenderOverlay(cameraInfo, color, pressed);
        }

        /// <summary>
        /// NOTE: This must be called from GUI mode, because of GUI.DrawTexture use.
        /// Render the speed limit signs based on the current settings.
        /// </summary>
        /// <param name="args">Parameters how to draw exactly.</param>
        public void ShowSigns_GUI(DrawArgs args) {
            Camera camera = Camera.main;
            if (camera == null) {
                return;
            }

            NetManager netManager = Singleton<NetManager>.instance;
            SpeedLimitManager speedLimitManager = SpeedLimitManager.Instance;

            var currentCamera = new CameraTransformValue(camera);
            Transform currentCameraTransform = camera.transform;
            Vector3 camPos = currentCameraTransform.position;

            if (!lastCachedCamera_.Equals(currentCamera)) {
                // cache visible segments
                lastCachedCamera_ = currentCamera;
                cachedVisibleSegmentIds_.Clear();

                ShowSigns_CacheVisibleSegments(
                    netManager: netManager,
                    camPos: camPos,
                    speedLimitManager: speedLimitManager);
            }

            bool hover = false;
            for (int segmentIdIndex = cachedVisibleSegmentIds_.Size - 1;
                 segmentIdIndex >= 0;
                 segmentIdIndex--) {
                ushort segmentId = cachedVisibleSegmentIds_.Values[segmentIdIndex];

                // If VehicleRestrictions tool is active, skip drawing the current selected segment
                if ((mainTool_.GetToolMode() == ToolMode.VehicleRestrictions) &&
                    (segmentId == TrafficManagerTool.SelectedSegmentId)) {
                    continue;
                }

                // no speed limit overlay on selected segment when in vehicle restrictions mode
                hover |= DrawSpeedLimitHandles(
                    segmentId: segmentId,
                    segment: ref netManager.m_segments.m_buffer[segmentId],
                    camPos: ref camPos,
                    args: args);
            }

            if (!hover) {
                this.segmentId_ = 0;
            }
        }

        /// <summary>
        /// When camera position has changed and cached segments set is invalid, scan all segments
        /// again and remember those visible in the camera frustum.
        /// </summary>
        /// <param name="netManager">Access to map data.</param>
        /// <param name="camPos">Camera position to consider.</param>
        /// <param name="speedLimitManager">Query if a segment is eligible for speed limits.</param>
        private void ShowSigns_CacheVisibleSegments(NetManager netManager,
                                                    Vector3 camPos,
                                                    SpeedLimitManager speedLimitManager) {
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
                    worldPos: netManager.m_segments.m_buffer[segmentId].m_bounds.center,
                    screenPos: out Vector3 _);

                if (!visible) {
                    continue;
                }

                if (!speedLimitManager.MayHaveCustomSpeedLimits(
                    segment: ref netManager.m_segments.m_buffer[segmentId])) {
                    continue;
                }

                cachedVisibleSegmentIds_.Add((ushort)segmentId);
            } // end for all segments
        }

        private bool DrawSpeedLimitHandles(ushort segmentId,
                                           ref NetSegment segment,
                                           ref Vector3 camPos,
                                           DrawArgs args) {
            // // TOxDO: Move this decision out of this function, up the callstack
            // if (!args.InteractiveSigns
            //     && !Options.speedLimitsOverlay
            //     && !MassEditOverlay.IsActive) {
            //     return false;
            // }

            // US signs are rectangular, all other are round
            float speedLimitSignVerticalScale = GetVerticalTextureScale();

            if (args.ShowLimitsPerLane) {
                return DrawSpeedLimitHandles_PerLane(
                    segmentId,
                    ref segment,
                    camPos,
                    args,
                    speedLimitSignVerticalScale);
            }

            return DrawSpeedLimitHandles_PerSegment(
                segmentId,
                camPos,
                args,
                speedLimitSignVerticalScale);
        }

        /// <summary>Render speed limit handles one per segment.</summary>
        /// <param name="segmentId">Seg id.</param>
        /// <param name="camPos">Camera.</param>
        /// <param name="args">Render args.</param>
        /// <param name="speedLimitSignVerticalScale">Whether sign is square or rectangular.</param>
        private bool DrawSpeedLimitHandles_PerSegment(ushort segmentId,
                                                      Vector3 camPos,
                                                      DrawArgs args,
                                                      float speedLimitSignVerticalScale) {
            bool ret = false;

            // draw speedlimits over mean middle points of lane beziers
            if (!segmentCenterByDir.TryGetValue(
                segmentId,
                out Dictionary<NetInfo.Direction, Vector3> segCenter)) {
                segCenter = new Dictionary<NetInfo.Direction, Vector3>();
                segmentCenterByDir.Add(segmentId, segCenter);
                GeometryUtil.CalculateSegmentCenterByDir(
                    segmentId: segmentId,
                    segmentCenterByDir: segCenter,
                    minDistance: SPEED_LIMIT_SIGN_SIZE * TrafficManagerTool.MAX_ZOOM);
            }

            //-- ignore uizoom -- float uiZoom = U.UIScaler.GetScale();
            SpeedLimitsOverlaySign signRenderer = new SpeedLimitsOverlaySign(speedLimitSignVerticalScale);

            // start from empty, no handles are hovered
            args.ClearHovered();

            foreach (KeyValuePair<NetInfo.Direction, Vector3> e in segCenter) {
                bool visible = GeometryUtil.WorldToScreenPoint(e.Value, out Vector3 screenPos);

                if (!visible) {
                    continue;
                }

                float zoom = (100.0f / (e.Value - camPos).magnitude);
                float size = (args.InteractiveSigns ? 1f : 0.8f) * SPEED_LIMIT_SIGN_SIZE * zoom;
                Color guiColor = GUI.color;

                // Recalculate visible rect for screen position and size
                signRenderer.Reset(screenPos, size);

                bool isHoveredHandle = args.InteractiveSigns && signRenderer.ContainsMouse();

                guiColor.a = TrafficManagerTool.GetHandleAlpha(isHoveredHandle);

                // Draw something right here, the road sign texture
                GUI.color = guiColor;

                // Get speed limit override for segment
                SpeedValue? overrideSpeedlimit =
                    SpeedLimitManager.Instance.GetCustomSpeedLimit(segmentId, e.Key);

                // Get default or default-override speed limit for road type
                NetInfo neti = GetSegmentNetinfo(segmentId);
                SpeedValue defaultSpeedlimit =
                    new SpeedValue(SpeedLimitManager.Instance.GetCustomNetInfoSpeedLimit(neti));

                // Render override
                signRenderer.DrawLargeTexture(
                    args.ShowDefaultsMode ? defaultSpeedlimit : overrideSpeedlimit);

                // If Alt is held, then also overlay the other (default limit in edit override mode,
                // or override in edit defaults mode) as a small texture.
                if (args.ShowOtherPerLaneModeTemporary) {
                    signRenderer.DrawSmallTexture(
                        args.ShowDefaultsMode ? overrideSpeedlimit : defaultSpeedlimit);
                }

                if (isHoveredHandle) {
                    // Clickable overlay (interactive signs also True):
                    // Register the position of a mouse-hovered speedlimit overlay icon
                    args.HoveredSegmentHandles.Add(
                        new OverlaySegmentSpeedlimitHandle(
                            segmentId: segmentId,
                            finalDirection: e.Key));

                    this.segmentId_ = segmentId;
                    this.finalDirection_ = e.Key;
                    ret = true;
                }

                guiColor.a = 1f;
                GUI.color = guiColor;
            }

            return ret;
        }

        /// <summary>
        /// From segment ID find the Segment and from it retrieve the NetInfo. Should be fast.
        /// </summary>
        /// <param name="segmentId">Segment.</param>
        /// <returns>Netinfo of that segment.</returns>
        private NetInfo GetSegmentNetinfo(ushort segmentId) {
            NetSegment[] segmentsBuffer = Singleton<NetManager>.instance.m_segments.m_buffer;
            return segmentsBuffer[segmentId].Info;
        }

        /// <summary>Draw speed limit handles one per lane.</summary>
        /// <param name="segmentId">Seg id.</param>
        /// <param name="segment">Segment reference from the game data.</param>
        /// <param name="camPos">Camera.</param>
        /// <param name="args">Render args.</param>
        /// <param name="speedLimitSignVerticalScale">Whether signs are square or rectangular.</param>
        private bool DrawSpeedLimitHandles_PerLane(ushort segmentId,
                                                   ref NetSegment segment,
                                                   Vector3 camPos,
                                                   DrawArgs args,
                                                   float speedLimitSignVerticalScale) {
            // start from empty, no handles are hovered
            args.ClearHovered();

            bool ret = false;
            Vector3 segmentCenterPos = segment.m_bounds.center;

            // show individual speed limit handle per lane
            int numLanes = GeometryUtil.GetSegmentNumVehicleLanes(
                segmentId: segmentId,
                nodeId: null,
                numDirections: out int numDirections,
                vehicleTypeFilter: SpeedLimitManager.VEHICLE_TYPES);

            NetInfo segmentInfo = segment.Info;
            Vector3 yu = (segment.m_endDirection - segment.m_startDirection).normalized;
            Vector3 xu = Vector3.Cross(yu, new Vector3(0, 1f, 0)).normalized;

            float signSize = args.InteractiveSigns
                ? SIGN_SIZE_INTERACTIVE
                : SIGN_SIZE_READONLY; // reserved sign size in game coordinates

            Vector3 worldPos = segmentCenterPos -
                               (0.5f * (((numLanes - 1) + numDirections) - 1) * signSize * xu);
            uint x = 0;

            IList<LanePos> sortedLanes = Constants.ServiceFactory.NetService.GetSortedLanes(
                segmentId: segmentId,
                segment: ref segment,
                startNode: null,
                laneTypeFilter: SpeedLimitManager.LANE_TYPES,
                vehicleTypeFilter: SpeedLimitManager.VEHICLE_TYPES);

            bool onlyMonorailLanes = sortedLanes.Count > 0;

            // bool isMouseButtonDown =
            //     Input.GetMouseButtonDown(0) && !args.ParentTool.ContainsMouse();

            if (args.InteractiveSigns) {
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

            // Main grid for large icons
            var grid = new Highlight.Grid(
                gridOrigin: worldPos,
                cellWidth: signSize,
                cellHeight: signSize,
                xu: xu,
                yu: yu);

            //-----------------------
            // For all lanes sorted
            //-----------------------
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

                GetSpeedLimitResult laneSpeedLimit = SpeedLimitManager.Instance.GetCustomSpeedLimit(laneId);
                Rect screenRect;

                //--------------------------------------
                //|     |Main icon texture (bigger icon)
                //| 9 0 |
                //|     |Only draw if override exists
                //--------------------------------------
                Texture2D mainIconTexture =
                    (laneSpeedLimit.Type == GetSpeedLimitResult.ResultType.OverrideExists
                     && laneSpeedLimit.OverrideValue.HasValue)
                        ? SpeedLimitTextures.GetSpeedLimitTexture(laneSpeedLimit.OverrideValue.Value)
                        : SpeedLimitTextures.TexturesKmph[0];

                bool isHoveredHandle = grid.DrawGenericOverlayGridTexture(
                        texture: mainIconTexture,
                        camPos: camPos,
                        x: x,
                        y: 0,
                        width: SPEED_LIMIT_SIGN_SIZE,
                        height: SPEED_LIMIT_SIGN_SIZE * speedLimitSignVerticalScale,
                        canHover: args.InteractiveSigns,
                        screenRect: out screenRect);

                //--------------------------------------------------------
                //|     |
                //| 9 0 | Small icon texture (25% size icon in the corner)
                //|   50|
                //--------------------------------------------------------
                Texture2D smallIconTexture =
                    SpeedLimitTextures.GetSpeedLimitTexture(laneSpeedLimit.DefaultValue);

                grid.DrawGenericOverlayGridTexture(
                    texture: smallIconTexture,
                    camPos: camPos,
                    x: x + 0.25f,
                    y: 0.5f,
                    width: SPEED_LIMIT_SIGN_SIZE * SMALL_ICON_SCALE,
                    height: SPEED_LIMIT_SIGN_SIZE * speedLimitSignVerticalScale * SMALL_ICON_SCALE,
                    canHover: false, // cannot hover small texture
                    screenRect: out screenRect);

                if (args.InteractiveSigns
                    && !onlyMonorailLanes
                    && ((laneInfo.m_vehicleType & VehicleInfo.VehicleType.Monorail) != VehicleInfo.VehicleType.None))
                {
                    Texture2D tex1 = RoadUI.VehicleInfoSignTextures[
                        LegacyExtVehicleType.ToNew(ExtVehicleType.PassengerTrain)];

                    // TODO: Replace with direct call to GUI.DrawTexture as in the func above
                    grid.DrawStaticSquareOverlayGridTexture(
                        texture: tex1,
                        camPos: camPos,
                        x: x,
                        y: 1,
                        size: SPEED_LIMIT_SIGN_SIZE,
                        screenRect: out screenRect);
                }

                if (isHoveredHandle) {
                    // Clickable overlay (interactive signs also True):
                    // Register the position of a mouse-hovered speedlimit overlay icon
                    args.HoveredLaneHandles.Add(
                        new OverlayLaneSpeedlimitHandle(
                            segmentId: segmentId,
                            laneId: laneId,
                            laneIndex: laneIndex,
                            laneInfo: laneInfo,
                            sortedLaneIndex: sortedLaneIndex));

                    this.segmentId_ = segmentId;
                    // this.laneId_ = laneId;
                    // this.laneIndex_ = laneIndex;
                    // this.laneInfo_ = laneInfo;
                    // this.sortedLaneIndex_ = sortedLaneIndex;
                    ret = true;
                }

                ++x;
            }

            return ret;
        }

        /// <summary>
        /// For US signs and MPH enabled, scale textures vertically by 1.25f.
        /// Other signs are round.
        /// </summary>
        /// <returns>Multiplier for horizontal sign size.</returns>
        private static float GetVerticalTextureScale() {
            return (GlobalConfig.Instance.Main.DisplaySpeedLimitsMph &&
                    (GlobalConfig.Instance.Main.MphRoadSignStyle == SpeedLimitSignTheme.SquareUS))
                ? 1.25f
                : 1.0f;
        }
    }

    // end class
}