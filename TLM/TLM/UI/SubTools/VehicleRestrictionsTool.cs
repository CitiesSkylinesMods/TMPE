namespace TrafficManager.UI.SubTools {
    using ColossalFramework;
    using static Util.SegmentLaneTraverser;
    using System.Collections.Generic;
    using ColossalFramework.UI;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.Manager.Impl;
    using TrafficManager.State;
    using TrafficManager.State.Keybinds;
    using TrafficManager.UI.Textures;
    using TrafficManager.Util;
    using UnityEngine;
    using TrafficManager.UI.Helpers;
    using TrafficManager.UI.MainMenu.OSD;
    using static TrafficManager.Util.Shortcuts;
    using TrafficManager.Util.Extensions;

    public class VehicleRestrictionsTool
        : LegacySubTool,
          UI.MainMenu.IOnscreenDisplayProvider {
        private static readonly ExtVehicleType[] RoadVehicleTypes = {
            ExtVehicleType.PassengerCar,
            ExtVehicleType.Bus,
            ExtVehicleType.Taxi,
            ExtVehicleType.CargoTruck,
            ExtVehicleType.Service,
            ExtVehicleType.Emergency,
        };

        private static readonly ExtVehicleType[] RailVehicleTypes = {
            ExtVehicleType.PassengerTrain,
            ExtVehicleType.CargoTrain,
        };

        private readonly GUI.WindowFunction _guiVehicleRestrictionsWindowDelegate;

        private readonly float vehicleRestrictionsSignSize = 80f;

        private HashSet<ushort> currentRestrictedSegmentIds;

        private bool cursorInSecondaryPanel;

        private bool overlayHandleHovered;

        private RenderData renderData_;

        private Rect windowRect =
            TrafficManagerTool.GetDefaultScreenPositionForRect(new Rect(0, 0, 620, 100));

        public VehicleRestrictionsTool(TrafficManagerTool mainTool)
            : base(mainTool) {
            _guiVehicleRestrictionsWindowDelegate = GuiVehicleRestrictionsWindow;

            currentRestrictedSegmentIds = new HashSet<ushort>();
        }

        private Color HighlightColor => MainTool.GetToolColor(false, false);
        private static bool RoadMode => ShiftIsPressed;

        public void UpdateOnscreenDisplayPanel() {
            if (SelectedSegmentId == 0) {
                // Select mode
                var items = new List<OsdItem>();
                items.Add(new Label(localizedText: T("VR.OnscreenHint.Mode:Select segment")));
                OnscreenDisplay.Display(items);
            } else {
                // Modify traffic light settings
                var items = new List<OsdItem>();
                items.Add(new Label(localizedText: T("VR.OnscreenHint.Mode:Toggle restrictions")));
                items.Add(
                    item: new Shortcut(
                        keybindSetting: KeybindSettingsBase.RestoreDefaultsKey,
                        localizedText: T("VR.Label:Revert to default")));
                items.Add(OnscreenDisplay.RightClick_LeaveSegment());
                OnscreenDisplay.Display(items);
            }
        }

        private static string T(string m) => Translation.VehicleRestrictions.Get(m);

        public override void OnActivate() {
            base.OnActivate();
            cursorInSecondaryPanel = false;
            RefreshCurrentRestrictedSegmentIds();
            MainTool.RequestOnscreenDisplayUpdate();
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
                ref NetSegment netSegment = ref segmentId.ToSegment();

                if (!netSegment.IsValid()) {
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

            if (SavedGameOptions.Instance.vehicleRestrictionsOverlay) {
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
            MainTool.CheckClicked(); // consume click.
            MainTool.RequestOnscreenDisplayUpdate();
        }

        public override void OnSecondaryClickOverlay() {
            if (!IsCursorInPanel()) {
                if (SelectedSegmentId != 0) {
                    SelectedSegmentId = 0;
                    MainTool.RequestOnscreenDisplayUpdate();
                } else {
                    MainTool.SetToolMode(ToolMode.None);
                }
            }
        }

        public override void OnToolGUI(Event e) {
            base.OnToolGUI(e);

            if (SelectedSegmentId != 0) {
                cursorInSecondaryPanel = false;

                Color oldColor = GUI.color;
                GUI.color = GUI.color.WithAlpha(TrafficManagerTool.GetWindowAlpha());

                windowRect = GUILayout.Window(
                    id: 255,
                    screenRect: windowRect,
                    func: _guiVehicleRestrictionsWindowDelegate,
                    text: T("Dialog.Title:Vehicle restrictions"),
                    style: WindowStyle,
                    options: EmptyOptionsArray);
                cursorInSecondaryPanel = windowRect.Contains(Event.current.mousePosition);
                GUI.color = oldColor;
                // overlayHandleHovered = false;
            }

            if (cursorInSecondaryPanel) {
                UIInput.MouseUsed();
            }

            // ShowSigns(false);
        }

        /// <summary>
        /// highlights the given lane according.
        /// the highlight is emboldened if mouse click is pressed.
        /// </summary>
        private void RenderLaneOverlay(RenderManager.CameraInfo cameraInfo, uint laneId) {
            var marker = new SegmentLaneMarker(laneId.ToLane().m_bezier);
            bool pressed = Input.GetMouseButton(0);
            Color color = HighlightColor;
            color = pressed ? Color.magenta : color;
            marker.RenderOverlay(cameraInfo, color, pressed);
        }

        /// <summary>
        /// highlights all the lanes with the same sorted index as the current lane.
        /// </summary>
        private void RenderRoadLane(RenderManager.CameraInfo cameraInfo) {
            SegmentLaneTraverser.Traverse(
                initialSegmentId: renderData_.segmentId,
                direction: SegmentTraverser.TraverseDirection.AnyDirection,
                side: SegmentTraverser.TraverseSide.AnySide,
                laneStopCrit: SegmentLaneTraverser.LaneStopCriterion.LaneCount,
                segStopCrit: SegmentTraverser.SegmentStopCriterion.Junction,
                laneTypeFilter: SpeedLimitManager.LANE_TYPES,
                vehicleTypeFilter: SpeedLimitManager.VEHICLE_TYPES,
                laneVisitor: data => {
                    if (renderData_.SortedLaneIndex == data.SortedLaneIndex) {
                        RenderLaneOverlay(cameraInfo: cameraInfo, laneId: data.CurLanePos.laneId);
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
                Highlight.DrawSegmentOverlay(
                    cameraInfo: cameraInfo,
                    segmentId: SelectedSegmentId,
                    color: color,
                    alphaBlend: true);

                if (overlayHandleHovered) {
                    if (RoadMode) {
                        RenderRoadLane(cameraInfo);
                    } else {
                        RenderLaneOverlay(cameraInfo, renderData_.laneId);
                    }
                }
            }

            if (cursorInSecondaryPanel) {
                return;
            }

            if (HoveredSegmentId != 0 && HoveredSegmentId != SelectedSegmentId &&
                !overlayHandleHovered) {
                NetTool.RenderOverlay(
                    cameraInfo: cameraInfo,
                    segment: ref HoveredSegmentId.ToSegment(),
                    importantColor: MainTool.GetToolColor(warning: false, error: false),
                    nonImportantColor: MainTool.GetToolColor(warning: false, error: false));
            }
        }

        public override void ShowGUIOverlay(ToolMode toolMode, bool viewOnly) {
            if (viewOnly && !SavedGameOptions.Instance.vehicleRestrictionsOverlay) {
                return;
            }

            ShowSigns(viewOnly);
        }

        private void ShowSigns(bool viewOnly) {
            Vector3 camPos = InGameUtil.Instance.CachedCameraTransform.position;
            bool handleHovered = false;

            foreach (ushort segmentId in currentRestrictedSegmentIds) {
                ref NetSegment netSegment = ref segmentId.ToSegment();

                if (!netSegment.IsValid()) {
                    continue;
                }

                Vector3 centerPos = netSegment.m_bounds.center;
                bool visible = GeometryUtil.WorldToScreenPoint(centerPos, out Vector3 _);

                if (!visible) {
                    continue;
                }

                if ((netSegment.m_bounds.center - camPos).sqrMagnitude >
                    TrafficManagerTool.MAX_OVERLAY_DISTANCE_SQR) {
                    continue; // do not draw if too distant
                }

                // draw vehicle restrictions
                if (DrawVehicleRestrictionHandles(
                        segmentId: segmentId,
                        segment: ref netSegment,
                        viewOnly: viewOnly || segmentId != SelectedSegmentId,
                        stateUpdated: out bool updated)) {
                    handleHovered = true;
                }

                if (updated) {
                    break;
                }
            }

            overlayHandleHovered = handleHovered;
        }

        private void GuiVehicleRestrictionsWindow(int num) {
            // use blue color when shift is pressed.
            Color oldColor = GUI.color;
            GUI.color = GUI.color.WithAlpha(TrafficManagerTool.GetWindowAlpha());
            if (RoadMode) {
                GUI.color = HighlightColor;
            }

            // uses pressed sprite when delete is pressed
            // uses blue color when shift is pressed.
            KeyCode hotkey = KeyCode.Delete;
            GUIStyle style = new GUIStyle("button");
            if (Input.GetKey(hotkey)) {
                style.normal.background = style.active.background;
            }

            if (GUILayout.Button(
                    T("Button:Allow all vehicles") + " [delete]",
                    style,
                    EmptyOptionsArray) || Input.GetKeyDown(hotkey)) {
                AllVehiclesFunc(true);
                if (RoadMode) {
                    ApplyRestrictionsToAllSegments();
                }
            }

            if (GUILayout.Button(T("Button:Ban all vehicles"), EmptyOptionsArray)) {
                AllVehiclesFunc(false);
                if (RoadMode) {
                    ApplyRestrictionsToAllSegments();
                }
            }

            if (RoadMode) {
                GUI.color = oldColor;
            }

            if (GUILayout.Button(
                    T("Button:Apply to entire road"),
                    EmptyOptionsArray)) {
                ApplyRestrictionsToAllSegments();
            }

            GUI.color = oldColor;
            DragWindow(ref windowRect);
        }

        private void AllVehiclesFunc(bool allow) {
            // allow all vehicle types
            ref NetSegment selectedSegment = ref SelectedSegmentId.ToSegment();
            NetInfo segmentInfo = selectedSegment.Info;

            var lanes = selectedSegment.GetSortedLanes(
                null,
                VehicleRestrictionsManager.LANE_TYPES,
                VehicleRestrictionsManager.VEHICLE_TYPES,
                sort: false);

            foreach (LanePos laneData in lanes) {
                uint laneId = laneData.laneId;
                byte laneIndex = laneData.laneIndex;
                NetInfo.Lane laneInfo = segmentInfo.m_lanes[laneIndex];

                ExtVehicleType allowedTypes = allow
                                                  ? VehicleRestrictionsManager.EXT_VEHICLE_TYPES
                                                  : ExtVehicleType.None;

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
        /// copies vehicle restrictions of the current segment
        /// and applies them to all segments until the next junction.
        /// </summary>
        /// <param name="sortedLaneIndex">if provided only current lane is considered</param>
        /// <param name="vehicleTypes">
        /// if provided only bits for which vehicleTypes is set are considered.
        /// </param>
        private void ApplyRestrictionsToAllSegments(
            int? sortedLaneIndex = null,
            ExtVehicleType? vehicleTypes = null) {
            NetManager netManager = Singleton<NetManager>.instance;

            NetInfo selectedSegmentInfo = SelectedSegmentId.ToSegment().Info;

            bool LaneVisitorFun(SegmentLaneVisitData data) {
                if (data.SegVisitData.Initial) {
                    return true;
                }

                if (sortedLaneIndex != null && data.SortedLaneIndex != sortedLaneIndex) {
                    return true;
                }

                ushort segmentId = data.SegVisitData.CurSeg.segmentId;
                NetInfo segmentInfo = segmentId.ToSegment().Info;

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
                        VehicleRestrictionsMode.Configured);

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

            if (viewOnly && !SavedGameOptions.Instance.vehicleRestrictionsOverlay &&
                MainTool.GetToolMode() != ToolMode.VehicleRestrictions) {
                return false;
            }

            Vector3 center = segment.m_middlePosition;
            bool visible = GeometryUtil.WorldToScreenPoint(center, out Vector3 _);

            if (!visible) {
                return false;
            }

            Vector3 camPos = Singleton<SimulationManager>.instance.m_simulationView.m_position;
            Vector3 diff = center - camPos;

            if (diff.sqrMagnitude > TrafficManagerTool.MAX_OVERLAY_DISTANCE_SQR) {
                return false; // do not draw if too distant
            }

            int numLanes = GeometryUtil.GetSegmentNumVehicleLanes(
                segmentId: segmentId,
                nodeId: null,
                numDirections: out int numDirections,
                vehicleTypeFilter: VehicleRestrictionsManager.VEHICLE_TYPES);

            // draw vehicle restrictions over each lane
            NetInfo segmentInfo = segment.Info;
            Vector3 yu = (segment.m_endDirection - segment.m_startDirection).normalized;

            // if ((segment.m_flags & NetSegment.Flags.Invert) == NetSegment.Flags.None)
            //        yu = -yu;
            Vector3 xu = Vector3.Cross(yu, new Vector3(0, 1f, 0)).normalized;
            float signSize = viewOnly ? 4f : 7f; // reserved sign size in game coordinates
            int maxNumSigns = 0;

            if (VehicleRestrictionsManager.Instance.IsRoadSegment(segmentInfo)) {
                maxNumSigns = RoadVehicleTypes.Length;
            } else if (VehicleRestrictionsManager.Instance.IsRailSegment(segmentInfo)) {
                maxNumSigns = RailVehicleTypes.Length;
            }

            // Vector3 zero = center - 0.5f * (float)(numLanes + numDirections - 1) * f * (xu + yu); // "bottom left"
            Vector3 zero = center - (0.5f * (numLanes - 1 + numDirections - 1) * signSize * xu)
                                  - (0.5f * maxNumSigns * signSize * yu); // "bottom left"

            // if (!viewOnly)
            //     Log._Debug($"xu: {xu.ToString()} yu: {yu.ToString()} center: {center.ToString()}
            //     zero: {zero.ToString()} numLanes: {numLanes} numDirections: {numDirections}");*/

            uint x = 0;
            Color guiColor = GUI.color; // TODO: Use OverlayHandleColorController

            var sortedLanes = segment.GetSortedLanes(
                startNode: null,
                laneTypeFilter: VehicleRestrictionsManager.LANE_TYPES,
                vehicleTypeFilter: VehicleRestrictionsManager.VEHICLE_TYPES);

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
                Color guiColor2 = GUI.color; // TODO: Use OverlayHandleColorController

                GUI.color = GUI.color.WithAlpha(TrafficManagerTool.OverlayAlpha);
                var gridRenderer = new Highlight.Grid(
                    gridOrigin: zero,
                    cellWidth: signSize,
                    cellHeight: signSize,
                    xu: xu,
                    yu: yu);

                var theme = RoadSignThemeManager.ActiveTheme;

                ExtVehicleType configurableVehicleTypes = VehicleRestrictionsManager.Instance.GetConfigurableVehicleTypes(segmentInfo, laneInfo);

                foreach (ExtVehicleType vehicleType in possibleVehicleTypes) {
                    bool allowed =
                        VehicleRestrictionsManager.Instance.IsAllowed(allowedTypes, vehicleType);

                    if (allowed && viewOnly) {
                        continue; // do not draw allowed vehicles in view-only mode
                    }

                    bool configurable = configurableVehicleTypes.IsFlagSet(vehicleType);
                    Texture2D drawTex = theme.VehicleRestriction(vehicleType, allowed, disabled: !configurable);
                    // if (drawTex == null) {
                    //     drawTex = Texture2D.whiteTexture;
                    // }

                    bool hoveredHandle = gridRenderer.DrawGenericOverlayGridTexture(
                        texture: drawTex,
                        camPos: camPos,
                        x: x,
                        y: y,
                        width: this.vehicleRestrictionsSignSize,
                        height: this.vehicleRestrictionsSignSize,
                        canHover: !viewOnly && configurable,
                        screenRect: out _);

                    if (hoveredHandle) {
                        hovered = true;
                        renderData_.segmentId = segmentId;
                        renderData_.laneId = laneId;
                        renderData_.laneIndex = laneIndex;
                        renderData_.laneInfo = laneInfo;
                        renderData_.SortedLaneIndex = sortedLaneIndex;
                    }

                    if (hoveredHandle && MainTool.CheckClicked()) {
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
                        RefreshCurrentRestrictedSegmentIds(segmentId);
                        if (RoadMode) {
                            ApplyRestrictionsToAllSegments(sortedLaneIndex, vehicleType);
                        }
                    }

                    ++y;
                }

                GUI.color = guiColor2; // TODO: Use OverlayHandleColorController

                ++x;
            }

            guiColor.a = 1f;
            GUI.color = guiColor; // TODO: Use OverlayHandleColorController

            return hovered;
        }

        private struct RenderData {
            internal ushort segmentId;
            internal uint laneId;
            internal byte laneIndex;
            internal NetInfo.Lane laneInfo;
            internal int SortedLaneIndex;
            internal bool GUIButtonHovered;
        }
    }
}