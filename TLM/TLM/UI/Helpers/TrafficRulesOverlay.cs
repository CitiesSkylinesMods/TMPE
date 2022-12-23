namespace TrafficManager.UI.Helpers {
    using ColossalFramework;
    using TrafficManager.API.Manager;
    using TrafficManager.API.Traffic.Data;
    using TrafficManager.Manager.Impl;
    using TrafficManager.State;
    using TrafficManager.Traffic.Impl;
    using TrafficManager.UI.SubTools.PrioritySigns;
    using TrafficManager.UI.Textures;
    using TrafficManager.Util;
    using TrafficManager.Util.Extensions;
    using UnityEngine;

    /// <summary>
    /// Class handles rendering of priority signs overlay.
    /// Create one and set its fields before calling DrawSignHandles
    /// </summary>
    public struct TrafficRulesOverlay {
        private const float SIGN_SIZE_PIXELS = 80f;
        private const float AVERAGE_METERS_PER_PIXEL = 0.075f;
        private const float SIGN_SIZE_METERS = SIGN_SIZE_PIXELS * AVERAGE_METERS_PER_PIXEL;
        private const float VIEW_SIZE_RATIO = 0.8f;

        private readonly TrafficManagerTool mainTool_;
        private readonly bool debug_;
        private readonly bool handleClick_;

        public bool ViewOnly;

        /// <summary>Initializes a new instance of the <see cref="TrafficRulesOverlay"/> struct for rendering.</summary>
        /// <param name="mainTool">Parent <see cref="TrafficManagerTool"/>.</param>
        /// <param name="debug">Is debug rendering on.</param>
        /// <param name="handleClick">Whether clicks are to be handled.</param>
        public TrafficRulesOverlay(TrafficManagerTool mainTool,
                       bool debug,
                       bool handleClick) {
            mainTool_ = mainTool;
            debug_ = debug;
            handleClick_ = handleClick;
            ViewOnly = true;
        }

        /// <summary>
        /// Handles layout for the Junction Restriction signs being rendered.
        /// One <see cref="SignsLayout"/> is created per junction.
        /// Defines basis of rotated coordinate system, aligned somewhere near the node center,
        /// and directed along the road segment.
        /// </summary>
        private struct SignsLayout {
            /// <summary>starting point to draw signs.</summary>
            private readonly Vector3 origin_;

            /// <summary>normalized vector across segment (sideways).
            /// dirX_ is not necessarily perpendicular to dirY_ for asymmetrical junctions.
            /// </summary>
            private readonly Vector3 dirX_;

            /// <summary>normalized vector going away from the node.</summary>
            private readonly Vector3 dirY_;

            private readonly int signsPerRow_;
            private readonly bool viewOnly_;

            /// <summary>Zoom level inherited from the MainTool.</summary>
            private readonly float baseZoom_;

            /// <summary>Sign size (world units: meters).</summary>
            private float signSizeMeters_;

            // outermost position to start drawing signs in x direction (sideways).
            private float startX_;

            /// <summary>How many signs been drawn to calculate the position of the new sign.</summary>
            private int counter_;

            public SignsLayout(ushort segmentId,
                               bool startNode,
                               int signsPerRow,
                               bool viewOnly,
                               float baseZoom)
            {
                int segmentEndIndex = ExtSegmentEndManager.Instance.GetIndex(segmentId, startNode);
                ref ExtSegmentEnd segmentEnd = ref ExtSegmentEndManager.Instance.ExtSegmentEnds[segmentEndIndex];

                dirX_ = (segmentEnd.LeftCorner - segmentEnd.RightCorner).normalized;

                // for curved angled segments, corner1Direction may slightly differ from corner2Direction
                dirY_ = (segmentEnd.LeftCornerDir + segmentEnd.RightCornerDir) * 0.5f;

                // origin point to start drawing sprites from.
                origin_ = (segmentEnd.LeftCorner + segmentEnd.RightCorner) * 0.5f;

                this.signsPerRow_ = signsPerRow;
                this.viewOnly_ = viewOnly;
                this.baseZoom_ = baseZoom;
                this.signSizeMeters_ = viewOnly
                                           ? SIGN_SIZE_METERS * VIEW_SIZE_RATIO
                                           : SIGN_SIZE_METERS;
                this.counter_ = 0;

                float lenX = this.signSizeMeters_ * (signsPerRow - 1);
                this.startX_ = -lenX * 0.5f;
            }

            public bool DrawSign(bool small,
                                 ref Vector3 camPos,
                                 Color guiColor,
                                 Texture2D signTexture)
            {
                int col = counter_ / signsPerRow_;
                int row = counter_ - (col * signsPerRow_);
                counter_++;

                // +0.5f so that the signs don't cover crossings.
                Vector3 signCenter =
                    origin_ +
                    ((signSizeMeters_ * (col + 0.5f)) * dirY_) +
                    (((signSizeMeters_ * row) + startX_) * dirX_);

                bool visible = GeometryUtil.WorldToScreenPoint(worldPos: signCenter,
                                                               screenPos: out Vector3 signScreenPos);
                if (!visible) {
                    return false;
                }

                Vector3 diff = signCenter - camPos;
                float zoom = 100.0f * baseZoom_ / diff.magnitude;
                float size = (small ? 0.75f : 1f)
                             * (viewOnly_ ? VIEW_SIZE_RATIO : 1f)
                             * SIGN_SIZE_PIXELS * zoom;

                var boundingBox = new Rect(
                    x: signScreenPos.x - (size * 0.5f),
                    y: signScreenPos.y - (size * 0.5f),
                    width: size,
                    height: size);

                bool hoveredHandle = !viewOnly_ && TrafficManagerTool.IsMouseOver(boundingBox);
                if (viewOnly_) {
                    // Readonly signs look grey-ish
                    guiColor = Color.Lerp(guiColor, Color.gray, 0.5f);
                } else if (hoveredHandle) {
                    guiColor = Color.Lerp(
                        a: guiColor,
                        b: new Color(r: 1f, g: .7f, b: 0f),
                        t: 0.5f);
                }
                guiColor.a = TrafficManagerTool.GetHandleAlpha(hoveredHandle);

                GUI.color = guiColor;
                GUI.DrawTexture(boundingBox, signTexture);
                return hoveredHandle;
            }
        }

        /// <summary>
        /// Draws clickable or readonly sign handles for all segments in the junction.
        /// </summary>
        /// <param name="nodeId">Junction node id.</param>
        /// <param name="node">Junction node ref.</param>
        /// <param name="camPos">Camera position.</param>
        /// <param name="stateUpdated">?</param>
        /// <returns>Whether any of the signs was hovered.</returns>
        public bool DrawSignHandles(ushort nodeId,
                                    ref NetNode node,
                                    ref Vector3 camPos,
                                    out bool stateUpdated) {
            bool isAnyHovered = false;
            stateUpdated = false;

            // Quit now if:
            //   * view only,
            //   * and no permanent overlay enabled,
            //   * and is Junctions restrictions tool
            // TODO generalize for all tools.
            if (this.ViewOnly &&
                !(SavedGameOptions.Instance.junctionRestrictionsOverlay ||
                  MassEditOverlay.IsActive) &&
                this.mainTool_.GetToolMode() != ToolMode.JunctionRestrictions) {
                return false;
            }

            // NetManager netManager = Singleton<NetManager>.instance;
            Color guiColor = GUI.color;
            // Vector3 nodePos = node.m_position;
            IExtSegmentEndManager segEndMan = Constants.ManagerFactory.ExtSegmentEndManager;
            var theme = RoadSignThemeManager.ActiveTheme;

            for (int segmentIndex = 0; segmentIndex < Constants.MAX_SEGMENTS_OF_NODE; ++segmentIndex) {
                ushort segmentId = node.GetSegment(segmentIndex);

                if (segmentId == 0) {
                    continue;
                }

                bool isStartNode = segmentId.ToSegment().IsStartNode(nodeId);

                bool isIncoming = segEndMan
                                  .ExtSegmentEnds[segEndMan.GetIndex(segmentId, isStartNode)]
                                  .incoming;

                NetInfo segmentInfo = Singleton<NetManager>
                                      .instance
                                      .m_segments
                                      .m_buffer[segmentId]
                                      .Info;

                ItemClass connectionClass = segmentInfo.GetConnectionClass();

                if (connectionClass.m_service != ItemClass.Service.Road) {
                    continue; // only for road junctions
                }

                SignsLayout signsLayout = new SignsLayout(
                    segmentId: segmentId,
                    startNode: isStartNode,
                    signsPerRow: isIncoming ? 2 : 1,
                    viewOnly: this.ViewOnly,
                    baseZoom: this.mainTool_.GetBaseZoom());

                IJunctionRestrictionsManager junctionRManager = Constants.ManagerFactory.JunctionRestrictionsManager;

                // draw "lane-changing when going straight allowed" sign at (0; 0)
                bool allowed =
                    junctionRManager.IsLaneChangingAllowedWhenGoingStraight(
                        segmentId: segmentId,
                        startNode: isStartNode);

                bool configurable =
                    junctionRManager.IsLaneChangingAllowedWhenGoingStraightConfigurable(
                        segmentId: segmentId,
                        startNode: isStartNode,
                        node: ref node);

                if (this.debug_
                    || (configurable
                        && (!this.ViewOnly
                            || (allowed != junctionRManager
                                           .GetDefaultLaneChangingAllowedWhenGoingStraight(
                                               segmentId: segmentId,
                                               startNode: isStartNode,
                                               node: ref node)))))
                {
                    bool signHovered = signsLayout.DrawSign(
                        small: !configurable,
                        camPos: ref camPos,
                        guiColor: guiColor,
                        signTexture: theme.GetOtherRestriction(
                            RoadSignTheme.OtherRestriction.LaneChange,
                            allowed));

                    if (signHovered && this.handleClick_) {
                        isAnyHovered = true;
                        if (this.mainTool_.CheckClicked()) {
                            junctionRManager.ToggleLaneChangingAllowedWhenGoingStraight(
                                segmentId: segmentId,
                                startNode: isStartNode);
                            stateUpdated = true;
                        }
                    }
                }

                // draw "u-turns allowed" sign at (1; 0)
                allowed = junctionRManager.IsUturnAllowed(segmentId, isStartNode);
                configurable = junctionRManager.IsUturnAllowedConfigurable(
                    segmentId: segmentId,
                    startNode: isStartNode,
                    node: ref node);

                if (this.debug_
                    || (configurable
                        && (!this.ViewOnly
                            || (allowed != junctionRManager.GetDefaultUturnAllowed(
                                    segmentId: segmentId,
                                    startNode: isStartNode,
                                    node: ref node)))))
                {
                    bool signHovered = signsLayout.DrawSign(
                        small: !configurable,
                        camPos: ref camPos,
                        guiColor: guiColor,
                        signTexture: theme.GetOtherRestriction(
                            RoadSignTheme.OtherRestriction.UTurn,
                            allowed));

                    if (signHovered && this.handleClick_) {
                        isAnyHovered = true;

                        if (this.mainTool_.CheckClicked()) {
                            if (!junctionRManager.ToggleUturnAllowed(
                                    segmentId,
                                    isStartNode)) {
                                // TODO MainTool.ShowTooltip(Translation.GetString("..."), node.m_position);
                            } else {
                                stateUpdated = true;
                            }
                        }
                    }
                }

                // draw "entering blocked junctions allowed" sign at (0; 1)
                allowed = junctionRManager.IsEnteringBlockedJunctionAllowed(
                    segmentId: segmentId,
                    startNode: isStartNode);
                configurable = junctionRManager.IsEnteringBlockedJunctionAllowedConfigurable(
                    segmentId: segmentId,
                    startNode: isStartNode,
                    node: ref node);

                if (this.debug_
                    || (configurable
                        && (!this.ViewOnly
                            || (allowed != junctionRManager
                                                    .GetDefaultEnteringBlockedJunctionAllowed(
                                                        segmentId,
                                                        isStartNode,
                                                        ref node)))))
                {
                    bool signHovered = signsLayout.DrawSign(
                        small: !configurable,
                        camPos: ref camPos,
                        guiColor: guiColor,
                        signTexture: theme.GetOtherRestriction(
                            RoadSignTheme.OtherRestriction.EnterBlockedJunction,
                            allowed));

                    if (signHovered && this.handleClick_) {
                        isAnyHovered = true;

                        if (this.mainTool_.CheckClicked()) {
                            junctionRManager.ToggleEnteringBlockedJunctionAllowed(
                                segmentId: segmentId,
                                startNode: isStartNode);
                            stateUpdated = true;
                        }
                    }
                }

                // draw "pedestrian crossing allowed" sign at (1; 1)
                allowed = junctionRManager.IsPedestrianCrossingAllowed(
                    segmentId: segmentId,
                    startNode: isStartNode);
                configurable = junctionRManager.IsPedestrianCrossingAllowedConfigurable(
                    segmentId: segmentId,
                    startNode: isStartNode,
                    node: ref node);

                if (this.debug_
                    || (configurable
                        && (!this.ViewOnly || !allowed))) {
                    bool signHovered = signsLayout.DrawSign(
                        small: !configurable,
                        camPos: ref camPos,
                        guiColor: guiColor,
                        signTexture: theme.GetOtherRestriction(
                            RoadSignTheme.OtherRestriction.Crossing,
                            allowed));

                    if (signHovered && this.handleClick_) {
                        isAnyHovered = true;

                        if (this.mainTool_.CheckClicked()) {
                            junctionRManager.TogglePedestrianCrossingAllowed(
                                segmentId,
                                isStartNode);
                            stateUpdated = true;
                        }
                    }
                }

                if (!SavedGameOptions.Instance.turnOnRedEnabled) {
                    continue;
                }

                //--------------------------------
                // TURN ON RED ENABLED
                //--------------------------------
                bool leftSideTraffic = Shortcuts.LHT;

                // draw "turn-left-on-red allowed" sign at (2; 0)
                allowed = junctionRManager.IsTurnOnRedAllowed(
                    near: leftSideTraffic,
                    segmentId: segmentId,
                    startNode: isStartNode);
                configurable = junctionRManager.IsTurnOnRedAllowedConfigurable(
                    near: leftSideTraffic,
                    segmentId: segmentId,
                    startNode: isStartNode,
                    node: ref node);

                if (this.debug_
                    || (configurable
                        && (!this.ViewOnly
                            || (allowed != junctionRManager.GetDefaultTurnOnRedAllowed(
                                    near: leftSideTraffic,
                                    segmentId: segmentId,
                                    startNode: isStartNode,
                                    node: ref node)))))
                {
                    bool signHovered = signsLayout.DrawSign(
                        small: !configurable,
                        camPos: ref camPos,
                        guiColor: guiColor,
                        signTexture: theme.GetOtherRestriction(
                            RoadSignTheme.OtherRestriction.LeftOnRed,
                            allowed));

                    if (signHovered && this.handleClick_) {
                        isAnyHovered = true;

                        if (this.mainTool_.CheckClicked()) {
                            junctionRManager.ToggleTurnOnRedAllowed(
                                near: leftSideTraffic,
                                segmentId: segmentId,
                                startNode: isStartNode);
                            stateUpdated = true;
                        }
                    }
                }

                // draw "turn-right-on-red allowed" sign at (2; 1)
                allowed = junctionRManager.IsTurnOnRedAllowed(
                    near: !leftSideTraffic,
                    segmentId: segmentId,
                    startNode: isStartNode);
                configurable = junctionRManager.IsTurnOnRedAllowedConfigurable(
                    near: !leftSideTraffic,
                    segmentId: segmentId,
                    startNode: isStartNode,
                    node: ref node);

                if (this.debug_
                    || (configurable
                        && (!this.ViewOnly
                            || (allowed != junctionRManager.GetDefaultTurnOnRedAllowed(
                                    near: !leftSideTraffic,
                                    segmentId: segmentId,
                                    startNode: isStartNode,
                                    node: ref node)))))
                {
                    bool signHovered = signsLayout.DrawSign(
                        small: !configurable,
                        camPos: ref camPos,
                        guiColor: guiColor,
                        signTexture: theme.GetOtherRestriction(
                            RoadSignTheme.OtherRestriction.RightOnRed,
                            allowed));

                    if (signHovered && this.handleClick_) {
                        isAnyHovered = true;

                        if (this.mainTool_.CheckClicked()) {
                            junctionRManager.ToggleTurnOnRedAllowed(
                                near: !leftSideTraffic,
                                segmentId: segmentId,
                                startNode: isStartNode);
                            stateUpdated = true;
                        }
                    }
                }
            }

            guiColor.a = 1f;
            GUI.color = guiColor;

            return isAnyHovered;
        }
    } // end class
}