namespace TrafficManager.UI.SubTools.PrioritySigns {
    using ColossalFramework;
    using TrafficManager.API.Manager;
    using TrafficManager.Manager.Impl;
    using TrafficManager.State;
    using TrafficManager.UI.Textures;
    using TrafficManager.Util;
    using UnityEngine;

    /// <summary>
    /// Class handles rendering of priority signs overlay.
    /// Create one and set its fields before calling DrawSignHandles
    /// </summary>
    public struct Overlay {
        private const float JUNCTION_RESTRICTIONS_SIGN_SIZE = 80f;

        private readonly TrafficManagerTool mainTool_;
        private readonly bool debug_;
        private readonly bool handleClick_;

        public bool ViewOnly;

        /// <summary>
        /// Handles layout for the Junction Restriction signs being rendered.
        /// One <see cref="SignsLayout"/> is created per junction.
        /// Defines basis of rotated coordinate system, aligned somewhere near the node center,
        /// and directed along the road segment.
        /// </summary>
        private struct SignsLayout {
            private Vector3 zero_;

            /// <summary>Unit vector perpendicular to the vector towards the segment center.</summary>
            public Vector3 yBasis;

            /// <summary>Unit vector towards the segment center.</summary>
            public Vector3 xBasis;

            private readonly int signsPerRow_;
            private readonly bool viewOnly_;

            /// <summary>Zoom level inherited from the MainTool.</summary>
            private readonly float baseZoom_;

            private float signSize_;

            /// <summary>Horizontal position when placing signs in a grid.</summary>
            private int xPosition;

            /// <summary>Vertical position when placing signs in a grid.</summary>
            private int yPosition;

            public SignsLayout(Vector3 nodePos,
                               Vector3 yBasis,
                               int signsPerRow,
                               bool viewOnly,
                               float baseZoom) {
                this.signsPerRow_ = signsPerRow;
                this.viewOnly_ = viewOnly;
                baseZoom_ = baseZoom;

                this.xBasis = yBasis;
                this.yBasis = Vector3.Cross(yBasis, new Vector3(0f, 1f, 0f)).normalized;

                this.signSize_ = viewOnly ? 3.5f : 6f;

                this.zero_ = GetZeroPosition(
                    nodePos: nodePos,
                    xu: this.yBasis,
                    yu: this.xBasis,
                    numSignsPerRow: signsPerRow,
                    signSize: this.signSize_,
                    viewOnly: viewOnly);
                xPosition = 0;
                yPosition = 0;

                // For view mode: Offset to the left (for Left side drive) or to the right by 8 units
                // if (viewOnly) {
                //     if (Constants.ServiceFactory.SimulationService.TrafficDrivesOnLeft) {
                //         this.zero_ -= this.yBasis * 8f;
                //     } else {
                //         this.zero_ += this.yBasis * 8f;
                //     }
                // }
            }

            /// <summary>
            /// Based on view mode, calculate zero position where the signs will start.
            /// </summary>
            /// <param name="nodePos">Center position for the junction.</param>
            /// <param name="xu">Unit vector to the right.</param>
            /// <param name="yu">Unit vector towards segment center.</param>
            /// <param name="numSignsPerRow">How many signs per row.</param>
            /// <param name="signSize">Sign size from <see cref="GetSignSize"/>.</param>
            /// <returns>Zero position where first sign will go.</returns>
            private static Vector3 GetZeroPosition(Vector3 nodePos,
                                                   Vector3 xu,
                                                   Vector3 yu,
                                                   int numSignsPerRow,
                                                   float signSize,
                                                   bool viewOnly) {
                // Vector3 centerStart = nodePos + (yu * (viewOnly ? 5f : 14f));
                Vector3 centerStart = nodePos + (yu * 14f);
                Vector3 zero = centerStart - (0.5f * (numSignsPerRow - 1) * signSize * xu); // "top left"
                return zero;
            }

            public bool DrawSign(bool small,
                                 ref Vector3 camPos,
                                 Color guiColor,
                                 Texture2D signTexture)
            {
                // World coordinates, where 1 unit = 1 m
                Vector3 signCenter = this.zero_
                                     + (this.signSize_ * this.xPosition * this.yBasis)
                                     + (this.signSize_ * this.yPosition * this.xBasis);
                bool visible = GeometryUtil.WorldToScreenPoint(worldPos: signCenter,
                                                               screenPos: out Vector3 signScreenPos);

                if (!visible) {
                    return false;
                }

                Vector3 diff = signCenter - camPos;
                float zoom = 100.0f * this.baseZoom_ / diff.magnitude;
                float size = (small ? 0.75f : 1f)
                             * (this.viewOnly_ ? 0.8f : 1f)
                             * JUNCTION_RESTRICTIONS_SIGN_SIZE * zoom;

                var boundingBox = new Rect(
                    x: signScreenPos.x - (size / 2),
                    y: signScreenPos.y - (size / 2),
                    width: size,
                    height: size);

                bool hoveredHandle = !this.viewOnly_ && TrafficManagerTool.IsMouseOver(boundingBox);
                if (this.viewOnly_) {
                    // Readonly signs look grey-ish
                    guiColor = Color.Lerp(guiColor, Color.gray, 0.5f);
                    guiColor.a = TrafficManagerTool.GetHandleAlpha(hovered: false);
                } else {
                    // Handles in edit mode are always visible. Hovered handles are also highlighted.
                    guiColor.a = 1f;

                    if (hoveredHandle) {
                        guiColor = Color.Lerp(
                            a: guiColor,
                            b: new Color(r: 1f, g: .7f, b: 0f),
                            t: 0.5f);
                    }
                }
                // guiColor.a = TrafficManagerTool.GetHandleAlpha(hoveredHandle);

                GUI.color = guiColor;
                GUI.DrawTexture(boundingBox, signTexture);
                return hoveredHandle;
            }

