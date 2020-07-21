namespace TrafficManager.UI.SubTools.SpeedLimits {
    using System.Collections.Generic;
    using ColossalFramework;
    using GenericGameBridge.Service;
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

    /// <summary>
    /// Stores rendering state for Speed Limits overlay and provides rendering of speed limit signs
    /// overlay for segments/lanes.
    /// </summary>
    public class SpeedLimitsOverlay {
        private ushort segmentId_ = 0;
        private uint laneId_ = 0;
        private byte laneIndex_ = 0;
        private int sortedLaneIndex_ = 0;
        private NetInfo.Lane laneInfo_;
        private NetInfo.Direction finalDirection_ = NetInfo.Direction.None;

        private TrafficManagerTool mainTool_;

        /// <summary>Used to pass options to the overlay rendering.</summary>
        public struct DrawArgs {
            public SpeedLimitsTool ParentTool;

            /// <summary>Set to true to allow bigger and clickable road signs.</summary>
            public bool InteractiveSigns;

            /// <summary>Set to true when operating entire road between two junctions.</summary>
            public bool MultiSegmentMode;

            /// <summary>Set to true to show speed limits for each lane.</summary>
            public bool ShowLimitsPerLane;
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

        public void Render(RenderManager.CameraInfo cameraInfo,
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

        /// <summary>Render the speed limit signs based on the current settings.</summary>
        /// <param name="cameraInfo">The camera.</param>
        /// <param name="interactiveSigns">Whether signs positions are used for mouse interaction.</param>
        public void ShowSigns(RenderManager.CameraInfo cameraInfo,
                              DrawArgs args) {
            NetManager netManager = Singleton<NetManager>.instance;
            SpeedLimitManager speedLimitManager = SpeedLimitManager.Instance;

            var currentCamera = new CameraTransformValue(cameraInfo.m_camera);
            Transform currentCameraTransform = cameraInfo.m_camera.transform;
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
                    segmentId: (ushort)segmentId,
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
            // TODO: Move this decision out of this function, up the callstack
            if (!args.InteractiveSigns
                && !Options.speedLimitsOverlay
                && !MassEditOverlay.IsActive) {
                return false;
            }

            bool ret = false;
            Vector3 center = segment.m_bounds.center;
            NetManager netManager = Singleton<NetManager>.instance;

            SpeedValue speedLimitToSet = args.InteractiveSigns
                ? args.ParentTool.CurrentPaletteSpeedLimit
                : new SpeedValue(-1f);

            // US signs are rectangular, all other are round
            float speedLimitSignVerticalScale = GetVerticalTextureScale();

            if (args.ShowLimitsPerLane) {
                // show individual speed limit handle per lane
                int numLanes = GeometryUtil.GetSegmentNumVehicleLanes(
                    segmentId: segmentId,
                    nodeId: null,
                    numDirections: out int numDirections,
                    vehicleTypeFilter: SpeedLimitManager.VEHICLE_TYPES);

                NetInfo segmentInfo = segment.Info;
                Vector3 yu = (segment.m_endDirection - segment.m_startDirection).normalized;
                Vector3 xu = Vector3.Cross(yu, new Vector3(0, 1f, 0)).normalized;

                float f = args.InteractiveSigns
                    ? SIGN_SIZE_INTERACTIVE
                    : SIGN_SIZE_READONLY; // reserved sign size in game coordinates

                Vector3 zero = center - (0.5f * (((numLanes - 1) + numDirections) - 1) * f * xu);
                uint x = 0;

                IList<LanePos> sortedLanes = Constants.ServiceFactory.NetService.GetSortedLanes(
                    segmentId: segmentId,
                    segment: ref segment,
                    startNode: null,
                    laneTypeFilter: SpeedLimitManager.LANE_TYPES,
                    vehicleTypeFilter: SpeedLimitManager.VEHICLE_TYPES);

                bool onlyMonorailLanes = sortedLanes.Count > 0;

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

                    bool hoveredHandle = this.mainTool_.DrawGenericOverlayGridTexture(
                        texture: SpeedLimitTextures.GetSpeedLimitTexture(laneSpeedLimit),
                        camPos: camPos,
                        gridOrigin: zero,
                        cellWidth: f,
                        cellHeight: f,
                        xu: xu,
                        yu: yu,
                        x: x,
                        y: 0,
                        width: SPEED_LIMIT_SIGN_SIZE,
                        height: SPEED_LIMIT_SIGN_SIZE * speedLimitSignVerticalScale,
                        canHover: args.InteractiveSigns);

                    if (args.InteractiveSigns
                        && !onlyMonorailLanes
                        && ((laneInfo.m_vehicleType & VehicleInfo.VehicleType.Monorail) !=
                            VehicleInfo.VehicleType.None)) {
                        Texture2D tex1 = RoadUI.VehicleInfoSignTextures[
                            LegacyExtVehicleType.ToNew(ExtVehicleType.PassengerTrain)];

                        this.mainTool_.DrawStaticSquareOverlayGridTexture(
                            texture: tex1,
                            camPos: camPos,
                            gridOrigin: zero,
                            cellSize: f,
                            xu: xu,
                            yu: yu,
                            x: x,
                            y: 1,
                            size: SPEED_LIMIT_SIGN_SIZE);
                    }

                    if (hoveredHandle) {
                        this.segmentId_ = segmentId;
                        this.laneId_ = laneId;
                        this.laneIndex_ = laneIndex;
                        this.laneInfo_ = laneInfo;
                        this.sortedLaneIndex_ = sortedLaneIndex;
                        ret = true;
                    }

                    if (hoveredHandle && Input.GetMouseButtonDown(0)
                                      && !args.ParentTool.ContainsMouse()) {
                        SpeedLimitManager.Instance.SetSpeedLimit(
                            segmentId: segmentId,
                            laneIndex: laneIndex,
                            laneInfo: laneInfo,
                            laneId: laneId,
                            speedLimit: speedLimitToSet.GameUnits);

                        if (args.MultiSegmentMode) {
                            if (new RoundaboutMassEdit().TraverseLoop(segmentId, out var segmentList)) {
                                var lanes = FollowRoundaboutLane(segmentList, segmentId, sortedLaneIndex);
                                foreach (var lane in lanes) {
                                    // the speed limit for this lane has already been set.
                                    if (lane.laneId == laneId) {
                                        continue;
                                    }

                                    SpeedLimitsTool.SetSpeedLimit(lane, speedLimitToSet);
                                }
                            } else {
                                int slIndexCopy = sortedLaneIndex;
                                SegmentLaneTraverser.Traverse(
                                    initialSegmentId: segmentId,
                                    direction: SegmentTraverser.TraverseDirection.AnyDirection,
                                    side: SegmentTraverser.TraverseSide.AnySide,
                                    laneStopCrit: SegmentLaneTraverser.LaneStopCriterion.LaneCount,
                                    segStopCrit: SegmentTraverser.SegmentStopCriterion.Junction,
                                    laneTypeFilter: SpeedLimitManager.LANE_TYPES,
                                    vehicleTypeFilter: SpeedLimitManager.VEHICLE_TYPES,
                                    laneVisitor: data => {
                                        if (data.SegVisitData.Initial) {
                                            return true;
                                        }

                                        if (slIndexCopy != data.SortedLaneIndex) {
                                            return true;
                                        }

                                        Constants.ServiceFactory.NetService.ProcessSegment(
                                            segmentId: data.SegVisitData.CurSeg.segmentId,
                                            handler: (ushort curSegmentId, ref NetSegment curSegment) => {
                                                NetInfo.Lane curLaneInfo = curSegment.Info.m_lanes[
                                                    data.CurLanePos.laneIndex];

                                                SpeedLimitManager.Instance.SetSpeedLimit(
                                                    segmentId: curSegmentId,
                                                    laneIndex: data.CurLanePos.laneIndex,
                                                    laneInfo: curLaneInfo,
                                                    laneId: data.CurLanePos.laneId,
                                                    speedLimit: speedLimitToSet.GameUnits);
                                                return true;
                                            });

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
                        SPEED_LIMIT_SIGN_SIZE * TrafficManagerTool.MAX_ZOOM);
                }

                foreach (KeyValuePair<NetInfo.Direction, Vector3> e in segCenter) {
                    bool visible = GeometryUtil.WorldToScreenPoint(e.Value, out Vector3 screenPos);

                    if (!visible) {
                        continue;
                    }

                    float zoom = (100.0f / (e.Value - camPos).magnitude)
                                 * this.mainTool_.GetBaseZoom();
                    float size = (args.InteractiveSigns ? 1f : 0.8f) * SPEED_LIMIT_SIGN_SIZE * zoom;
                    Color guiColor = GUI.color;
                    var boundingBox = new Rect(x: screenPos.x - (size / 2),
                                               y: screenPos.y - (size / 2),
                                               width: size,
                                               height: size * speedLimitSignVerticalScale);
                    bool hoveredHandle = args.InteractiveSigns
                                         && TrafficManagerTool.IsMouseOver(boundingBox);

                    guiColor.a = TrafficManagerTool.GetHandleAlpha(hoveredHandle);


                    // Draw something right here, the road sign texture
                    GUI.color = guiColor;
                    SpeedValue displayLimit = new SpeedValue(
                        SpeedLimitManager.Instance.GetCustomSpeedLimit(segmentId, e.Key));
                    Texture2D tex = SpeedLimitTextures.GetSpeedLimitTexture(displayLimit);

                    GUI.DrawTexture(position: boundingBox, image: tex);

                    if (hoveredHandle) {
                        this.segmentId_ = segmentId;
                        this.finalDirection_ = e.Key;
                        ret = true;
                    }

                    if (hoveredHandle && Input.GetMouseButtonDown(0)
                                      && !args.ParentTool.ContainsMouse()) {
                        // change the speed limit to the selected one
                        SpeedLimitManager.Instance.SetSpeedLimit(
                            segmentId: segmentId,
                            finalDir: e.Key,
                            speedLimit: args.ParentTool.CurrentPaletteSpeedLimit.GameUnits);

                        if (args.MultiSegmentMode) {
                            if (new RoundaboutMassEdit().TraverseLoop(segmentId, out var segmentList)) {
                                foreach (ushort segId in segmentList) {
                                    SpeedLimitManager.Instance.SetSpeedLimit(
                                        segId,
                                        args.ParentTool.CurrentPaletteSpeedLimit.GameUnits);
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
                                                speedLimitToSet.GameUnits);
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

        /// <summary>
        /// iterates through the given roundabout <paramref name="segmentList"/> returning an enumeration
        /// of all lanes with a matching <paramref name="sortedLaneIndex"/> based on <paramref name="segmentId0"/>
        /// </summary>
        /// <param name="segmentList">input list of roundabout segments (must be oneway, and in the same direction).</param>
        /// <param name="segmentId0">The segment to match lane agaisnt</param>
        /// <param name="sortedLaneIndex">Index.</param>
        private IEnumerable<LanePos> FollowRoundaboutLane(
                    List<ushort> segmentList,
                    ushort segmentId0,
                    int sortedLaneIndex) {
            bool invert0 = segmentId0.ToSegment().m_flags.IsFlagSet(NetSegment.Flags.Invert);

            int count0 = Shortcuts.netService.GetSortedLanes(
               segmentId: segmentId0,
               segment: ref segmentId0.ToSegment(),
               startNode: null,
               laneTypeFilter: SpeedLimitManager.LANE_TYPES,
               vehicleTypeFilter: SpeedLimitManager.VEHICLE_TYPES,
               sort: false).Count;

            foreach (ushort segmentId in segmentList) {
                bool invert = segmentId.ToSegment().m_flags.IsFlagSet(NetSegment.Flags.Invert);
                IList<LanePos> lanes = Shortcuts.netService.GetSortedLanes(
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

                if (index >= 0 && index < lanes.Count) {
                    yield return lanes[index];
                }
            } // foreach
        }
    } // end class
}