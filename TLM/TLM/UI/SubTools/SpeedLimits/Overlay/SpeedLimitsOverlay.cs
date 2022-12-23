namespace TrafficManager.UI.SubTools.SpeedLimits.Overlay {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using ColossalFramework;
    using JetBrains.Annotations;
    using TrafficManager.API.Traffic.Data;
    using TrafficManager.Manager.Impl;
    using TrafficManager.State;
    using TrafficManager.Traffic;
    using TrafficManager.UI.Helpers;
    using TrafficManager.UI.Textures;
    using TrafficManager.Util;
    using TrafficManager.Util.Caching;
    using TrafficManager.Util.Extensions;
    using UnityEngine;

    /// <summary>
    /// Stores rendering state for Speed Limits overlay and provides rendering of speed limit signs
    /// overlay for segments/lanes.
    /// </summary>
    public class SpeedLimitsOverlay {
        private TrafficManagerTool mainTool_;

        // private ushort segmentId_;
        private NetInfo.Direction finalDirection_ = NetInfo.Direction.None;

        /// <summary>Used to pass options to the overlay rendering.</summary>
        public class DrawArgs {
            /// <summary>If not null, contains mouse position. Null means mouse is over some GUI window.</summary>+
            public Vector2? Mouse;

            /// <summary>List of UI frame rectangles which will make the signs fade if rendered over.</summary>
            public List<Rect> UiWindowRects;

            /// <summary>Set to true to allow bigger and clickable road signs.</summary>
            public bool IsInteractive;

            /// <summary>
            /// User is holding Shift to edit multiple segments.
            /// Set to true when operating entire road between two junctions.
            /// </summary>
            public bool MultiSegmentMode;

            /// <summary>Choose what to display (hold Alt to display something else).</summary>
            public SpeedlimitsToolMode ToolMode;

            /// <summary>Hovered SEGMENT speed limit handles (output after rendering).</summary>
            public List<OverlaySegmentSpeedlimitHandle> HoveredSegmentHandles;

            /// <summary>Previous frame data for blue overlay.</summary>
            internal List<OverlaySegmentSpeedlimitHandle> PrevHoveredSegmentHandles = new();

            /// <summary>Hovered LANE speed limit handles (output after rendering).</summary>
            public List<OverlayLaneSpeedlimitHandle> HoveredLaneHandles;

            /// <summary>Previous frame data for blue overlay.</summary>
            internal List<OverlayLaneSpeedlimitHandle> PrevHoveredLaneHandles = new();

            public static DrawArgs Create() {
                return new() {
                    UiWindowRects = new List<Rect>(),
                    IsInteractive = false,
                    MultiSegmentMode = false,
                    ToolMode = SpeedlimitsToolMode.Segments,
                    HoveredSegmentHandles = new(),
                    HoveredLaneHandles = new(),
                };
            }

            public void ClearHovered() {
                this.PrevHoveredSegmentHandles = this.HoveredSegmentHandles;
                this.HoveredSegmentHandles = new();

                this.PrevHoveredLaneHandles = this.HoveredLaneHandles;
                this.HoveredLaneHandles = new();
            }

            public bool IntersectsAnyUIRect(Rect testRect) {
                return this.UiWindowRects.Any(testRect.Overlaps);
            }
        }

        /// <summary>Environment for rendering multiple signs, to avoid creating same data over and over
        /// and to carry drawing state between multiple calls without using class fields.</summary>
        private class DrawEnv {
            public Vector2 signsThemeAspectRatio_;
            public RoadSignTheme largeSignsTextures_;

            /// <summary>
            /// This is set to true if the user will see blue default signs, or the user is holding
            /// Alt to see blue signs temporarily. Holding Alt while default signs are shown, will
            /// show segment speeds instead.
            /// </summary>
            public bool drawDefaults_;

            public float baseScreenSizeForSign_;
        }

        private struct CachedSegment {
            public ushort id_;
            public Vector3 center_;
        }

        /// <summary>
        /// Stores potentially visible segment ids while the camera did not move.
        /// </summary>
        [NotNull]
        private readonly GenericArrayCache<CachedSegment> cachedVisibleSegmentIds_;

        /// <summary>If set to true, prompts one-time cache reset.</summary>
        private bool resetCacheFlag_ = false;

        /// <summary>Stores last cached camera position in <see cref="cachedVisibleSegmentIds_"/>.</summary>
        private CameraTransformValue lastCachedCamera_;

        private bool lastUndergroundMode_ = TrafficManagerTool.IsUndergroundMode;

        private const float SPEED_LIMIT_SIGN_SIZE = 70f;

        /// <summary>Cached segment centers.</summary>
        private readonly Dictionary<ushort, Vector3> segmentCenters_ = new();

        public SpeedLimitsOverlay(TrafficManagerTool mainTool) {
            this.mainTool_ = mainTool;
            this.cachedVisibleSegmentIds_ = new GenericArrayCache<CachedSegment>(NetManager.MAX_SEGMENT_COUNT);
            this.lastCachedCamera_ = new CameraTransformValue();
        }

        /// <summary>Displays non-sign overlays, like lane highlights.</summary>
        /// <param name="cameraInfo">The camera.</param>
        /// <param name="args">The state of the parent <see cref="SpeedLimitsTool"/>.</param>
        public void RenderBlueOverlays(RenderManager.CameraInfo cameraInfo,
                                       [NotNull] DrawArgs args) {
            switch (args.ToolMode) {
                // In segments mode, highlight the hovered segment
                // In defaults mode, same, affects the hovered segment (but also all roads of that type)
                case SpeedlimitsToolMode.Segments:
                case SpeedlimitsToolMode.Defaults: {
                    // Prevent rendering twice if two signs visually overlap and mouse hovers over both
                    HashSet<ushort> uniqueSegmentIds = new();

                    foreach (var hovered in args.PrevHoveredSegmentHandles) {
                        uniqueSegmentIds.Add(hovered.SegmentId);
                    }

                    foreach (ushort hoveredSegmentId in uniqueSegmentIds) {
                        this.RenderBlueOverlays_Segment(cameraInfo, hoveredSegmentId, args);
                    }

                    break;
                }

                case SpeedlimitsToolMode.Lanes: {
                    foreach (var hovered in args.PrevHoveredLaneHandles) {
                        this.RenderBlueOverlays_HoveredLane(cameraInfo, hovered, args);
                    }

                    break;
                }
            }
        }

        /// <summary>Render segment overlay (this is curves, not the signs).</summary>
        /// <param name="cameraInfo">The camera.</param>
        /// <param name="segmentId">The segment to draw, comes from args.Hovered....</param>
        /// <param name="args">The state of the parent <see cref="SpeedLimitsTool"/>.</param>
        private void RenderBlueOverlays_Segment(RenderManager.CameraInfo cameraInfo,
                                                ushort segmentId,
                                                [NotNull] DrawArgs args) {
            //------------------------
            // Single segment highlight. User is NOT holding Shift.
            //------------------------
            if (!args.MultiSegmentMode) {
                this.RenderBlueOverlays_SegmentLanes(
                    cameraInfo: cameraInfo,
                    segmentId: segmentId,
                    args: args,
                    finalDirection: this.finalDirection_);
                return;
            }

            //------------------------
            // Entire street highlight. User is holding Shift.
            //------------------------
            if (RoundaboutMassEdit.Instance.TraverseLoop(
                segmentId: segmentId,
                segList: out var segmentList)) {
                foreach (ushort continuedRoadSegmentId in segmentList) {
                    this.RenderBlueOverlays_SegmentLanes(
                        cameraInfo: cameraInfo,
                        segmentId: continuedRoadSegmentId,
                        args: args);
                }
            } else {
                SegmentTraverser.Traverse(
                    initialSegmentId: segmentId,
                    direction: SegmentTraverser.TraverseDirection.AnyDirection,
                    side: SegmentTraverser.TraverseSide.AnySide,
                    stopCrit: SegmentTraverser.SegmentStopCriterion.Junction,
                    visitorFun: data => {
                        NetInfo.Direction finalDirection = this.finalDirection_;

                        if (data.IsReversed(segmentId)) {
                            finalDirection = NetInfo.InvertDirection(finalDirection);
                        }

                        this.RenderBlueOverlays_SegmentLanes(
                            cameraInfo: cameraInfo,
                            segmentId: data.CurSeg.segmentId,
                            args: args,
                            finalDirection: finalDirection);
                        return true;
                    });
            }
        }

        /// <summary>Render lane overlay for hovered lane, and if Shift is held, highlight entire street.</summary>
        private void RenderBlueOverlays_HoveredLane(RenderManager.CameraInfo cameraInfo,
                                                    OverlayLaneSpeedlimitHandle hovered,
                                                    [NotNull]
                                                    DrawArgs args) {
            if (!args.MultiSegmentMode) {
                this.RenderBlueOverlays_Lane(cameraInfo, hovered.LaneId, args);
                return;
            }

            var segmentId = hovered.LaneId.ToLane().m_segment;

            if (RoundaboutMassEdit.Instance.TraverseLoop(segmentId, out var segmentList)) {
                var lanes = hovered.FollowRoundaboutLane(
                    segmentList: segmentList,
                    segmentId0: segmentId,
                    sortedLaneIndex: hovered.SortedLaneIndex);
                foreach (var lane in lanes) {
                    this.RenderBlueOverlays_Lane(cameraInfo, lane.laneId, args);
                }
            } else {
                bool LaneVisitorFun(SegmentLaneTraverser.SegmentLaneVisitData data) {
                    if (data.SortedLaneIndex == hovered.SortedLaneIndex) {
                        this.RenderBlueOverlays_Lane(cameraInfo, data.CurLanePos.laneId, args);
                    }

                    return true;
                }

                SegmentLaneTraverser.Traverse(
                    initialSegmentId: segmentId,
                    direction: SegmentTraverser.TraverseDirection.AnyDirection,
                    side: SegmentTraverser.TraverseSide.AnySide,
                    laneStopCrit: SegmentLaneTraverser.LaneStopCriterion.LaneCount,
                    segStopCrit: SegmentTraverser.SegmentStopCriterion.Junction,
                    laneTypeFilter: SpeedLimitManager.LANE_TYPES,
                    vehicleTypeFilter: SpeedLimitManager.VEHICLE_TYPES,
                    laneVisitor: LaneVisitorFun);
            }
        }

        /// <summary>
        /// Renders all lane curves with the given <paramref name="finalDirection"/>
        /// if NetInfo.Direction.None, all lanes are rendered.
        /// </summary>
        private void RenderBlueOverlays_SegmentLanes(
            RenderManager.CameraInfo cameraInfo,
            ushort segmentId,
            DrawArgs args,
            NetInfo.Direction finalDirection = NetInfo.Direction.None)
        {
            ref NetSegment netSegment = ref segmentId.ToSegment();
            ExtSegmentManager extSegmentManager = ExtSegmentManager.Instance;

            foreach (var laneIdAndIndex in extSegmentManager.GetSegmentLaneIdsAndLaneIndexes(segmentId)) {
                NetInfo.Lane laneInfo = netSegment.Info.m_lanes[laneIdAndIndex.laneIndex];

                if ((laneInfo.m_laneType & SpeedLimitManager.LANE_TYPES) == 0) {
                    continue;
                }

                if ((laneInfo.m_vehicleType & SpeedLimitManager.VEHICLE_TYPES) == 0) {
                    continue;
                }

                if (laneInfo.m_finalDirection != finalDirection && finalDirection != NetInfo.Direction.Both) {
                    continue;
                }

                RenderBlueOverlays_Lane(cameraInfo, laneIdAndIndex.laneId, args);
            }
        }

        /// <summary>Draw blue lane curves overlay.</summary>
        /// <param name="cameraInfo">The Camera.</param>
        /// <param name="laneId">The lane.</param>
        /// <param name="args">The state of the parent <see cref="SpeedLimitsTool"/>.</param>
        private void RenderBlueOverlays_Lane(RenderManager.CameraInfo cameraInfo,
                                             uint laneId,
                                             [NotNull] DrawArgs args) {
            SegmentLaneMarker marker = new SegmentLaneMarker(laneId.ToLane().m_bezier);
            bool pressed = Input.GetMouseButton(0);
            Color color = this.mainTool_.GetToolColor(warning: pressed, error: false);

            if (args.ToolMode == SpeedlimitsToolMode.Lanes) {
                marker.Size = 3f; // lump the lanes together.
            }

            marker.RenderOverlay(cameraInfo, color, pressed);
        }

        /// <summary>Called by the parent tool on activation. Reset the cached segments cache and
        /// camera cache.</summary>
        public void ResetCache() {
            this.resetCacheFlag_ = true;
        }

        /// <summary>
        /// Draw speed limit signs (only in GUI mode).
        /// NOTE: This must be called from GUI mode, because of GUI.DrawTexture use.
        /// Render the speed limit signs based on the current settings.
        /// </summary>
        /// <param name="args">The state of the parent <see cref="SpeedLimitsTool"/>.</param>
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

            // TODO: Can road network change while speed limit tool is active? Disasters?
            if (this.resetCacheFlag_
                || !this.lastCachedCamera_.Equals(currentCamera)
                || this.lastUndergroundMode_ != TrafficManagerTool.IsUndergroundMode) {
                this.lastCachedCamera_ = currentCamera;
                this.resetCacheFlag_ = false;
                this.lastUndergroundMode_ = TrafficManagerTool.IsUndergroundMode;

                this.ShowSigns_RefreshVisibleSegmentsCache(
                    netManager: netManager,
                    camPos: camPos,
                    speedLimitManager: speedLimitManager);
            }

            bool hover = false;
            DrawEnv drawEnv = new DrawEnv {
                signsThemeAspectRatio_ = RoadSignThemeManager.ActiveTheme.GetAspectRatio(),
                largeSignsTextures_ = args.ToolMode switch {
                    SpeedlimitsToolMode.Segments => RoadSignThemeManager.ActiveTheme,
                    SpeedlimitsToolMode.Lanes => RoadSignThemeManager.ActiveTheme,

                    // Defaults can show normal textures if the user holds Alt
                    SpeedlimitsToolMode.Defaults => args.ToolMode == SpeedlimitsToolMode.Defaults
                                                        ? RoadSignThemeManager.ActiveTheme
                                                        : RoadSignThemeManager.Instance.SpeedLimitDefaults,
                    _ => throw new ArgumentOutOfRangeException(),
                },
                drawDefaults_ = args.ToolMode == SpeedlimitsToolMode.Defaults,
                baseScreenSizeForSign_ = Constants.OverlaySignVisibleSize,
            };

            for (int segmentIdIndex = this.cachedVisibleSegmentIds_.Size - 1;
                 segmentIdIndex >= 0;
                 segmentIdIndex--) {
                ref CachedSegment cachedSeg = ref this.cachedVisibleSegmentIds_.Values[segmentIdIndex];

                // If VehicleRestrictions tool is active, skip drawing the current selected segment
                if (this.mainTool_.GetToolMode() == ToolMode.VehicleRestrictions
                    && cachedSeg.id_ == TrafficManagerTool.SelectedSegmentId) {
                    continue;
                }

                if (args.ToolMode == SpeedlimitsToolMode.Lanes && !drawEnv.drawDefaults_) {
                    // in defaults mode separate lanes don't make any sense, so show segments at all times
                    hover |= this.DrawSpeedLimitHandles_PerLane(
                        segmentId: cachedSeg.id_,
                        segmentCenterPos: cachedSeg.center_,
                        camPos: camPos,
                        drawEnv: drawEnv,
                        args: args);
                } else {
                    // Both segment speed limits and default speed limits are displayed in the same way
                    hover |= this.DrawSpeedLimitHandles_PerSegment(
                        segmentId: cachedSeg.id_,
                        segCenter: cachedSeg.center_,
                        camPos: camPos,
                        drawEnv: drawEnv,
                        args: args);
                }
            }
        }

        /// <summary>
        /// When camera position has changed and cached segments set is invalid, scan all segments
        /// again and remember those visible in the camera frustum.
        /// </summary>
        /// <param name="netManager">Access to map data.</param>
        /// <param name="camPos">Camera position to consider.</param>
        /// <param name="speedLimitManager">Query if a segment is eligible for speed limits.</param>
        private void ShowSigns_RefreshVisibleSegmentsCache(NetManager netManager,
                                                           Vector3 camPos,
                                                           SpeedLimitManager speedLimitManager) {
            // cache visible segments
            this.cachedVisibleSegmentIds_.Clear();
            this.segmentCenters_.Clear();

            for (uint segmentId = 1; segmentId < NetManager.MAX_SEGMENT_COUNT; ++segmentId) {
                ref NetSegment segment = ref ((ushort)segmentId).ToSegment();

                // Ignore: Bad segments
                if (!segment.IsValid()) {
                    continue;
                }

                // Ignore: Can't have speed limits set
                if (!segment.MayHaveCustomSpeedLimits()) {
                    continue;
                }

                // Ignore: Underground segments only can be seen in underground mode
                if (segment.IsBothEndsUnderground() != this.lastUndergroundMode_) {
                    continue;
                }

                {
                    Vector3 distToCamera = segment.m_bounds.center - camPos;

                    // Ignore: Too far segments
                    if (distToCamera.sqrMagnitude > TrafficManagerTool.MAX_OVERLAY_DISTANCE_SQR) {
                        continue; // do not draw if too distant
                    }
                }

                {
                    // Ignore: Not in screen segments
                    bool visible = GeometryUtil.WorldToScreenPoint(
                        worldPos: segment.m_bounds.center,
                        screenPos: out Vector3 _);

                    if (!visible) {
                        continue;
                    }
                }

                // Place this check last as it might be expensive
                if (!SpeedLimitManager.Instance.IsCustomisable(segment.Info)) {
                    continue;
                }

                this.cachedVisibleSegmentIds_.Add(
                    new CachedSegment {
                        id_ = (ushort)segmentId,
                        center_ = segment.GetCenter(),
                    });
            } // end for all segments
        }

        /// <summary>
        /// Render speed limit handles one per segment, both directions averaged, and if the speed
        /// limits on the directions don't match, extra small speed limit icons are added.
        /// </summary>
        /// <param name="segmentId">Seg id.</param>
        /// <param name="segCenter">Bezier center for the segment to draw at.</param>
        /// <param name="camPos">Camera.</param>
        /// <param name="args">Render args.</param>
        private bool DrawSpeedLimitHandles_PerSegment(ushort segmentId,
                                                      Vector3 segCenter,
                                                      Vector3 camPos,
                                                      [NotNull] DrawEnv drawEnv,
                                                      [NotNull] DrawArgs args) {
            // Default signs are round, mph/kmph textures can be round or rectangular
            var colorController = new OverlayHandleColorController(args.IsInteractive);

            //--------------------------
            // For all segments visible
            //--------------------------
            bool visible = GeometryUtil.WorldToScreenPoint(worldPos: segCenter, screenPos: out Vector3 screenPos);

            bool ret = visible && DrawSpeedLimitHandles_SegmentCenter(
                    segmentId,
                    segCenter,
                    camPos,
                    screenPos,
                    colorController,
                    drawEnv,
                    args);

            colorController.RestoreGUIColor();
            return ret;
        }

        private bool DrawSpeedLimitHandles_SegmentCenter(
            ushort segmentId,
            Vector3 segCenter,
            Vector3 camPos,
            Vector3 screenPos,
            OverlayHandleColorController colorController,
            [NotNull] DrawEnv drawEnv,
            [NotNull] DrawArgs args)
        {
            Vector2 aspectRatio = drawEnv.drawDefaults_
                                     ? RoadSignThemeManager.DefaultSpeedlimitsAspectRatio()
                                     : drawEnv.signsThemeAspectRatio_;

            // TODO: Replace formula in visibleScale and size to use Constants.OVERLAY_INTERACTIVE_SIGN_SIZE and OVERLAY_READONLY_SIGN_SIZE
            float visibleScale = drawEnv.baseScreenSizeForSign_ / (segCenter - camPos).magnitude;
            float size = (args.IsInteractive ? 1f : 0.8f) * SPEED_LIMIT_SIGN_SIZE * visibleScale;

            SignRenderer signRenderer = default;
            SignRenderer squareSignRenderer = default;

            ref NetSegment segment = ref segmentId.ToSegment();
            bool forceSlowDriving = SpeedLimitManager.IsInSlowDrivingDistrict(ref segment);

            // Recalculate visible rect for screen position and size
            Rect signScreenRect = signRenderer.Reset(screenPos, size: size * aspectRatio);
            bool isHoveredHandle = !forceSlowDriving && args.IsInteractive && signRenderer.ContainsMouse(args.Mouse);

            //-----------
            // Rendering
            //-----------
            // Sqrt(visibleScale) makes fade start later as distance grows
            colorController.SetGUIColor(
                hovered: isHoveredHandle,
                intersectsGuiWindows: args.IntersectsAnyUIRect(signScreenRect),
                opacityMultiplier: 1.0f); // Mathf.Sqrt(visibleScale) for fade

            NetInfo neti = segment.Info;
            var defaultSpeedLimit = new SpeedValue(
                gameUnits: forceSlowDriving ? SpeedValue.FromKmph(20).GameUnits : SpeedLimitManager.Instance.CalculateCustomNetinfoSpeedLimit(info: neti));

            // Render override if interactive, or if readonly info layer and override exists
            if (drawEnv.drawDefaults_) {
                //-------------------------------------
                // Draw default blue speed limit
                //-------------------------------------
                squareSignRenderer.Reset(
                    screenPos,
                    size: size * RoadSignThemeManager.DefaultSpeedlimitsAspectRatio());
                squareSignRenderer.DrawLargeTexture(
                    speedlimit: defaultSpeedLimit,
                    theme: RoadSignThemeManager.Instance.SpeedLimitDefaults,
                    disabled: forceSlowDriving);
            } else {
                //-------------------------------------
                // Draw override, if exists, otherwise draw circle and small blue default
                // Get speed limit override for segment
                //-------------------------------------
                SpeedValue? drawSpeedlimit = SpeedLimitManager.Instance.CalculateCustomSpeedLimit(segmentId, SpeedLimitManager.VEHICLE_TYPES);

                bool isDefaultSpeed =
                    !drawSpeedlimit.HasValue ||
                    drawSpeedlimit.Value.Equals(defaultSpeedLimit);

                signRenderer.DrawLargeTexture(
                    speedlimit: (isDefaultSpeed || forceSlowDriving) ? defaultSpeedLimit : drawSpeedlimit,
                    theme: drawEnv.largeSignsTextures_,
                    disabled: forceSlowDriving);

                if (!forceSlowDriving &&
                    args.IsInteractive &&
                    drawSpeedlimit.HasValue &&
                    !isDefaultSpeed &&
                    SavedGameOptions.Instance.showDefaultSpeedSubIcon) {
                    signRenderer.DrawDefaultSpeedSubIcon(defaultSpeedLimit);
                }
            }

            if (segment.IsBothEndsUnderground()) {
                //-----------------------
                // Draw small arrow down
                //-----------------------
                signRenderer.DrawSmallTexture_TopLeft(RoadUI.Instance.Underground);
            }

            if (!isHoveredHandle) {
                return false;
            }

            // Clickable overlay (interactive signs also True):
            // Register the position of a mouse-hovered speedlimit overlay icon
            args.HoveredSegmentHandles.Add(
                item: new OverlaySegmentSpeedlimitHandle(segmentId));

            this.finalDirection_ = NetInfo.Direction.Both;
            return true;
        }

        /// <summary>Draw speed limit handles one per lane.</summary>
        /// <param name="segmentId">Seg id.</param>
        /// <param name="segmentCenterPos">Cached or calculated via Segment.GetCenter() center of bezier.</param>
        /// <param name="camPos">Camera.</param>
        /// <param name="drawEnv">Temporary values used for rendering this frame.</param>
        /// <param name="args">Render args.</param>
        private bool DrawSpeedLimitHandles_PerLane(
            ushort segmentId,
            Vector3 segmentCenterPos,
            Vector3 camPos,
            [NotNull] DrawEnv drawEnv,
            [NotNull] DrawArgs args)
        {
            bool ret = false;
            ref NetSegment segment = ref segmentId.ToSegment();

            // show individual speed limit handle per lane
            int numLanes = GeometryUtil.GetSegmentNumVehicleLanes(
                segmentId: segmentId,
                nodeId: null,
                numDirections: out int numDirections,
                vehicleTypeFilter: SpeedLimitManager.VEHICLE_TYPES);

            NetInfo segmentInfo = segment.Info;
            Vector3 yu = (segment.m_endDirection - segment.m_startDirection).normalized;
            Vector3 xu = Vector3.Cross(yu, new Vector3(0, 1f, 0)).normalized;
            float signSize = args.IsInteractive
                                 ? Constants.OVERLAY_INTERACTIVE_SIGN_SIZE
                                 : Constants.OVERLAY_READONLY_SIGN_SIZE;

            Vector3 drawOriginPos = segmentCenterPos -
                                    (0.5f * (((numLanes - 1) + numDirections) - 1) * signSize * xu);

            var sortedLanes = segment.GetSortedLanes(
                null,
                SpeedLimitManager.LANE_TYPES,
                SpeedLimitManager.VEHICLE_TYPES);

            bool onlyMonorailLanes = sortedLanes.Count > 0;

            if (args.IsInteractive) {
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
                gridOrigin: drawOriginPos,
                cellWidth: signSize,
                cellHeight: signSize,
                xu: xu,
                yu: yu);

            // Sign renderer logic and chosen texture for signs
            SignRenderer signRenderer = default;

            // Defaults have 1:1 ratio (square textures)
            Vector2 largeRatio = drawEnv.drawDefaults_
                                     ? RoadSignThemeManager.DefaultSpeedlimitsAspectRatio()
                                     : drawEnv.signsThemeAspectRatio_;

            // Signs are rendered in a grid starting from col 0
            float signColumn = 0f;
            var colorController = new OverlayHandleColorController(args.IsInteractive);

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
                        signColumn += 1f; // full space between opposite directions
                    }

                    directions.Add(laneInfo.m_finalDirection);
                }

                Vector3 worldPos = grid.GetPositionForRowCol(signColumn, 0);
                bool visible = GeometryUtil.WorldToScreenPoint(worldPos, out Vector3 screenPos);

                if (!visible) {
                    continue;
                }

                float visibleScale = drawEnv.baseScreenSizeForSign_ / (worldPos - camPos).magnitude;
                float size = (args.IsInteractive ? 1f : 0.8f) * SPEED_LIMIT_SIGN_SIZE * visibleScale;

                Rect signScreenRect = signRenderer.Reset(screenPos, size: largeRatio * size);

                // Set render transparency based on mouse hover
                bool isHoveredHandle = args.IsInteractive && signRenderer.ContainsMouse(args.Mouse);

                // Sqrt(visibleScale) makes fade start later as distance grows
                colorController.SetGUIColor(
                    hovered: isHoveredHandle,
                    intersectsGuiWindows: args.IntersectsAnyUIRect(signScreenRect),
                    opacityMultiplier: 1f); // Mathf.Sqrt(visibleScale) for fade

                // Get speed limit override for the lane
                GetSpeedLimitResult overrideSpeedlimit =
                    SpeedLimitManager.Instance.CalculateCustomSpeedLimit(laneId);

                bool isDefaultSpeed =
                    !overrideSpeedlimit.OverrideValue.HasValue
                    || (overrideSpeedlimit.DefaultValue.HasValue &&
                        overrideSpeedlimit.OverrideValue.Value.Equals(overrideSpeedlimit.DefaultValue.Value));

                signRenderer.DrawLargeTexture(
                    speedlimit: isDefaultSpeed ? overrideSpeedlimit.DefaultValue.Value : overrideSpeedlimit.OverrideValue.Value,
                    theme: drawEnv.largeSignsTextures_);

                if (args.IsInteractive &&
                    overrideSpeedlimit.OverrideValue.HasValue &&
                    !isDefaultSpeed &&
                    SavedGameOptions.Instance.showDefaultSpeedSubIcon) {
                    signRenderer.DrawDefaultSpeedSubIcon(overrideSpeedlimit.DefaultValue.Value);
                }

                if (args.IsInteractive
                    && !onlyMonorailLanes
                    && ((laneInfo.m_vehicleType & VehicleInfo.VehicleType.Monorail) != VehicleInfo.VehicleType.None))
                {
                    var vehicleInfoSignTextures = RoadUI.Instance.VehicleInfoSignTextures;
                    Texture2D tex1 = vehicleInfoSignTextures[LegacyExtVehicleType.ToNew(old: ExtVehicleType.PassengerTrain)];

                    // TODO: Replace with direct call to GUI.DrawTexture as in the func above
                    grid.DrawStaticSquareOverlayGridTexture(
                        texture: tex1,
                        camPos: camPos,
                        x: signColumn,
                        y: 1f,
                        size: SPEED_LIMIT_SIGN_SIZE,
                        screenRect: out Rect _);
                }

                if (segment.IsBothEndsUnderground()) {
                    //-------------------------------------
                    // Draw arrow down
                    //-------------------------------------
                    signRenderer.DrawSmallTexture_TopLeft(RoadUI.Instance.Underground);
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

                    ret = true;
                }

                signColumn += 1f;
            }

            colorController.RestoreGUIColor();
            return ret;
        }
    }

    // end class
}