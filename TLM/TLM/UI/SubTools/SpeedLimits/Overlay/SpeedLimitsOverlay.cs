namespace TrafficManager.UI.SubTools.SpeedLimits.Overlay {
    using System.Collections.Generic;
    using System.Linq;
    using CitiesGameBridge.Service;
    using ColossalFramework;
    using GenericGameBridge.Service;
    using JetBrains.Annotations;
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
        private const float SMALL_ICON_SCALE = 0.66f;

        private ushort segmentId_;
        private NetInfo.Direction finalDirection_ = NetInfo.Direction.None;

        private TrafficManagerTool mainTool_;

        /// <summary>Used to pass options to the overlay rendering.</summary>
        public struct DrawArgs {
            /// <summary>If not null, contains mouse position. Null means mouse is over some GUI window.</summary>+
            public Vector2? Mouse;

            /// <summary>List of UI frame rectangles which will make the signs fade if rendered over.</summary>
            public List<Rect> UiWindowRects;

            /// <summary>Set to true to allow bigger and clickable road signs.</summary>
            public bool InteractiveSigns;

            /// <summary>
            /// User is holding Shift to edit multiple segments.
            /// Set to true when operating entire road between two junctions.
            /// </summary>
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
                return new() {
                    UiWindowRects = new List<Rect>(),
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

            public bool IntersectsAnyUIRect(Rect testRect) {
                return this.UiWindowRects.Any(testRect.Overlaps);
            }
        }

        /// <summary>
        /// Stores potentially visible segment ids while the camera did not move.
        /// </summary>
        [NotNull]
        private readonly GenericArrayCache<ushort> cachedVisibleSegmentIds_;

        /// <summary>Stores last cached camera position in <see cref="cachedVisibleSegmentIds_"/>.</summary>
        private CameraTransformValue lastCachedCamera_;

        private const float SPEED_LIMIT_SIGN_SIZE = 70f;

        private readonly Dictionary<ushort, Dictionary<NetInfo.Direction, Vector3>>
            segmentCenterByDir_ = new();

        public SpeedLimitsOverlay(TrafficManagerTool mainTool) {
            this.mainTool_ = mainTool;
            this.cachedVisibleSegmentIds_ = new GenericArrayCache<ushort>(NetManager.MAX_SEGMENT_COUNT);
            this.lastCachedCamera_ = new CameraTransformValue();
        }

        /// <summary>Displays non-sign overlays, like lane highlights.</summary>
        /// <param name="cameraInfo">The camera.</param>
        /// <param name="args">The state of the parent <see cref="SpeedLimitsTool"/>.</param>
        public void RenderHelperGraphics(RenderManager.CameraInfo cameraInfo,
                                         DrawArgs args) {
            if (!args.ShowLimitsPerLane) {
                this.RenderSegments(cameraInfo, args);
            }
        }

        /// <summary>Render segment overlay (this is curves, not the signs).</summary>
        /// <param name="cameraInfo">The camera.</param>
        /// <param name="args">The state of the parent <see cref="SpeedLimitsTool"/>.</param>
        private void RenderSegments(RenderManager.CameraInfo cameraInfo,
                                    DrawArgs args) {
            if (!args.MultiSegmentMode) {
                //------------------------
                // Single segment highlight
                //------------------------
                this.RenderSegmentSideOverlay(
                    cameraInfo: cameraInfo,
                    segmentId: this.segmentId_,
                    args: args,
                    finalDirection: this.finalDirection_);
            } else {
                //------------------------
                // Entire street highlight
                //------------------------
                if (RoundaboutMassEdit.Instance.TraverseLoop(
                    segmentId: this.segmentId_,
                    segList: out var segmentList)) {
                    foreach (ushort segmentId in segmentList) {
                        this.RenderSegmentSideOverlay(
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

                            this.RenderSegmentSideOverlay(
                                cameraInfo: cameraInfo,
                                segmentId: data.CurSeg.segmentId,
                                args: args,
                                finalDirection: finalDirection);
                            return true;
                        });
                }
            }
        }

        /// <summary>
        /// Renders all lane curves with the given <paramref name="finalDirection"/>
        /// if NetInfo.Direction.None, all lanes are rendered.
        /// </summary>
        private void RenderSegmentSideOverlay(RenderManager.CameraInfo cameraInfo,
                                              ushort segmentId,
                                              DrawArgs args,
                                              NetInfo.Direction finalDirection = NetInfo.Direction.None)
        {
            bool pressed = Input.GetMouseButton(0);
            ref NetSegment netSegment = ref segmentId.ToSegment();
            foreach (LaneIdAndIndex laneIdAndIndex in NetService.Instance.GetSegmentLaneIdsAndLaneIndexes(segmentId)) {
                NetInfo.Lane laneInfo = netSegment.Info.m_lanes[laneIdAndIndex.laneIndex];

                bool render = (laneInfo.m_laneType & SpeedLimitManager.LANE_TYPES) != 0;
                render &= (laneInfo.m_vehicleType & SpeedLimitManager.VEHICLE_TYPES) != 0;
                render &= laneInfo.m_finalDirection == finalDirection || finalDirection == NetInfo.Direction.None;

                if (render) {
                    RenderLaneOverlay(cameraInfo, laneIdAndIndex.laneId, args);
                }
            }
        }

        /// <summary>Draw blue lane curves overlay.</summary>
        /// <param name="cameraInfo">The Camera.</param>
        /// <param name="laneId">The lane.</param>
        /// <param name="args">The state of the parent <see cref="SpeedLimitsTool"/>.</param>
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
            if (!this.lastCachedCamera_.Equals(currentCamera)) {
                // cache visible segments
                this.lastCachedCamera_ = currentCamera;
                this.cachedVisibleSegmentIds_.Clear();

                this.ShowSigns_CacheVisibleSegments(
                    netManager: netManager,
                    camPos: camPos,
                    speedLimitManager: speedLimitManager);
            }

            bool hover = false;
            for (int segmentIdIndex = this.cachedVisibleSegmentIds_.Size - 1;
                 segmentIdIndex >= 0;
                 segmentIdIndex--) {
                ushort segmentId = this.cachedVisibleSegmentIds_.Values[segmentIdIndex];

                // If VehicleRestrictions tool is active, skip drawing the current selected segment
                if (this.mainTool_.GetToolMode() == ToolMode.VehicleRestrictions
                    && segmentId == TrafficManagerTool.SelectedSegmentId) {
                    continue;
                }

                // no speed limit overlay on selected segment when in vehicle restrictions mode
                hover |= this.DrawSpeedLimitHandles(
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
                if (!ExtSegmentManager.Instance.IsSegmentValid((ushort)segmentId)) {
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

                this.cachedVisibleSegmentIds_.Add((ushort)segmentId);
            } // end for all segments
        }

        private bool DrawSpeedLimitHandles(ushort segmentId,
                                           ref NetSegment segment,
                                           ref Vector3 camPos,
                                           DrawArgs args) {
            // in defaults mode separate lanes don't make any sense, so show segments at all times
            if (args.ShowLimitsPerLane && !args.ShowDefaultsMode) {
                return this.DrawSpeedLimitHandles_PerLane(
                    segmentId,
                    ref segment,
                    camPos,
                    args);
            }

            return this.DrawSpeedLimitHandles_PerSegment(
                segmentId,
                camPos,
                args);
        }

        /// <summary>Render speed limit handles one per segment.</summary>
        /// <param name="segmentId">Seg id.</param>
        /// <param name="camPos">Camera.</param>
        /// <param name="args">Render args.</param>
        private bool DrawSpeedLimitHandles_PerSegment(ushort segmentId,
                                                      Vector3 camPos,
                                                      DrawArgs args) {
            bool ret = false;

            // draw speedlimits over mean middle points of lane beziers
            if (!this.segmentCenterByDir_.TryGetValue(
                key: segmentId,
                value: out Dictionary<NetInfo.Direction, Vector3> segCenter)) {
                segCenter = new Dictionary<NetInfo.Direction, Vector3>();
                this.segmentCenterByDir_.Add(key: segmentId, value: segCenter);
                GeometryUtil.CalculateSegmentCenterByDir(
                    segmentId: segmentId,
                    segmentCenterByDir: segCenter,
                    minDistance: SPEED_LIMIT_SIGN_SIZE * TrafficManagerTool.MAX_ZOOM);
            }

            // Sign renderer logic and chosen texture for signs
            SpeedLimitsOverlaySign signRenderer = default;
            IDictionary<int, Texture2D> signsThemeTextures = SpeedLimitTextures.GetTextureSource();
            IDictionary<int, Texture2D> largeSignsTextureSource = args.ShowDefaultsMode
                ? SpeedLimitTextures.RoadDefaults
                : signsThemeTextures;

            // Default signs are round, mph/kmph textures can be round or rectangular
            Vector2 signsThemeAspectRatio = SpeedLimitTextures.GetTextureAspectRatio();
            Vector2 largeRatio = args.ShowDefaultsMode ? Vector2.one : signsThemeAspectRatio;
            var colorController = new OverlayHandleColorController(args.InteractiveSigns);

            //--------------------------
            // For all segments visible
            //--------------------------
            foreach (KeyValuePair<NetInfo.Direction, Vector3> e in segCenter) {
                bool visible = GeometryUtil.WorldToScreenPoint(worldPos: e.Value, screenPos: out Vector3 screenPos);

                if (!visible) {
                    continue;
                }

                float visibleScale = 100.0f / (e.Value - camPos).magnitude;
                float size = (args.InteractiveSigns ? 1f : 0.8f) * SPEED_LIMIT_SIGN_SIZE * visibleScale;

                // Recalculate visible rect for screen position and size
                Rect signScreenRect = signRenderer.Reset(screenPos: screenPos, size: size * largeRatio);

                bool isHoveredHandle = args.InteractiveSigns && signRenderer.ContainsMouse(args.Mouse);

                // Get speed limit override for segment
                SpeedValue? overrideSpeedlimit =
                    SpeedLimitManager.Instance.GetCustomSpeedLimit(segmentId, finalDir: e.Key);

                // Get default or default-override speed limit for road type
                NetInfo neti = this.GetSegmentNetinfo(segmentId);
                SpeedValue defaultSpeedlimit =
                    new SpeedValue(gameUnits: SpeedLimitManager.Instance.GetCustomNetInfoSpeedLimit(info: neti));

                //-----------
                // Rendering
                //-----------
                // Sqrt(visibleScale) makes fade start later as distance grows
                colorController.SetGUIColor(
                    hovered: isHoveredHandle,
                    intersectsGuiWindows: args.IntersectsAnyUIRect(signScreenRect),
                    opacityMultiplier: Mathf.Sqrt(visibleScale));

                // Render override if interactive, or if readonly info layer and override exists
                if (args.InteractiveSigns || overrideSpeedlimit.HasValue) {
                    signRenderer.DrawLargeTexture(
                        speedlimit: args.ShowDefaultsMode ? defaultSpeedlimit : overrideSpeedlimit,
                        textureSource: largeSignsTextureSource);
                }

                // If Alt is held, then also overlay the other (default limit in edit override mode,
                // or override in edit defaults mode) as a small texture.
                if (args.ShowOtherPerLaneModeTemporary) {
                    if (args.ShowDefaultsMode) {
                        signRenderer.DrawSmallTexture(
                            speedlimit: overrideSpeedlimit,
                            smallSize: size * SMALL_ICON_SCALE * signsThemeAspectRatio,
                            textureSource: signsThemeTextures);
                    } else {
                        signRenderer.DrawSmallTexture(
                            speedlimit: defaultSpeedlimit,
                            smallSize: size * SMALL_ICON_SCALE * Vector2.one,
                            textureSource: SpeedLimitTextures.RoadDefaults);
                    }
                }

                if (isHoveredHandle) {
                    // Clickable overlay (interactive signs also True):
                    // Register the position of a mouse-hovered speedlimit overlay icon
                    args.HoveredSegmentHandles.Add(
                        item: new OverlaySegmentSpeedlimitHandle(
                            segmentId: segmentId,
                            finalDirection: e.Key));

                    this.segmentId_ = segmentId;
                    this.finalDirection_ = e.Key;
                    ret = true;
                }
            }

            colorController.RestoreGUIColor();
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
        private bool DrawSpeedLimitHandles_PerLane(ushort segmentId,
                                                   ref NetSegment segment,
                                                   Vector3 camPos,
                                                   DrawArgs args) {
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
                ? Constants.OVERLAY_INTERACTIVE_SIGN_SIZE
                : Constants.OVERLAY_READONLY_SIGN_SIZE; // reserved sign size in game coordinates

            Vector3 drawOriginPos = segmentCenterPos -
                                    (0.5f * (((numLanes - 1) + numDirections) - 1) * signSize * xu);

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

            // Main grid for large icons
            var grid = new Highlight.Grid(
                gridOrigin: drawOriginPos,
                cellWidth: signSize,
                cellHeight: signSize,
                xu: xu,
                yu: yu);

            // Sign renderer logic and chosen texture for signs
            SpeedLimitsOverlaySign signRenderer = default;
            IDictionary<int, Texture2D> currentThemeTextures = SpeedLimitTextures.GetTextureSource();

            Vector2 signsThemeAspectRatio = SpeedLimitTextures.GetTextureAspectRatio();
            Vector2 largeRatio = args.ShowDefaultsMode ? Vector2.one : signsThemeAspectRatio;

            IDictionary<int, Texture2D> largeSignsTextureSource = args.ShowDefaultsMode
                ? SpeedLimitTextures.RoadDefaults
                : currentThemeTextures;

            // Signs are rendered in a grid starting from col 0
            float signColumn = 0f;
            var colorController = new OverlayHandleColorController(args.InteractiveSigns);

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

                float visibleScale = 100.0f / (worldPos - camPos).magnitude;
                float size = (args.InteractiveSigns ? 1f : 0.8f) * SPEED_LIMIT_SIGN_SIZE * visibleScale;
                Rect signScreenRect = signRenderer.Reset(screenPos, size: largeRatio * size);

                // Set render transparency based on mouse hover
                bool isHoveredHandle = args.InteractiveSigns && signRenderer.ContainsMouse(args.Mouse);

                // Sqrt(visibleScale) makes fade start later as distance grows
                colorController.SetGUIColor(
                    hovered: isHoveredHandle,
                    intersectsGuiWindows: args.IntersectsAnyUIRect(signScreenRect),
                    opacityMultiplier: Mathf.Sqrt(visibleScale));

                // Get speed limit override for the lane
                GetSpeedLimitResult overrideSpeedlimit =
                    SpeedLimitManager.Instance.GetCustomSpeedLimit(laneId);

                // TODO: Potentially Null pointer reference in overrideSpeedlimit.DefaultValue.Value
                SpeedValue largeSpeedlimit = overrideSpeedlimit.OverrideValue.HasValue
                                                 ? overrideSpeedlimit.OverrideValue.Value
                                                 : overrideSpeedlimit.DefaultValue.Value;

                signRenderer.DrawLargeTexture(speedlimit: largeSpeedlimit,
                                              textureSource: largeSignsTextureSource);

                // If Alt is held, then also overlay the other (default limit in edit override mode,
                // or override in edit defaults mode) as a small texture.
                if (args.ShowOtherPerLaneModeTemporary) {
                    if (args.ShowDefaultsMode) {
                        signRenderer.DrawSmallTexture(
                            speedlimit: overrideSpeedlimit.OverrideValue,
                            smallSize: size * SMALL_ICON_SCALE * signsThemeAspectRatio,
                            textureSource: currentThemeTextures);
                    } else {
                        signRenderer.DrawSmallTexture(
                            speedlimit: overrideSpeedlimit.DefaultValue,
                            smallSize: size * SMALL_ICON_SCALE * Vector2.one,
                            textureSource: SpeedLimitTextures.RoadDefaults);
                    }
                }

                if (args.InteractiveSigns
                    && !onlyMonorailLanes
                    && ((laneInfo.m_vehicleType & VehicleInfo.VehicleType.Monorail) != VehicleInfo.VehicleType.None))
                {
                    Texture2D tex1 = RoadUI.VehicleInfoSignTextures[
                        LegacyExtVehicleType.ToNew(old: ExtVehicleType.PassengerTrain)];

                    // TODO: Replace with direct call to GUI.DrawTexture as in the func above
                    grid.DrawStaticSquareOverlayGridTexture(
                        texture: tex1,
                        camPos: camPos,
                        x: signColumn,
                        y: 1f,
                        size: SPEED_LIMIT_SIGN_SIZE,
                        screenRect: out Rect _);
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