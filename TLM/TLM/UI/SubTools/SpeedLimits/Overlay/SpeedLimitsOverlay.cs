namespace TrafficManager.UI.SubTools.SpeedLimits.Overlay {
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
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
        [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:Fields should be private", Justification = "Reviewed.")]
        private class DrawEnv {
            /* Fields are set per-frame */

            // TODO: These fields should probably be moved out of DrawEnv

            // used to apply effects to overlay icons
            public OverlayHandleColorController Gui_;

            // set true to let other overlays know no need to check for hover
            public bool CheckHover_;

            /// <summary>
            /// This is set to true if the user will see blue default signs, or the user is holding
            /// Alt to see blue signs temporarily. Holding Alt while default signs are shown, will
            /// show segment speeds instead.
            /// </summary>
            public bool DefaultsMode_;

            /* Constructor used once per cache */

            // TODO: A fresh DrawEnv will be required whenever:
            // - UI resolution changes
            // - User changes icon theme
            // - User changes whether defaults are themed/unthemed in normal view
            public DrawEnv(
                RoadSignThemes.RoadSignTheme overrideTheme,
                Vector2 overrideAR,
                RoadSignThemes.RoadSignTheme fallbackTheme,
                Vector2 fallbackAR,
                RoadSignThemes.RoadSignTheme defaultsTheme,
                Vector2 defaultsAR,
                float uiScaleFactor
                ) {

                this.OverrideTheme = overrideTheme;
                this.OverrideAR = overrideAR;
                this.FallbackTheme = fallbackTheme;
                this.FallbackAR = fallbackAR;
                this.DefaultsTheme = defaultsTheme;
                this.DefaultsAR = defaultsAR;
                this.UIScaleFactor = uiScaleFactor;
            }

            /* SpeedlimitsToolMode.Segments or SpeedlimitsToolMode.Lanes modes */

            // Non-default speed
            public RoadSignThemes.RoadSignTheme OverrideTheme { get; private set; }
            public Vector2 OverrideAR { get; private set; }

            // Unset or same as default speed = how do we theme default speeds in normal mode?
            public RoadSignThemes.RoadSignTheme FallbackTheme { get; private set; }
            public Vector2 FallbackAR { get; private set; }

            /* SpeedlimitsToolMode.Defaults mode */

            // Default (for network) speed
            public RoadSignThemes.RoadSignTheme DefaultsTheme { get; private set; }
            public Vector2 DefaultsAR { get; private set; }

            // Base size for icons, factoring in UI scale
            public float UIScaleFactor { get; private set; }
        }

        private struct CachedSegment {
            public bool isDirty; // not used _yet_
            public ushort id_;
            public Vector3 center_;
        }

        /// <summary>
        /// Stores potentially visible segment ids while the camera did not move.
        /// </summary>
        [NotNull]
        private readonly GenericArrayCache<CachedSegment> cachedVisibleSegmentIds_;

        /// <summary>If set to true, prompts one-time cache reset.</summary>
        private bool resetCacheFlag_ = true;

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
        /// Do not call this directly, it should only be called from ShowSigns_GUI.
        ///
        /// On cache reset:
        /// - Create new DrawEnv
        /// - Refresh cache of visible segments
        /// </summary>
        private void ResetCacheIfNecessary(Camera camera, Vector3 camPos, ref DrawArgs args) {

            var currentCamera = new CameraTransformValue(camera);

            // TODO: Can road network change while speed limit tool is active? Disasters?
            if (this.resetCacheFlag_
                || !this.lastCachedCamera_.Equals(currentCamera)
                || this.lastUndergroundMode_ != TrafficManagerTool.IsUndergroundMode) {

                this.resetCacheFlag_ = false;

                var themed = RoadSignThemes.ActiveTheme;
                var themedAR = themed.GetAspectRatio();

                var unthemed = RoadSignThemes.Instance.RoadDefaults;
                var unthemedAR = unthemed.GetAspectRatio();

                var preferThemed = !Options.differentiateDefaultSpeedsInNormalView;

                // TODO: DrawEnv reset should ideally be separate from cached segments reset
                this.env_ = new DrawEnv(
                    overrideTheme: themed,
                    overrideAR: themedAR,
                    fallbackTheme: preferThemed ? themed : unthemed,
                    fallbackAR: preferThemed ? themedAR : unthemedAR,
                    defaultsTheme: unthemed,
                    defaultsAR: unthemedAR,
                    uiScaleFactor: Constants.OverlaySignVisibleSize);

                this.lastCachedCamera_ = currentCamera;
                this.lastUndergroundMode_ = TrafficManagerTool.IsUndergroundMode;

                this.ShowSigns_RefreshVisibleSegmentsCache(camPos);
            }
        }

        /// <summary>
        /// When camera position has changed and cached segments set is invalid, scan all segments
        /// again and remember those visible in the camera frustum.
        /// </summary>
        /// <param name="netManager">Access to map data.</param>
        /// <param name="camPos">Camera position to consider.</param>
        /// <param name="speedLimitManager">Query if a segment is eligible for speed limits.</param>
        private void ShowSigns_RefreshVisibleSegmentsCache(Vector3 camPos) {

            this.cachedVisibleSegmentIds_.Clear();
            this.segmentCenters_.Clear();

            for (uint segmentId = 1; segmentId < NetManager.MAX_SEGMENT_COUNT; ++segmentId) {

                ref NetSegment segment = ref ((ushort)segmentId).ToSegment();

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
                        isDirty = true,
                        id_ = (ushort)segmentId,
                        center_ = segment.GetCenter(),
                    });
            } // end for all segments
        }

        private DrawEnv env_;

        /// <summary>
        /// Draw speed limit signs (only in GUI mode).
        /// NOTE: This must be called from GUI mode, because of GUI.DrawTexture use.
        /// Render the speed limit signs based on the current settings.
        /// </summary>
        /// <param name="args">The state of the parent <see cref="SpeedLimitsTool"/>.</param>
        public void ShowSigns_GUI(DrawArgs args) {
            Camera camera = Camera.main;
            if (camera == null) {
                this.ResetCache();
                return;
            }
            Transform currentCameraTransform = camera.transform;
            Vector3 camPos = currentCameraTransform.position;

            ResetCacheIfNecessary(camera, camPos, ref args);

            this.env_.Gui_ = new OverlayHandleColorController(args.IsInteractive);
            this.env_.CheckHover_ = args.IsInteractive;
            this.env_.DefaultsMode_ = args.ToolMode == SpeedlimitsToolMode.Defaults;

            for (int cacheIdx = this.cachedVisibleSegmentIds_.Size - 1; cacheIdx >= 0; cacheIdx--) {

                // If VehicleRestrictions tool is active, skip drawing the current selected segment
                ushort segmentId = this.cachedVisibleSegmentIds_.Values[cacheIdx].id_;
                if (this.mainTool_.GetToolMode() == ToolMode.VehicleRestrictions
                    && segmentId == TrafficManagerTool.SelectedSegmentId) {
                    continue;
                }

                if (args.ToolMode == SpeedlimitsToolMode.Segments || this.env_.DefaultsMode_) {
                    this.DrawSpeedLimitHandles_PerSegment(cacheIdx, args, camPos);
                } else {
                    this.DrawSpeedLimitHandles_PerLane(cacheIdx, args, camPos);
                }
            }
        }

        /// <summary>Draw speed limit handles; one per segment.</summary>
        /// <param name="cacheIdx">The index of this segment in the segment cache.</param>
        /// <param name="args">Render args.</param>
        /// <param name="camPos">Camera position.</param>
        private void DrawSpeedLimitHandles_PerSegment(int cacheIdx, [NotNull] DrawArgs args, Vector3 camPos)
        {
            CachedSegment cache = this.cachedVisibleSegmentIds_.Values[cacheIdx];

            var segmentId = cache.id_;

            NetSegment segment = segmentId.ToSegment();
            NetInfo segmentInfo = segment.Info;

            var speedLimit = new SpeedValue(gameUnits: SpeedLimitManager.Instance.CalculateCustomNetinfoSpeedLimit(segmentInfo));

            RoadSignThemes.RoadSignTheme theme;
            Vector2 aspectRatio;

            if (this.env_.DefaultsMode_) {

                // SpeedlimitsToolMode.Defaults

                theme = this.env_.DefaultsTheme;
                aspectRatio = this.env_.DefaultsAR;

            } else {

                // SpeedlimitsToolMode.Segments

                SpeedValue? overrideSpeedlimitForward =
                    SpeedLimitManager.Instance.CalculateCustomSpeedLimit(segmentId, finalDir: NetInfo.Direction.Forward);
                SpeedValue? overrideSpeedlimitBack =
                    SpeedLimitManager.Instance.CalculateCustomSpeedLimit(segmentId, finalDir: NetInfo.Direction.Backward);

                SpeedValue? speedOverride = GetAverageSpeedlimit(
                    forward: overrideSpeedlimitForward,
                    back: overrideSpeedlimitBack);

                if (speedOverride.HasValue && !speedOverride.Value.Equals(speedLimit)) {
                    speedLimit = speedOverride.Value;

                    theme = this.env_.OverrideTheme;
                    aspectRatio = this.env_.OverrideAR;
                } else {
                    theme = this.env_.FallbackTheme;
                    aspectRatio = this.env_.FallbackAR;
                }

            }

            // TODO: Replace formula in visibleScale and size to use Constants.OVERLAY_INTERACTIVE_SIGN_SIZE and OVERLAY_READONLY_SIGN_SIZE
            var segCenter = cache.center_;
            float visibleScale = this.env_.UIScaleFactor / (segCenter - camPos).magnitude;
            float size = (args.IsInteractive ? 1f : 0.8f) * SPEED_LIMIT_SIGN_SIZE * visibleScale;

            SignRenderer render = default;

            GeometryUtil.WorldToScreenPoint(worldPos: segCenter, screenPos: out Vector3 screenPos);
            Rect signRect = render.Reset(screenPos, size * aspectRatio);

            bool isHovered = this.env_.CheckHover_ && render.ContainsMouse(args.Mouse);

            this.env_.Gui_.SetGUIColor(
                hovered: isHovered,
                intersectsGuiWindows: args.IntersectsAnyUIRect(signRect),
                opacityMultiplier: 1.0f); // Mathf.Sqrt(visibleScale) for fade

            render.DrawLargeTexture(speedLimit, theme);

            if (segment.IsBothEndsUnderground()) {
                render.DrawSmallTexture_TopLeft(RoadUI.Instance.Underground);
            }

            this.env_.Gui_.RestoreGUIColor();

            if (!isHovered) {
                return;
            }

            this.env_.CheckHover_ = false;

            // Register the position of a mouse-hovered speedlimit overlay icon
            args.HoveredSegmentHandles.Add(item: new OverlaySegmentSpeedlimitHandle(segmentId));

            this.finalDirection_ = NetInfo.Direction.Both;
        }

        private SpeedValue? GetAverageSpeedlimit(SpeedValue? forward, SpeedValue? back) {
            if (forward.HasValue && back.HasValue) {
                return (forward.Value + back.Value).Scale(0.5f);
            }

            return forward ?? back;
        }

        /// <summary>Draw speed limit handles; one per lane.</summary>
        /// <param name="cacheIdx">The index of this segment in the segment cache.</param>
        /// <param name="args">Render args.</param>
        /// <param name="camPos">Camera position.</param>
        private void DrawSpeedLimitHandles_PerLane(int cacheIdx, [NotNull] DrawArgs args, Vector3 camPos)
        {

            ref CachedSegment cache = ref this.cachedVisibleSegmentIds_.Values[cacheIdx];
            var segmentId = cache.id_;

            NetSegment segment = segmentId.ToSegment();
            NetInfo segmentInfo = segment.Info;

            IList<LanePos> lanes = segment.GetSortedLanes(
                startNode: null,
                laneTypeFilter: SpeedLimitManager.LANE_TYPES,
                vehicleTypeFilter: SpeedLimitManager.VEHICLE_TYPES);

            if (lanes.Count == 0) {
                return;
            }

            var directions = new HashSet<NetInfo.Direction>();

            bool onlyMonorailLanes = true;

            int sortedLaneIndex = -1;
            int directionChangeIndex = -1;

            foreach (var lane in lanes) {
                ++sortedLaneIndex;

                // TODO: find a less bloaty way of calculating num lane directions
                if (directionChangeIndex == -1 && !directions.Contains(lane.finalDirection)) {
                    directions.Add(lane.finalDirection);
                    if (directions.Count == 2) {
                        directionChangeIndex = sortedLaneIndex;
                    }
                }

                // TODO: we should just always draw mini icon on monorails = consistency
                if (onlyMonorailLanes &&
                    (lane.vehicleType & VehicleInfo.VehicleType.Monorail) == VehicleInfo.VehicleType.None) {
                    onlyMonorailLanes = false;
                }

                if (!onlyMonorailLanes && directions.Count > 1) {
                    break;
                }
            }

            Vector3 yu = (segment.m_endDirection - segment.m_startDirection).normalized;
            Vector3 xu = Vector3.Cross(yu, new Vector3(0, 1f, 0)).normalized;
            float signSize = args.IsInteractive
                                 ? Constants.OVERLAY_INTERACTIVE_SIGN_SIZE
                                 : Constants.OVERLAY_READONLY_SIGN_SIZE;

            Vector3 drawOriginPos = cache.center_ -
                                    (0.5f * (((lanes.Count - 1) + directions.Count) - 1) * signSize * xu);

            // Main grid for large icons
            var grid = new Highlight.Grid(
                gridOrigin: drawOriginPos,
                cellWidth: signSize,
                cellHeight: signSize,
                xu: xu,
                yu: yu);

            // Signs are rendered in a grid starting from col 0
            float signColumn = 0f;

            bool foundHovered = false;
            sortedLaneIndex = -1;

            foreach (var lane in lanes) {

                ++sortedLaneIndex;

                if (sortedLaneIndex == directionChangeIndex) {
                    signColumn += 1f; // add space between directions
                }

                Vector3 worldPos = grid.GetPositionForRowCol(signColumn, 0);
                bool visible = GeometryUtil.WorldToScreenPoint(worldPos, out Vector3 screenPos);

                if (!visible) {
                    continue;
                }

                SpeedValue speedLimit;

                uint laneId = lane.laneId;

                GetSpeedLimitResult speeds =
                    SpeedLimitManager.Instance.CalculateCustomSpeedLimit(laneId);

                RoadSignThemes.RoadSignTheme theme;
                Vector2 aspectRatio;

                if (!speeds.OverrideValue.HasValue || speeds.OverrideValue.Value.Equals(speeds.DefaultValue)) {
                    speedLimit = speeds.DefaultValue.Value;

                    theme = this.env_.FallbackTheme;
                    aspectRatio = this.env_.FallbackAR;
                } else {
                    speedLimit = speeds.OverrideValue.Value;

                    theme = this.env_.OverrideTheme;
                    aspectRatio = this.env_.OverrideAR;
                }

                // TODO: Replace formula in visibleScale and size to use Constants.OVERLAY_INTERACTIVE_SIGN_SIZE and OVERLAY_READONLY_SIGN_SIZE
                float visibleScale = this.env_.UIScaleFactor / (worldPos - camPos).magnitude;
                float size = (args.IsInteractive ? 1f : 0.8f) * SPEED_LIMIT_SIGN_SIZE * visibleScale;

                SignRenderer render = default;

                Rect signRect = render.Reset(screenPos, size * aspectRatio);
                bool isHovered = this.env_.CheckHover_ && render.ContainsMouse(args.Mouse);

                this.env_.Gui_.SetGUIColor(
                    hovered: isHovered,
                    intersectsGuiWindows: args.IntersectsAnyUIRect(signRect),
                    opacityMultiplier: 1.0f); // Mathf.Sqrt(visibleScale) for fade

                render.DrawLargeTexture(speedLimit, theme);

                if (segment.IsBothEndsUnderground()) {
                    render.DrawSmallTexture_TopLeft(RoadUI.Instance.Underground);
                }

                if (args.IsInteractive
                    && !onlyMonorailLanes
                    && ((lane.vehicleType & VehicleInfo.VehicleType.Monorail) != VehicleInfo.VehicleType.None))
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

                this.env_.Gui_.RestoreGUIColor();

                signColumn += 1f;

                if (!isHovered) {
                    continue;
                }

                var laneIndex = lane.laneIndex;
                NetInfo.Lane laneInfo = segmentInfo.m_lanes[laneIndex];

                // Register the position of a mouse-hovered speedlimit overlay icon
                args.HoveredLaneHandles.Add(
                    new OverlayLaneSpeedlimitHandle(
                        segmentId: segmentId,
                        laneId: laneId,
                        laneIndex: laneIndex,
                        laneInfo: laneInfo,
                        sortedLaneIndex: sortedLaneIndex));

                foundHovered = true;

            }

            if (foundHovered) {
                this.env_.CheckHover_ = false;
            }

            return;
        }
    }

    // end class
}