namespace TrafficManager.UI.SubTools {
    using ColossalFramework;
    using static Util.SegmentLaneTraverser;
    using System.Collections.Generic;
    using TrafficManager.Manager.Impl;
    using TrafficManager.State;
    using TrafficManager.UI.Textures;
    using TrafficManager.Util.Caching;
    using TrafficManager.Util;
    using UnityEngine;
    using TrafficManager.UI.Helpers;
    using static TrafficManager.Util.Shortcuts;
    using ColossalFramework.Math;
    using CSUtil.Commons;

    public class ParkingRestrictionsTool : SubTool {
        private ParkingRestrictionsManager parkingManager => ParkingRestrictionsManager.Instance;


        private readonly Dictionary<ushort, Dictionary<NetInfo.Direction, Vector3>> segmentCenterByDir
            = new Dictionary<ushort, Dictionary<NetInfo.Direction, Vector3>>();

        private const float SIGN_SIZE = 80f;

        private struct GUIHoverInfo {
            internal NetInfo.Direction direction;
            internal ushort segmentId;

            public static bool operator ==(GUIHoverInfo a, GUIHoverInfo b) =>
                a.segmentId == b.segmentId && a.direction == b.direction;

            public static bool operator !=(GUIHoverInfo a, GUIHoverInfo b) =>
                !(a == b);

        }
        private SegmentLaneMarker LaneMarker;
        private uint HoveredLaneId;
        private GUIHoverInfo Hover;
        private GUIHoverInfo CachedHover;


        /// <summary>
        /// Stores potentially visible segment ids while the camera did not move
        /// </summary>
        private GenericArrayCache<ushort> CachedVisibleSegmentIds { get; }

        /// <summary>
        /// Stores last cached camera position in <see cref="CachedVisibleSegmentIds"/>
        /// </summary>
        private CameraTransformValue LastCachedCamera { get; set; }

        public ParkingRestrictionsTool(TrafficManagerTool mainTool)
            : base(mainTool)
        {
            CachedVisibleSegmentIds = new GenericArrayCache<ushort>(NetManager.MAX_SEGMENT_COUNT);
            LastCachedCamera = new CameraTransformValue();
        }

        public override void OnActivate() { }

        public override void OnPrimaryClickOverlay() {

        }

        private SegmentLaneMarker GetLaneMarker(ushort segmentId, NetInfo.Direction direction) {
            Bezier3 bezier = default(Bezier3);
            netService.IterateSegmentLanes(
                segmentId,
                (uint laneId,
                ref NetLane lane,
                NetInfo.Lane laneInfo,
                ushort _,
                ref NetSegment segment,
                byte laneIndex) => {
                    bool isParking = laneInfo.m_laneType.IsFlagSet(NetInfo.LaneType.Parking);
                    if (isParking && laneInfo.m_direction == direction) {
                        bezier = lane.m_bezier;
                        return false;
                    }
                    return true;
                });
            return new SegmentLaneMarker(bezier);
        }

        private bool IsSameDirection(ushort segmentId1, ushort segmentId2) {
            bool invert1 = segmentId1.ToSegment().m_flags.IsFlagSet(NetSegment.Flags.Invert);
            bool invert2 = segmentId2.ToSegment().m_flags.IsFlagSet(NetSegment.Flags.Invert);
            return invert1 == invert2;
        } 

        private void RenderRoadParking(RenderManager.CameraInfo cameraInfo) {
            NetLane[] laneBuffer = NetManager.instance.m_lanes.m_buffer;
            bool LaneVisitor(SegmentLaneVisitData data) {
                ushort segmentId = data.SegVisitData.CurSeg.segmentId;
                int laneIndex = data.CurLanePos.laneIndex;
                NetInfo.Lane laneInfo = segmentId.ToSegment().Info.m_lanes[laneIndex];

                NetInfo.Direction direction = laneInfo.m_direction;
                if(!IsSameDirection(segmentId, Hover.segmentId)) {
                    direction = NetInfo.InvertDirection(direction);
                }
                if (direction == Hover.direction) {
                    bool pressed = Input.GetMouseButton(0);
                    Color color = MainTool.GetToolColor(pressed, false);
                    uint otherLaneId = data.CurLanePos.laneId;
                    var laneMarker = new SegmentLaneMarker(laneBuffer[otherLaneId].m_bezier);
                    laneMarker.RenderOverlay(cameraInfo, color, enlarge: pressed);
                }

                return true;
            }

            SegmentLaneTraverser.Traverse(
                Hover.segmentId,
                SegmentTraverser.TraverseDirection.AnyDirection,
                SegmentTraverser.TraverseSide.AnySide,
                SegmentLaneTraverser.LaneStopCriterion.LaneCount,
                SegmentTraverser.SegmentStopCriterion.Junction,
                ParkingRestrictionsManager.LANE_TYPES,
                ParkingRestrictionsManager.VEHICLE_TYPES,
                LaneVisitor);
        }