            public void AdvancePosition() {
                this.xPosition++;
                if (this.xPosition >= this.signsPerRow_) {
                    this.xPosition = 0;
                    this.yPosition++;
                }
            }
        }

        /// <summary>Initializes a new instance of the <see cref="Overlay"/> struct for rendering.</summary>
        /// <param name="mainTool">Parent <see cref="TrafficManagerTool"/>.</param>
        /// <param name="debug">Is debug rendering on.</param>
        /// <param name="handleClick">Whether clicks are to be handled.</param>
        public Overlay(TrafficManagerTool mainTool,
                       bool debug,
                       bool handleClick) {
            mainTool_ = mainTool;
            debug_ = debug;
            handleClick_ = handleClick;
            ViewOnly = true;
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
            //   * and is not Prio Signs tool
            if (this.ViewOnly &&
                !(Options.junctionRestrictionsOverlay ||
                  MassEditOverlay.IsActive) &&
                this.mainTool_.GetToolMode() != ToolMode.JunctionRestrictions) {
                return false;
            }

            // NetManager netManager = Singleton<NetManager>.instance;
            Color guiColor = GUI.color;
            Vector3 nodePos = Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId].m_position;
            IExtSegmentEndManager segEndMan = Constants.ManagerFactory.ExtSegmentEndManager;

            for (int i = 0; i < 8; ++i) {
                ushort segmentId = node.GetSegment(i);

                if (segmentId == 0) {
                    continue;
                }

                bool isStartNode =
                    (bool)Constants.ServiceFactory.NetService.IsStartNode(segmentId, nodeId);

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

                //------------------------------------
                // Draw all junction restriction signs
                // Determine direction from node center towards each segment center and use that
                // as axis Y, and then dot product gives "horizontal" axis X
                //------------------------------------
                Vector3 segmentCenterPos = Singleton<NetManager>
                                           .instance
                                           .m_segments
                                           .m_buffer[segmentId]
                                           .m_bounds
                                           .center;

                SignsLayout signsLayout = new SignsLayout(
                    nodePos: nodePos,
                    yBasis: (segmentCenterPos - nodePos).normalized,
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
                        signTexture: allowed
                                         ? JunctionRestrictions.LaneChangeAllowed
                                         : JunctionRestrictions.LaneChangeForbidden);

                    if (signHovered && this.handleClick_) {
                        isAnyHovered = true;
                        if (this.mainTool_.CheckClicked()) {
                            junctionRManager.ToggleLaneChangingAllowedWhenGoingStraight(
                                segmentId: segmentId,
                                startNode: isStartNode);
                            stateUpdated = true;
                        }
                    }

                    signsLayout.AdvancePosition();
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
                        signTexture: allowed
                                         ? JunctionRestrictions.UturnAllowed
                                         : JunctionRestrictions.UturnForbidden);

                    if (signHovered && this.handleClick_) {
                        isAnyHovered = true;

                        if (this.mainTool_.CheckClicked()) {
                            if (!junctionRManager.ToggleUturnAllowed(
                                    segmentId,
                                    isStartNode)) {
                                // TODO MainTool.ShowTooltip(Translation.GetString("..."), Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId].m_position);
                            } else {
                                stateUpdated = true;
                            }
                        }
                    }

                    signsLayout.AdvancePosition();
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
                        signTexture: allowed
                                         ? JunctionRestrictions.EnterBlockedJunctionAllowed
                                         : JunctionRestrictions.EnterBlockedJunctionForbidden);

                    if (signHovered && this.handleClick_) {
                        isAnyHovered = true;

                        if (this.mainTool_.CheckClicked()) {
                            junctionRManager.ToggleEnteringBlockedJunctionAllowed(
                                segmentId: segmentId,
                                startNode: isStartNode);
                            stateUpdated = true;
                        }
                    }

                    signsLayout.AdvancePosition();
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
                        signTexture: allowed
                                         ? JunctionRestrictions.PedestrianCrossingAllowed
                                         : JunctionRestrictions.PedestrianCrossingForbidden);

                    if (signHovered && this.handleClick_) {
                        isAnyHovered = true;

                        if (this.mainTool_.CheckClicked()) {
                            junctionRManager.TogglePedestrianCrossingAllowed(
                                segmentId,
                                isStartNode);
                            stateUpdated = true;
                        }
                    }

                    signsLayout.AdvancePosition();
                }

                if (!Options.turnOnRedEnabled) {
                    continue;
                }

                //--------------------------------
                // TURN ON RED ENABLED
                //--------------------------------
                bool leftSideTraffic = Constants.ServiceFactory.SimulationService.TrafficDrivesOnLeft;

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
                        signTexture: allowed
                                         ? JunctionRestrictions.LeftOnRedAllowed
                                         : JunctionRestrictions.LeftOnRedForbidden);

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

                    signsLayout.AdvancePosition();
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
                        signTexture: allowed
                                         ? JunctionRestrictions.RightOnRedAllowed
                                         : JunctionRestrictions.RightOnRedForbidden);

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

                    signsLayout.AdvancePosition();
                }
            }

            guiColor.a = 1f;
            GUI.color = guiColor;

            return isAnyHovered;
        }
    } // end class
}