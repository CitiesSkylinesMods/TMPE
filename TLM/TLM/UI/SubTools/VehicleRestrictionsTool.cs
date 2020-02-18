namespace TrafficManager.UI.SubTools {
    using ColossalFramework;
    using GenericGameBridge.Service;
    using static Util.SegmentLaneTraverser;
    using System.Collections.Generic;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.Manager.Impl;
    using TrafficManager.State;
    using TrafficManager.UI.Textures;
    using TrafficManager.Util;
    using UnityEngine;
    using TrafficManager.UI.Helpers;
    using CSUtil.Commons;

    public class VehicleRestrictionsTool : SubTool {
        private static readonly ExtVehicleType[] RoadVehicleTypes = {
            ExtVehicleType.PassengerCar, ExtVehicleType.Bus, ExtVehicleType.Taxi, ExtVehicleType.CargoTruck,
            ExtVehicleType.Service, ExtVehicleType.Emergency
        };

        private static readonly ExtVehicleType[] RailVehicleTypes = {
            ExtVehicleType.PassengerTrain, ExtVehicleType.CargoTrain
        };

        private readonly float vehicleRestrictionsSignSize = 80f;

        private bool cursorInSecondaryPanel;

        private bool overlayHandleHovered;

        private static Color roadModeColor = Color.yellow;
        private static bool roadMode => Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        private Rect windowRect = TrafficManagerTool.MoveGUI(new Rect(0, 0, 200, 100));

        private HashSet<ushort> currentRestrictedSegmentIds;

        private static string T(string m) => Translation.VehicleRestrictions.Get(m);

        private struct RenderData {
            internal ushort segmentId;
            internal uint laneId;
            internal byte laneIndex;
            internal NetInfo.Lane laneInfo;
            internal bool GUIButtonHovered;
        }
        private RenderData renderData;




        public VehicleRestrictionsTool(TrafficManagerTool mainTool)
            : base(mainTool) {
            currentRestrictedSegmentIds = new HashSet<ushort>();
        }

        public override void OnActivate() {
            cursorInSecondaryPanel = false;
            RefreshCurrentRestrictedSegmentIds();
        }

        private void RefreshCurrentRestrictedSegmentIds(ushort forceSegmentId = 0) {
            if (forceSegmentId == 0) {
                currentRestrictedSegmentIds.Clear();
            } else {
                currentRestrictedSegmentIds.Remove(forceSegmentId);
            }

            for (ushort segmentId = forceSegmentId == 0 ? (ushort)1 : forceSegmentId;
                 segmentId <= (forceSegmentId == 0
                                   ? NetManager.MAX_SEGMENT_COUNT - 1
                                   : forceSegmentId);
                 ++segmentId) {
                if (!Constants.ServiceFactory.NetService.IsSegmentValid(segmentId)) {
                    continue;
                }

                if (segmentId == SelectedSegmentId ||
                    VehicleRestrictionsManager.Instance.HasSegmentRestrictions(segmentId)) {
                    currentRestrictedSegmentIds.Add(segmentId);
                }
            }
        }

        public override void Cleanup() { }

        public override void Initialize() {
            base.Initialize();
            Cleanup();

            if (Options.vehicleRestrictionsOverlay) {
                RefreshCurrentRestrictedSegmentIds();
            } else {
                currentRestrictedSegmentIds.Clear();
            }
        }

        public override bool IsCursorInPanel() {
            return base.IsCursorInPanel() || cursorInSecondaryPanel;
        }

        public override void OnPrimaryClickOverlay() {
            // Log._Debug($"Restrictions: {HoveredSegmentId} {overlayHandleHovered}");
            if (HoveredSegmentId == 0) {
                return;
            }

            if (overlayHandleHovered) {
                return;
            }

            SelectedSegmentId = HoveredSegmentId;
            currentRestrictedSegmentIds.Add(SelectedSegmentId);
        }

        public override void OnSecondaryClickOverlay() {
            if (!IsCursorInPanel()) {
                SelectedSegmentId = 0;
            }
        }

        public override void OnToolGUI(Event e) {
            base.OnToolGUI(e);

            if (SelectedSegmentId != 0) {
                cursorInSecondaryPanel = false;

                windowRect = GUILayout.Window(
                    255,
                    windowRect,
                    GuiVehicleRestrictionsWindow,
                   T("Dialog.Title:Vehicle restrictions"),
                    WindowStyle);
                cursorInSecondaryPanel = windowRect.Contains(Event.current.mousePosition);

                // overlayHandleHovered = false;
            }

            // ShowSigns(false);
        }

        private static NetLane[] laneBuffer => NetManager.instance.m_lanes.m_buffer;

        /// <summary>
        /// highlights the given lane according.
        /// the highlight is emboldened if mouse click is pressed.
        /// </summary>
        private void RenderLaneOverlay(RenderManager.CameraInfo cameraInfo, uint laneId) {
            var marker = new SegmentLaneMarker(laneBuffer[laneId].m_bezier);
            bool pressed = Input.GetMouseButton(0);
            Color color = roadMode ? roadModeColor : Color.white;
            color = pressed ? Color.magenta : color;
            marker.RenderOverlay(cameraInfo, color, pressed);
        }

        /// <summary>
        /// highlitghts all the lanes with the same sorted index as the current lane.
        /// </summary>
        private void RenderRoadLane(RenderManager.CameraInfo cameraInfo) {
            int initialSortedLaneIndex = -1;
            SegmentLaneTraverser.Traverse(
                renderData.segmentId,
                SegmentTraverser.TraverseDirection.AnyDirection,
                SegmentTraverser.TraverseSide.AnySide,
                SegmentLaneTraverser.LaneStopCriterion.LaneCount,
                SegmentTraverser.SegmentStopCriterion.Junction,
                SpeedLimitManager.LANE_TYPES,
                SpeedLimitManager.VEHICLE_TYPES,
                data => {
                    if (data.SegVisitData.Initial &&
                        data.CurLanePos.laneIndex == renderData.laneIndex) {
                        initialSortedLaneIndex = data.SortedLaneIndex;
                    }
                    if (initialSortedLaneIndex == data.SortedLaneIndex) {
                        RenderLaneOverlay(cameraInfo, data.CurLanePos.laneId);
                    }
                    return true;
                });
        }

        public override void RenderOverlay(RenderManager.CameraInfo cameraInfo) {
            // Log._Debug($"Restrictions overlay {_cursorInSecondaryPanel} {HoveredNodeId} {SelectedNodeId} {HoveredSegmentId} {SelectedSegmentId}");
            if (SelectedSegmentId != 0) {
                Color color = MainTool.GetToolColor(true, false);
                // continues lane highlight requires lane alphaBlend == false.
                // for such lane highlight to be on the top of segment highlight,
                // the alphaBlend of segment highlight needs to be true.
                TrafficManagerTool.DrawSegmentOverlay(cameraInfo, SelectedSegmentId, color, true);

                if (overlayHandleHovered) {
                    if (!roadMode) {
                        RenderLaneOverlay(cameraInfo, renderData.laneId);
                    } else {
                        RenderRoadLane(cameraInfo);
                    }
                }
            }

            if (cursorInSecondaryPanel) {
                return;
            }

            if (HoveredSegmentId != 0 && HoveredSegmentId != SelectedSegmentId &&
                !overlayHandleHovered) {
                NetTool.RenderOverlay(
                    cameraInfo,
                    ref Singleton<NetManager>.instance.m_segments.m_buffer[HoveredSegmentId],
                    MainTool.GetToolColor(false, false),
                    MainTool.GetToolColor(false, false));
            }
        }

        public override void ShowGUIOverlay(ToolMode toolMode, bool viewOnly) {
            if (viewOnly && !Options.vehicleRestrictionsOverlay) {
                return;
            }

            ShowSigns(viewOnly);
        }

        private void ShowSigns(bool viewOnly) {
            Vector3 camPos = Camera.main.transform.position;
            NetManager netManager = Singleton<NetManager>.instance;
            ushort updatedSegmentId = 0;
            bool handleHovered = false;

            foreach (ushort segmentId in currentRestrictedSegmentIds) {
                if (!Constants.ServiceFactory.NetService.IsSegmentValid(segmentId)) {
                    continue;
                }

                Vector3 centerPos = netManager.m_segments.m_buffer[segmentId].m_bounds.center;
                bool visible = MainTool.WorldToScreenPoint(centerPos, out Vector3 _);

                if (!visible) {
                    continue;
                }

                if ((netManager.m_segments.m_buffer[segmentId].m_bounds.center - camPos).sqrMagnitude >
                    TrafficManagerTool.MAX_OVERLAY_DISTANCE_SQR) {
                    continue; // do not draw if too distant
                }

                // draw vehicle restrictions
                if (DrawVehicleRestrictionHandles(
                    segmentId,
                    ref netManager.m_segments.m_buffer[segmentId],
                    viewOnly || segmentId != SelectedSegmentId,
                    out bool update)) {
                    handleHovered = true;
                }

                if (update) {
                    updatedSegmentId = segmentId;
                }
            }

            overlayHandleHovered = handleHovered;

            if (updatedSegmentId != 0) {
                RefreshCurrentRestrictedSegmentIds(updatedSegmentId);
            }
        }

        private void GuiVehicleRestrictionsWindow(int num) {
            // use yellow color when shift is pressed.
            Color oldColor = GUI.color;
            if (roadMode) {
                GUI.color = roadModeColor;
            }

            {
                // uses pressed sprite when delete is pressed
                // uses yellow color when shift is pressed.
                KeyCode hotkey = KeyCode.Delete;
                GUIStyle style = new GUIStyle("button");
                if (Input.GetKey(hotkey)) {
                    style.normal.background = style.active.background;
                }
                if (GUILayout.Button(
                   T("Button:Allow all vehicles") + " [delete]",
                   style) || Input.GetKeyDown(hotkey)) {
                    AllVehiclesFunc(true);
                    if (roadMode) {
                        ApplyRestrictionsToAllSegments();
                    }
                }
            }

            if (GUILayout.Button(Translation.VehicleRestrictions.Get("Button:Ban all vehicles"))) {
                AllVehiclesFunc(false);
                if (roadMode) {
                    ApplyRestrictionsToAllSegments();
                }
            }
          
            GUI.color = oldColor;

            if (GUILayout.Button(
               T("Button:Apply to entire road"))) {
                ApplyRestrictionsToAllSegments();
            }

            DragWindow(ref windowRect);
        }


        private void AllVehiclesFunc(bool allow) {
            // allow all vehicle types
            NetInfo segmentInfo = SelectedSegmentId.ToSegment().Info;

            IList<LanePos> lanes = Constants.ServiceFactory.NetService.GetSortedLanes(
                SelectedSegmentId,
                ref SelectedSegmentId.ToSegment(),
                null,
                VehicleRestrictionsManager.LANE_TYPES,
                VehicleRestrictionsManager.VEHICLE_TYPES,
                sort:false);

            foreach (LanePos laneData in lanes) {
                uint laneId = laneData.laneId;
                byte laneIndex = laneData.laneIndex;
                NetInfo.Lane laneInfo = segmentInfo.m_lanes[laneIndex];

                ExtVehicleType allowedTypes =
                    allow ?
                    VehicleRestrictionsManager.EXT_VEHICLE_TYPES :
                    ExtVehicleType.None;

                VehicleRestrictionsManager.Instance.SetAllowedVehicleTypes(
                    SelectedSegmentId,
                    segmentInfo,
                    laneIndex,
                    laneInfo,
                    laneId,
                    allowedTypes);
            }

            RefreshCurrentRestrictedSegmentIds(SelectedSegmentId);
        }

        /// <summary>
        /// coppies vehicle restrictions of the current segment
        /// and applies them to all segments until the next junction.
        /// </summary>
        /// <param name="sortedLaneIndex">if provided only current lane is considered</param>
        /// <param name="vehicleTypes">
        /// if provided only bits for which vehicleTypes is set are considered.
        /// </param>
        private void ApplyRestrictionsToAllSegments(
            int? sortedLaneIndex = null,
            ExtVehicleType ?vehicleTypes = null) {
            NetManager netManager = Singleton<NetManager>.instance;

            NetInfo selectedSegmentInfo = netManager.m_segments.m_buffer[SelectedSegmentId].Info;

            bool LaneVisitorFun(SegmentLaneVisitData data) {
                if (data.SegVisitData.Initial) {
                    return true;
                }

                if (sortedLaneIndex != null && data.SortedLaneIndex != sortedLaneIndex) {
                    return true;
                }

                ushort segmentId = data.SegVisitData.CurSeg.segmentId;
                NetInfo segmentInfo = netManager.m_segments.m_buffer[segmentId].Info;

                byte selectedLaneIndex = data.InitLanePos.laneIndex;
                NetInfo.Lane selectedLaneInfo = selectedSegmentInfo.m_lanes[selectedLaneIndex];

                uint laneId = data.CurLanePos.laneId;
                byte laneIndex = data.CurLanePos.laneIndex;
                NetInfo.Lane laneInfo = segmentInfo.m_lanes[laneIndex];

                // apply restrictions of selected segment & lane
                ExtVehicleType mask =
                    VehicleRestrictionsManager.Instance.GetAllowedVehicleTypes(
                        SelectedSegmentId,
                        selectedSegmentInfo,
                        selectedLaneIndex,
                        selectedLaneInfo,
                        VehicleRestrictionsMode.Configured); ;
                if (vehicleTypes != null) {
                    ExtVehicleType currentMask =
                        VehicleRestrictionsManager.Instance.GetAllowedVehicleTypes(
                            segmentId,
                            segmentInfo,
                            laneIndex,
                            laneInfo,
                            VehicleRestrictionsMode.Configured);

                    // only apply changes where types is 1. that means:
                    // for bits where types is 0, use currentMask,
                    // for bits where types is 1, use initial mask.
                    ExtVehicleType types2 = (ExtVehicleType)vehicleTypes; //cast
                    mask = (types2 & mask) | (~types2 & currentMask);
                }

                VehicleRestrictionsManager.Instance.SetAllowedVehicleTypes(
                    segmentId,
                    segmentInfo,
                    laneIndex,
                    laneInfo,
                    laneId,
                    mask);

                RefreshCurrentRestrictedSegmentIds(segmentId);

                return true;
            }

            SegmentLaneTraverser.Traverse(
                SelectedSegmentId,
                SegmentTraverser.TraverseDirection.AnyDirection,
                SegmentTraverser.TraverseSide.AnySide,
                SegmentLaneTraverser.LaneStopCriterion.LaneCount,
                SegmentTraverser.SegmentStopCriterion.Junction,
                VehicleRestrictionsManager.LANE_TYPES,
                VehicleRestrictionsManager.VEHICLE_TYPES,
                LaneVisitorFun);
        }

        private bool DrawVehicleRestrictionHandles(ushort segmentId,
                                                   ref NetSegment segment,
                                                   bool viewOnly,
                                                   out bool stateUpdated) {
            stateUpdated = false;

            if (viewOnly && !Options.vehicleRestrictionsOverlay &&
                MainTool.GetToolMode() != ToolMode.VehicleRestrictions) {
                return false;
            }

            Vector3 center = segment.m_bounds.center;

            bool visible = MainTool.WorldToScreenPoint(center, out Vector3 _);

            if (!visible) {
                return false;
            }

            Vector3 camPos = Singleton<SimulationManager>.instance.m_simulationView.m_position;
            Vector3 diff = center - camPos;

            if (diff.sqrMagnitude > TrafficManagerTool.MAX_OVERLAY_DISTANCE_SQR) {
                return false; // do not draw if too distant
            }

            int numLanes = TrafficManagerTool.GetSegmentNumVehicleLanes(
                segmentId,
                null,
                out int numDirections,
                VehicleRestrictionsManager.VEHICLE_TYPES);

            // draw vehicle restrictions over each lane
            NetInfo segmentInfo = segment.Info;
            Vector3 yu = (segment.m_endDirection - segment.m_startDirection).normalized;

            // if ((segment.m_flags & NetSegment.Flags.Invert) == NetSegment.Flags.None)
            //        yu = -yu;
            Vector3 xu = Vector3.Cross(yu, new Vector3(0, 1f, 0)).normalized;
            float f = viewOnly ? 4f : 7f; // reserved sign size in game coordinates
            int maxNumSigns = 0;

            if (VehicleRestrictionsManager.Instance.IsRoadSegment(segmentInfo)) {
                maxNumSigns = RoadVehicleTypes.Length;
            } else if (VehicleRestrictionsManager.Instance.IsRailSegment(segmentInfo)) {
                maxNumSigns = RailVehicleTypes.Length;
            }

            // Vector3 zero = center - 0.5f * (float)(numLanes + numDirections - 1) * f * (xu + yu); // "bottom left"
            Vector3 zero = center - (0.5f * (numLanes - 1 + numDirections - 1) * f * xu)
                                  - (0.5f * maxNumSigns * f * yu); // "bottom left"

            // if (!viewOnly)
            //     Log._Debug($"xu: {xu.ToString()} yu: {yu.ToString()} center: {center.ToString()}
            //     zero: {zero.ToString()} numLanes: {numLanes} numDirections: {numDirections}");*/

            uint x = 0;
            Color guiColor = GUI.color;
            IList<LanePos> sortedLanes = Constants.ServiceFactory.NetService.GetSortedLanes(
                segmentId,
                ref segment,
                null,
                VehicleRestrictionsManager.LANE_TYPES,
                VehicleRestrictionsManager.VEHICLE_TYPES);
            bool hovered = false;
            HashSet<NetInfo.Direction> directions = new HashSet<NetInfo.Direction>();
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

                ExtVehicleType[] possibleVehicleTypes;

                if (VehicleRestrictionsManager.Instance.IsRoadLane(laneInfo)) {
                    possibleVehicleTypes = RoadVehicleTypes;
                } else if (VehicleRestrictionsManager.Instance.IsRailLane(laneInfo)) {
                    possibleVehicleTypes = RailVehicleTypes;
                } else {
                    ++x;
                    continue;
                }

                ExtVehicleType allowedTypes =
                    VehicleRestrictionsManager.Instance.GetAllowedVehicleTypes(
                        segmentId,
                        segmentInfo,
                        laneIndex,
                        laneInfo,
                        VehicleRestrictionsMode.Configured);

                uint y = 0;
#if DEBUG_disabled_xxx
                Vector3 labelCenter = zero + f * (float)x * xu + f * (float)y * yu; // in game coordinates

                Vector3 labelScreenPos;
                bool visible = MainTool.WorldToScreenPoint(labelCenter, out labelScreenPos);
                labelScreenPos.y = Screen.height - labelScreenPos.y;
                diff = labelCenter - camPos;

                var labelZoom = 1.0f / diff.magnitude * 100f;
                _counterStyle.fontSize = (int)(11f * labelZoom);
                _counterStyle.normal.textColor = new Color(1f, 1f, 0f);

                string labelStr = $"Idx {laneIndex}";
                Vector2 dim = _counterStyle.CalcSize(new GUIContent(labelStr));
                Rect labelRect = new Rect(labelScreenPos.x - dim.x / 2f, labelScreenPos.y, dim.x, dim.y);
                GUI.Label(labelRect, labelStr, _counterStyle);

                ++y;
#endif
                foreach (ExtVehicleType vehicleType in possibleVehicleTypes) {
                    bool allowed = VehicleRestrictionsManager.Instance.IsAllowed(allowedTypes, vehicleType);

                    if (allowed && viewOnly) {
                        continue; // do not draw allowed vehicles in view-only mode
                    }

                    bool hoveredHandle = MainTool.DrawGenericSquareOverlayGridTexture(
                        RoadUI.VehicleRestrictionTextures[vehicleType][allowed],
                        camPos,
                        zero,
                        f,
                        xu,
                        yu,
                        x,
                        y,
                        vehicleRestrictionsSignSize,
                        !viewOnly);

                    if (hoveredHandle) {
                        hovered = true;
                        renderData.segmentId = segmentId;
                        renderData.laneId = laneId;
                        renderData.laneIndex = laneIndex;
                        renderData.laneInfo = laneInfo;

                    }

                    if (hoveredHandle && MainTool.CheckClicked() ) {
                        // toggle vehicle restrictions
                        // Log._Debug($"Setting vehicle restrictions of segment {segmentId}, lane
                        //     idx {laneIndex}, {vehicleType.ToString()} to {!allowed}");
                        VehicleRestrictionsManager.Instance.ToggleAllowedType(
                            segmentId,
                            segmentInfo,
                            laneIndex,
                            laneId,
                            laneInfo,
                            vehicleType,
                            !allowed);
                        stateUpdated = true;

                        if (roadMode) {
                            ApplyRestrictionsToAllSegments(sortedLaneIndex, vehicleType);
                        }
                    }

                    ++y;
                }

                ++x;
            }

            guiColor.a = 1f;
            GUI.color = guiColor;

            return hovered;
        }
    }
}