        public override void RenderOverlay(RenderManager.CameraInfo cameraInfo) {
            if(Hover.segmentId == 0) {
                return;
            }
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            if (shift) {
                RenderRoadParking(cameraInfo);
                return;
            }

            bool allowed = parkingManager.IsParkingAllowed(Hover.segmentId, Hover.direction);
            bool pressed = Input.GetMouseButton(0);
            Color color;
            if (pressed) {
                color = MainTool.GetToolColor(true, false);
            } else if(allowed){
                color = Color.green;
            } else {
                color = Color.red;
            }

            if(Hover != CachedHover) {
                CachedHover = Hover;
                LaneMarker = GetLaneMarker(Hover.segmentId,Hover.direction);
            }

            LaneMarker.RenderOverlay(cameraInfo, color, enlarge: pressed);
        }

        public override void ShowGUIOverlay(ToolMode toolMode, bool viewOnly) {
            if (viewOnly && !Options.parkingRestrictionsOverlay) {
                return;
            }

            ShowSigns(viewOnly);
        }

        public override void Cleanup() {
            segmentCenterByDir.Clear();
            CachedVisibleSegmentIds.Clear();
            lastCamPos = null;
            lastCamRot = null;
        }

        private Quaternion? lastCamRot;
        private Vector3? lastCamPos;

        private void ShowSigns(bool viewOnly) {
            NetManager netManager = Singleton<NetManager>.instance;

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

                    // if ((netManager.m_segments.m_buffer[segmentId].m_flags
                    //     & NetSegment.Flags.Untouchable) != NetSegment.Flags.None)
                    // continue;
                    Vector3 distToCamera = netManager.m_segments.m_buffer[segmentId].m_bounds.center - camPos;
                    if (distToCamera.sqrMagnitude > TrafficManagerTool.MAX_OVERLAY_DISTANCE_SQR) {
                        continue; // do not draw if too distant
                    }

                    bool visible = MainTool.WorldToScreenPoint(
                        netManager.m_segments.m_buffer[segmentId].m_bounds.center,
                        out Vector3 _);

                    if (!visible) {
                        continue;
                    }

                    if (!parkingManager.MayHaveParkingRestriction((ushort)segmentId)) {
                        continue;
                    }

                    CachedVisibleSegmentIds.Add((ushort)segmentId);
                } // end for all segments
            }

