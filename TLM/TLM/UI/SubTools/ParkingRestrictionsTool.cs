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
    using TrafficManager.UI.SubTools.PrioritySigns;
    using ColossalFramework.UI;
    using TrafficManager.UI.MainMenu.OSD;
    using TrafficManager.Util.Extensions;

    public class ParkingRestrictionsTool
        : LegacySubTool,
          UI.MainMenu.IOnscreenDisplayProvider
    {
        public ParkingRestrictionsTool(TrafficManagerTool mainTool)
            : base(mainTool) {
            CachedVisibleSegmentIds = new GenericArrayCache<ushort>(NetManager.MAX_SEGMENT_COUNT);
            LastCachedCamera = new CameraTransformValue();
        }

        private ParkingRestrictionsManager parkingManager => ParkingRestrictionsManager.Instance;

        private readonly Dictionary<ushort, Dictionary<NetInfo.Direction, Vector3>> segmentCenterByDir
            = new Dictionary<ushort, Dictionary<NetInfo.Direction, Vector3>>();

        private const float SIGN_SIZE = 80f;

        private SegmentLaneMarker laneMarker_;

        private RenderData renderInfo_;

        private struct RenderData {
            internal NetInfo.Direction FinalDirection;
            internal ushort SegmentId;

            public static bool operator ==(RenderData a, RenderData b) =>
                a.SegmentId == b.SegmentId && a.FinalDirection == b.FinalDirection;

            public static bool operator !=(RenderData a, RenderData b) =>
                !(a == b);
        }

        /// <summary>
        /// Stores potentially visible segment ids while the camera did not move
        /// </summary>
        private GenericArrayCache<ushort> CachedVisibleSegmentIds { get; }

        /// <summary>
        /// Stores last cached camera position in <see cref="CachedVisibleSegmentIds"/>
        /// </summary>
        private CameraTransformValue LastCachedCamera { get; set; }

        private bool forceCameraCacheReset_ = false;

        public override void OnActivate() {
            base.OnActivate();
            MainTool.RequestOnscreenDisplayUpdate();
            this.forceCameraCacheReset_ = true;
        }

        public override void OnPrimaryClickOverlay() { }

        public override void OnSecondaryClickOverlay() {
            MainTool.SetToolMode(ToolMode.None);
        }

        private void RenderSegmentParkings(RenderManager.CameraInfo cameraInfo) {
            bool allowed = parkingManager.IsParkingAllowed(renderInfo_.SegmentId, renderInfo_.FinalDirection);
            bool pressed = Input.GetMouseButton(0);
            Color color;
            if (pressed) {
                color = MainTool.GetToolColor(true, false);
            } else if (allowed) {
                color = Color.green;
            } else {
                color = Color.red;
            }

            Bezier3 bezier = default;
            ref NetSegment segment = ref renderInfo_.SegmentId.ToSegment();
            NetInfo netInfo = segment.Info;
            if (netInfo == null) {
                return;
            }

            ExtSegmentManager extSegmentManager = ExtSegmentManager.Instance;
            foreach (LaneIdAndIndex laneIdAndIndex in extSegmentManager.GetSegmentLaneIdsAndLaneIndexes(renderInfo_.SegmentId)) {
                NetInfo.Lane laneInfo = netInfo.m_lanes[laneIdAndIndex.laneIndex];
                bool isParking = laneInfo.m_laneType.IsFlagSet(NetInfo.LaneType.Parking);
                if (isParking && laneInfo.m_finalDirection == renderInfo_.FinalDirection) {
                    bezier = laneIdAndIndex.laneId.ToLane().m_bezier;
                    laneMarker_ = new SegmentLaneMarker(bezier);
                    laneMarker_.RenderOverlay(cameraInfo, color, enlarge: pressed);
                }
            }
        }

        private void RenderRoadParkings(RenderManager.CameraInfo cameraInfo) {
            bool LaneVisitor(SegmentLaneVisitData data) {
                ushort segmentId = data.SegVisitData.CurSeg.segmentId;
                int laneIndex = data.CurLanePos.laneIndex;
                NetInfo.Lane laneInfo = segmentId.ToSegment().Info.m_lanes[laneIndex];

                NetInfo.Direction finalDirection = laneInfo.m_finalDirection;

                if (!data.SegVisitData.Initial) {
                    bool reverse =
                        data.SegVisitData.ViaStartNode ==
                        data.SegVisitData.ViaInitialStartNode;

                    bool invert1 = segmentId.ToSegment().m_flags.IsFlagSet(NetSegment.Flags.Invert);
                    bool invert2 = renderInfo_.SegmentId.ToSegment().m_flags.IsFlagSet(NetSegment.Flags.Invert);
                    bool invert = invert1 != invert2;

                    if (reverse ^ invert) {
                        finalDirection = NetInfo.InvertDirection(finalDirection);
                    }
                }
                if (finalDirection == renderInfo_.FinalDirection) {
                    bool pressed = Input.GetMouseButton(0);
                    Color color = MainTool.GetToolColor(pressed, false);
                    uint otherLaneId = data.CurLanePos.laneId;
                    var laneMarker = new SegmentLaneMarker(otherLaneId.ToLane().m_bezier);
                    laneMarker.RenderOverlay(cameraInfo, color, enlarge: pressed);
                }

                return true;
            }

            SegmentLaneTraverser.Traverse(
                initialSegmentId: renderInfo_.SegmentId,
                direction: SegmentTraverser.TraverseDirection.AnyDirection,
                side: SegmentTraverser.TraverseSide.AnySide,
                laneStopCrit: SegmentLaneTraverser.LaneStopCriterion.LaneCount,
                segStopCrit: SegmentTraverser.SegmentStopCriterion.Junction,
                laneTypeFilter: ParkingRestrictionsManager.LANE_TYPES,
                vehicleTypeFilter: ParkingRestrictionsManager.VEHICLE_TYPES,
                laneVisitor: LaneVisitor);
        }

        public override void RenderOverlay(RenderManager.CameraInfo cameraInfo) {
            if(renderInfo_.SegmentId == 0) {
                return;
            }
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            if (shift) {
                RenderRoadParkings(cameraInfo);
            } else {
                RenderSegmentParkings(cameraInfo);
            }
        }

        public override void ShowGUIOverlay(ToolMode toolMode, bool viewOnly) {
            if (viewOnly && !SavedGameOptions.Instance.parkingRestrictionsOverlay && !MassEditOverlay.IsActive) {
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
            var currentCamera = new CameraTransformValue(InGameUtil.Instance.CachedMainCamera);
            Transform currentCameraTransform = InGameUtil.Instance.CachedCameraTransform;
            Vector3 camPos = currentCameraTransform.position;

            if (!LastCachedCamera.Equals(currentCamera) || this.forceCameraCacheReset_) {
                // cache visible segments
                LastCachedCamera = currentCamera;
                CachedVisibleSegmentIds.Clear();
                forceCameraCacheReset_ = false;

                for (uint segmentId = 1; segmentId < NetManager.MAX_SEGMENT_COUNT; ++segmentId) {
                    ref NetSegment netSegment = ref ((ushort)segmentId).ToSegment();
                    if (!netSegment.IsValid()) {
                        continue;
                    }

                    Vector3 distToCamera = netSegment.m_bounds.center - camPos;
                    if (distToCamera.sqrMagnitude > TrafficManagerTool.MAX_OVERLAY_DISTANCE_SQR) {
                        continue; // do not draw if too distant
                    }

                    bool visible = GeometryUtil.WorldToScreenPoint(netSegment.m_bounds.center, out Vector3 _);
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
                    segmentId: segmentId,
                    clicked: clicked,
                    segment: ref segmentId.ToSegment(),
                    viewOnly: viewOnly,
                    camPos: ref camPos);
                if (dir != NetInfo.Direction.None) {
                    renderInfo_.SegmentId = segmentId;
                    renderInfo_.FinalDirection = dir;
                    hovered = true;
                }
            }
            if (!hovered) {
                renderInfo_.SegmentId = 0;
                renderInfo_.FinalDirection = NetInfo.Direction.None;
            }
        }

        private NetInfo.Direction DrawParkingRestrictionHandles(ushort segmentId,
                                                   bool clicked,
                                                   ref NetSegment segment,
                                                   bool viewOnly,
                                                   ref Vector3 camPos) {
            if (viewOnly && !SavedGameOptions.Instance.parkingRestrictionsOverlay && !MassEditOverlay.IsActive) {
                return NetInfo.Direction.None;
            }

            NetManager netManager = Singleton<NetManager>.instance;
            ParkingRestrictionsManager parkingManager = ParkingRestrictionsManager.Instance;
            NetInfo.Direction hoveredDirection = NetInfo.Direction.None;

            // draw parking restriction signs over mean middle points of lane beziers
            if (!segmentCenterByDir.TryGetValue(
                    key: segmentId,
                    value: out Dictionary<NetInfo.Direction, Vector3> segCenter))
            {
                segCenter = new Dictionary<NetInfo.Direction, Vector3>();
                segmentCenterByDir.Add(segmentId, segCenter);
                GeometryUtil.CalculateSegmentCenterByDir(
                    segmentId: segmentId,
                    outputDict: segCenter,
                    minDistance: SIGN_SIZE * TrafficManagerTool.MAX_ZOOM);
            }

            foreach (KeyValuePair<NetInfo.Direction, Vector3> e in segCenter) {
                bool configurable = parkingManager.MayHaveParkingRestriction(segmentId, e.Key);
                if (!configurable) {
                    continue;
                }

                bool allowed = parkingManager.IsParkingAllowed(segmentId, e.Key);
                if (allowed && viewOnly) {
                    continue;
                }

                bool visible = GeometryUtil.WorldToScreenPoint(e.Value, out Vector3 screenPos);
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

                if (SavedGameOptions.Instance.speedLimitsOverlay || MassEditOverlay.IsActive) {
                    boundingBox.y -= size + 10f;
                }

                bool hoveredHandle = !viewOnly && TrafficManagerTool.IsMouseOver(boundingBox);

                guiColor.a = TrafficManagerTool.GetHandleAlpha(hoveredHandle);

                if (hoveredHandle) {
                    // mouse hovering over sign
                    hoveredDirection = e.Key;
                }

                GUI.color = GUI.color.WithAlpha(TrafficManagerTool.OverlayAlpha);
                GUI.DrawTexture(boundingBox, RoadSignThemeManager.ActiveTheme.Parking(allowed));
                GUI.color = guiColor;

                if (hoveredHandle && clicked && !IsCursorInPanel() &&
                    parkingManager.ToggleParkingAllowed(segmentId, hoveredDirection)) {
                    allowed = !allowed;

                    if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                    {
                        NetInfo.Direction normDir = e.Key;

                        if ((segmentId.ToSegment().m_flags & NetSegment.Flags.Invert) != NetSegment.Flags.None) {
                            normDir = NetInfo.InvertDirection(normDir);
                        }

                        bool LaneVisitor(SegmentLaneVisitData data) {
                            if (data.SegVisitData.Initial) {
                                return true;
                            }

                            bool reverse = data.SegVisitData.ViaStartNode ==
                                           data.SegVisitData.ViaInitialStartNode;

                            ushort otherSegmentId = data.SegVisitData.CurSeg.segmentId;
                            ref NetSegment otherSegment = ref otherSegmentId.ToSegment();

                            NetInfo otherSegmentInfo = otherSegment.Info;
                            byte laneIndex = data.CurLanePos.laneIndex;
                            NetInfo.Lane laneInfo = otherSegmentInfo.m_lanes[laneIndex];

                            NetInfo.Direction otherNormDir = laneInfo.m_finalDirection;
                            if (((otherSegment.m_flags &
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

        private static string T(string key) => Translation.ParkingRestrictions.Get(key);

        public void UpdateOnscreenDisplayPanel() {
            var items = new List<OsdItem>();
            items.Add(new Label(localizedText: T("Parking.OnscreenHint.Mode:Click to toggle")));
            items.Add(
                new HardcodedMouseShortcut(
                    button: UIMouseButton.Left,
                    shift: true,
                    ctrl: false,
                    alt: false,
                    localizedText: T("Parking.ShiftClick:Apply to entire road")));
            OnscreenDisplay.Display(items);
        }
    }
}