            bool hovered = false;
            bool clicked = !viewOnly && MainTool.CheckClicked();
            for (int segmentIdIndex = CachedVisibleSegmentIds.Size - 1;
                 segmentIdIndex >= 0;
                 segmentIdIndex--)
            {
                ushort segmentId = CachedVisibleSegmentIds.Values[segmentIdIndex];

                // draw parking restrictions
                if ((MainTool.GetToolMode() == ToolMode.SpeedLimits)
                    || ((MainTool.GetToolMode() == ToolMode.VehicleRestrictions)
                        && (segmentId == SelectedSegmentId)))
                {
                    continue;
                }

                // no parking restrictions overlay on selected segment when in vehicle restrictions mode
                var dir = DrawParkingRestrictionHandles(
                    segmentId,
                    clicked,
                    ref netManager.m_segments.m_buffer[segmentId],
                    viewOnly,
                    ref camPos);
                if (dir != NetInfo.Direction.None) {
                    Hover.segmentId = segmentId;
                    Hover.direction = dir;
                    hovered = true;
                } 
            }
            if (!hovered) {
                Hover.segmentId = 0;
                Hover.direction = NetInfo.Direction.None;
            }
        }

        private NetInfo.Direction DrawParkingRestrictionHandles(ushort segmentId,
                                                   bool clicked,
                                                   ref NetSegment segment,
                                                   bool viewOnly,
                                                   ref Vector3 camPos) {
            if (viewOnly && !Options.parkingRestrictionsOverlay) {
                return NetInfo.Direction.None;
            }

            NetManager netManager = Singleton<NetManager>.instance;
            ParkingRestrictionsManager parkingManager = ParkingRestrictionsManager.Instance;
            NetInfo.Direction hoveredDirection = NetInfo.Direction.None;

            // draw parking restriction signs over mean middle points of lane beziers
            if (!segmentCenterByDir.TryGetValue(
                    segmentId,
                    out Dictionary<NetInfo.Direction, Vector3> segCenter))
            {
                segCenter = new Dictionary<NetInfo.Direction, Vector3>();
                segmentCenterByDir.Add(segmentId, segCenter);
                TrafficManagerTool.CalculateSegmentCenterByDir(segmentId, segCenter);
            }

            foreach (KeyValuePair<NetInfo.Direction, Vector3> e in segCenter) {
                bool allowed = parkingManager.IsParkingAllowed(segmentId, e.Key);
                if (allowed && viewOnly) {
                    continue;
                }

                bool visible = MainTool.WorldToScreenPoint(e.Value, out Vector3 screenPos);

                if (!visible) {
                    continue;
                }

                float zoom = (1.0f / (e.Value - camPos).magnitude) * 100f * MainTool.GetBaseZoom();
                float size = (viewOnly ? 0.8f : 1f) * SIGN_SIZE * zoom;
                Color guiColor = GUI.color;
                Rect boundingBox = new Rect(
                    screenPos.x - (size / 2),
                    screenPos.y - (size / 2),
                    size,
                    size);

                if (Options.speedLimitsOverlay) {
                    boundingBox.y -= size + 10f;
                }

                bool hoveredHandle = !viewOnly && TrafficManagerTool.IsMouseOver(boundingBox);

                guiColor.a = TrafficManagerTool.GetHandleAlpha(hoveredHandle);

                if (hoveredHandle) {
                    // mouse hovering over sign
                    hoveredDirection = e.Key;
                }

                GUI.color = guiColor;
                GUI.DrawTexture(boundingBox, RoadUI.ParkingRestrictionTextures[allowed]);

                if (hoveredHandle && clicked && !IsCursorInPanel() &&
                    parkingManager.ToggleParkingAllowed(segmentId, hoveredDirection)) {
                    allowed = !allowed;

                    if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                    {
                        NetInfo.Direction normDir = e.Key;

                        if ((netManager.m_segments.m_buffer[segmentId].m_flags &
                             NetSegment.Flags.Invert) != NetSegment.Flags.None) {
                            normDir = NetInfo.InvertDirection(normDir);
                        }

                        bool LaneVisitor(SegmentLaneVisitData data) {
                            if (data.SegVisitData.Initial) {
                                return true;
                            }

                            bool reverse = data.SegVisitData.ViaStartNode ==
                                           data.SegVisitData.ViaInitialStartNode;

                            ushort otherSegmentId = data.SegVisitData.CurSeg.segmentId;
                            NetInfo otherSegmentInfo =
                                netManager.m_segments.m_buffer[otherSegmentId].Info;
                            byte laneIndex = data.CurLanePos.laneIndex;
                            NetInfo.Lane laneInfo = otherSegmentInfo.m_lanes[laneIndex];

                            NetInfo.Direction otherNormDir = laneInfo.m_finalDirection;
                            if (((netManager.m_segments.m_buffer[otherSegmentId].m_flags &
                                  NetSegment.Flags.Invert) != NetSegment.Flags.None) ^ reverse) {
                                otherNormDir = NetInfo.InvertDirection(otherNormDir);
                            }

                            if (otherNormDir == normDir) {
                                parkingManager.SetParkingAllowed(
                                    otherSegmentId,
                                    laneInfo.m_finalDirection,
                                    allowed);
                            }

                            return true;
                        }

                        SegmentLaneTraverser.Traverse(
                            segmentId,
                            SegmentTraverser.TraverseDirection.AnyDirection,
                            SegmentTraverser.TraverseSide.AnySide,
                            SegmentLaneTraverser.LaneStopCriterion.LaneCount,
                            SegmentTraverser.SegmentStopCriterion.Junction,
                            ParkingRestrictionsManager.LANE_TYPES,
                            ParkingRestrictionsManager.VEHICLE_TYPES,
                            LaneVisitor);
                    }
                }

                guiColor.a = 1f;
                GUI.color = guiColor;
            }

            return hoveredDirection;
        }
    }
}