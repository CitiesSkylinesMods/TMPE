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
    /// </summary>
    public static class Overlay {
        private const float JUNCTION_RESTRICTIONS_SIGN_SIZE = 80f;

        public static bool DrawSignHandles(TrafficManagerTool mainTool,
                                           bool debug,
                                           ushort nodeId,
                                           ref NetNode node,
                                           bool viewOnly,
                                           bool handleClick,
                                           ref Vector3 camPos,
                                           out bool stateUpdated) {
            bool hovered = false;
            stateUpdated = false;

            if (viewOnly &&
                !(Options.junctionRestrictionsOverlay ||
                  MassEditOverlay.IsActive) &&
                (mainTool.GetToolMode() != ToolMode.JunctionRestrictions)) {
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

                bool startNode =
                    (bool)Constants.ServiceFactory.NetService.IsStartNode(segmentId, nodeId);
                bool incoming = segEndMan.ExtSegmentEnds[segEndMan.GetIndex(segmentId, startNode)]
                                         .incoming;

                int numSignsPerRow = incoming ? 2 : 1;

                NetInfo segmentInfo =
                    Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].Info;

                ItemClass connectionClass = segmentInfo.GetConnectionClass();

                if (connectionClass.m_service != ItemClass.Service.Road) {
                    continue; // only for road junctions
                }

                // draw all junction restriction signs
                Vector3 segmentCenterPos = Singleton<NetManager>
                                           .instance.m_segments.m_buffer[segmentId]
                                           .m_bounds.center;
                Vector3 yu = (segmentCenterPos - nodePos).normalized;
                Vector3 xu = Vector3.Cross(yu, new Vector3(0, 1f, 0)).normalized;
                float f = viewOnly ? 6f : 7f; // reserved sign size in game coordinates

                Vector3 centerStart = nodePos + (yu * (viewOnly ? 5f : 14f));
                Vector3 zero = centerStart - (0.5f * (numSignsPerRow - 1) * f * xu); // "top left"
                if (viewOnly) {
                    if (Constants.ServiceFactory.SimulationService.TrafficDrivesOnLeft) {
                        zero -= xu * 8f;
                    } else {
                        zero += xu * 8f;
                    }
                }

                bool signHovered;
                int x = 0;
                int y = 0;
                bool hasSignInPrevRow = false;

                // draw "lane-changing when going straight allowed" sign at (0; 0)
                bool allowed =
                    JunctionRestrictionsManager.Instance.IsLaneChangingAllowedWhenGoingStraight(
                        segmentId,
                        startNode);

                bool configurable =
                    Constants.ManagerFactory.JunctionRestrictionsManager
                             .IsLaneChangingAllowedWhenGoingStraightConfigurable(
                                 segmentId,
                                 startNode,
                                 ref node);

                if (debug
                    || (configurable
                        && (!viewOnly
                            || (allowed != Constants.ManagerFactory
                                                    .JunctionRestrictionsManager
                                                    .GetDefaultLaneChangingAllowedWhenGoingStraight(
                                                        segmentId,
                                                        startNode,
                                                        ref node))))) {
                    DrawSign(
                        mainTool: mainTool,
                        viewOnly: viewOnly,
                        small: !configurable,
                        camPos: ref camPos,
                        xu: ref xu,
                        yu: ref yu,
                        f: f,
                        zero: ref zero,
                        x: x,
                        y: y,
                        guiColor: guiColor,
                        signTexture: allowed
                                         ? JunctionRestrictions.LaneChangeAllowed
                                         : JunctionRestrictions.LaneChangeForbidden,
                        hoveredHandle: out signHovered);

                    if (signHovered && handleClick) {
                        hovered = true;
                        if (mainTool.CheckClicked()) {
                            JunctionRestrictionsManager
                                .Instance.ToggleLaneChangingAllowedWhenGoingStraight(
                                    segmentId,
                                    startNode);
                            stateUpdated = true;
                        }
                    }

                    ++x;
                    hasSignInPrevRow = true;
                }

                // draw "u-turns allowed" sign at (1; 0)
                allowed = JunctionRestrictionsManager.Instance.IsUturnAllowed(segmentId, startNode);
                configurable =
                    Constants.ManagerFactory.JunctionRestrictionsManager.IsUturnAllowedConfigurable(
                        segmentId,
                        startNode,
                        ref node);
                if (debug
                    || (configurable
                        && (!viewOnly
                            || (allowed != Constants.ManagerFactory
                                                    .JunctionRestrictionsManager
                                                    .GetDefaultUturnAllowed(
                                                        segmentId,
                                                        startNode,
                                                        ref node))))) {
                    DrawSign(
                        mainTool: mainTool,
                        viewOnly: viewOnly,
                        small: !configurable,
                        camPos: ref camPos,
                        xu: ref xu,
                        yu: ref yu,
                        f: f,
                        zero: ref zero,
                        x: x,
                        y: y,
                        guiColor: guiColor,
                        signTexture: allowed
                                         ? JunctionRestrictions.UturnAllowed
                                         : JunctionRestrictions.UturnForbidden,
                        hoveredHandle: out signHovered);

                    if (signHovered && handleClick) {
                        hovered = true;

                        if (mainTool.CheckClicked()) {
                            if (!JunctionRestrictionsManager.Instance.ToggleUturnAllowed(
                                    segmentId,
                                    startNode)) {
                                // TODO MainTool.ShowTooltip(Translation.GetString("..."), Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId].m_position);
                            } else {
                                stateUpdated = true;
                            }
                        }
                    }

                    x++;
                    hasSignInPrevRow = true;
                }

                x = 0;
                if (hasSignInPrevRow) {
                    ++y;
                    hasSignInPrevRow = false;
                }

                // draw "entering blocked junctions allowed" sign at (0; 1)
                allowed = JunctionRestrictionsManager.Instance.IsEnteringBlockedJunctionAllowed(
                    segmentId,
                    startNode);
                configurable =
                    Constants.ManagerFactory.JunctionRestrictionsManager
                             .IsEnteringBlockedJunctionAllowedConfigurable(
                                 segmentId,
                                 startNode,
                                 ref node);

                if (debug
                    || (configurable
                        && (!viewOnly
                            || (allowed != Constants.ManagerFactory
                                                    .JunctionRestrictionsManager
                                                    .GetDefaultEnteringBlockedJunctionAllowed(
                                                        segmentId,
                                                        startNode,
                                                        ref node))))) {
                    DrawSign(
                        mainTool: mainTool,
                        viewOnly: viewOnly,
                        small: !configurable,
                        camPos: ref camPos,
                        xu: ref xu,
                        yu: ref yu,
                        f: f,
                        zero: ref zero,
                        x: x,
                        y: y,
                        guiColor: guiColor,
                        signTexture: allowed
                                         ? JunctionRestrictions.EnterBlockedJunctionAllowed
                                         : JunctionRestrictions.EnterBlockedJunctionForbidden,
                        hoveredHandle: out signHovered);

                    if (signHovered && handleClick) {
                        hovered = true;

                        if (mainTool.CheckClicked()) {
                            JunctionRestrictionsManager
                                .Instance
                                .ToggleEnteringBlockedJunctionAllowed(segmentId, startNode);
                            stateUpdated = true;
                        }
                    }

                    ++x;
                    hasSignInPrevRow = true;
                }

                // draw "pedestrian crossing allowed" sign at (1; 1)
                allowed = JunctionRestrictionsManager.Instance.IsPedestrianCrossingAllowed(
                    segmentId,
                    startNode);
                configurable =
                    Constants.ManagerFactory.JunctionRestrictionsManager
                             .IsPedestrianCrossingAllowedConfigurable(
                                 segmentId,
                                 startNode,
                                 ref node);

                if (debug
                    || (configurable
                        && (!viewOnly || !allowed))) {
                    DrawSign(
                        mainTool: mainTool,
                        viewOnly: viewOnly,
                        small: !configurable,
                        camPos: ref camPos,
                        xu: ref xu,
                        yu: ref yu,
                        f: f,
                        zero: ref zero,
                        x: x,
                        y: y,
                        guiColor: guiColor,
                        signTexture: allowed
                                         ? JunctionRestrictions.PedestrianCrossingAllowed
                                         : JunctionRestrictions.PedestrianCrossingForbidden,
                        hoveredHandle: out signHovered);

                    if (signHovered && handleClick) {
                        hovered = true;

                        if (mainTool.CheckClicked()) {
                            JunctionRestrictionsManager.Instance.TogglePedestrianCrossingAllowed(
                                segmentId,
                                startNode);
                            stateUpdated = true;
                        }
                    }

                    x++;
                    hasSignInPrevRow = true;
                }

                x = 0;

                if (hasSignInPrevRow) {
                    ++y;
                    hasSignInPrevRow = false;
                }

                if (!Options.turnOnRedEnabled) {
                    continue;
                }

                //--------------------------------
                // TURN ON RED ENABLED
                //--------------------------------
                IJunctionRestrictionsManager junctionRestrictionsManager =
                    Constants.ManagerFactory.JunctionRestrictionsManager;
                bool lht = Constants.ServiceFactory.SimulationService.TrafficDrivesOnLeft;

                // draw "turn-left-on-red allowed" sign at (2; 0)
                allowed = junctionRestrictionsManager.IsTurnOnRedAllowed(lht, segmentId, startNode);
                configurable = junctionRestrictionsManager.IsTurnOnRedAllowedConfigurable(
                    lht,
                    segmentId,
                    startNode,
                    ref node);

                if (debug
                    || (configurable
                        && (!viewOnly
                            || (allowed != junctionRestrictionsManager
                                    .GetDefaultTurnOnRedAllowed(
                                        lht,
                                        segmentId,
                                        startNode,
                                        ref node))))) {
                    DrawSign(
                        mainTool: mainTool,
                        viewOnly: viewOnly,
                        small: !configurable,
                        camPos: ref camPos,
                        xu: ref xu,
                        yu: ref yu,
                        f: f,
                        zero: ref zero,
                        x: x,
                        y: y,
                        guiColor: guiColor,
                        signTexture: allowed
                                         ? JunctionRestrictions.LeftOnRedAllowed
                                         : JunctionRestrictions.LeftOnRedForbidden,
                        hoveredHandle: out signHovered);

                    if (signHovered && handleClick) {
                        hovered = true;

                        if (mainTool.CheckClicked()) {
                            junctionRestrictionsManager.ToggleTurnOnRedAllowed(
                                lht,
                                segmentId,
                                startNode);
                            stateUpdated = true;
                        }
                    }

                    hasSignInPrevRow = true;
                }

                x++;

                // draw "turn-right-on-red allowed" sign at (2; 1)
                allowed = junctionRestrictionsManager.IsTurnOnRedAllowed(
                    !lht,
                    segmentId,
                    startNode);
                configurable = junctionRestrictionsManager.IsTurnOnRedAllowedConfigurable(
                    !lht,
                    segmentId,
                    startNode,
                    ref node);

                if (debug
                    || (configurable
                        && (!viewOnly
                            || (allowed != junctionRestrictionsManager
                                    .GetDefaultTurnOnRedAllowed(
                                        !lht,
                                        segmentId,
                                        startNode,
                                        ref node))))) {
                    DrawSign(
                        mainTool: mainTool,
                        viewOnly: viewOnly,
                        small: !configurable,
                        camPos: ref camPos,
                        xu: ref xu,
                        yu: ref yu,
                        f: f,
                        zero: ref zero,
                        x: x,
                        y: y,
                        guiColor: guiColor,
                        signTexture: allowed
                                         ? JunctionRestrictions.RightOnRedAllowed
                                         : JunctionRestrictions.RightOnRedForbidden,
                        hoveredHandle: out signHovered);

                    if (signHovered && handleClick) {
                        hovered = true;

                        if (mainTool.CheckClicked()) {
                            junctionRestrictionsManager.ToggleTurnOnRedAllowed(
                                !lht,
                                segmentId,
                                startNode);
                            stateUpdated = true;
                        }
                    }

                    hasSignInPrevRow = true;
                }
            }

            guiColor.a = 1f;
            GUI.color = guiColor;

            return hovered;
        }

        private static void DrawSign(TrafficManagerTool mainTool,
                                     bool viewOnly,
                                     bool small,
                                     ref Vector3 camPos,
                                     ref Vector3 xu,
                                     ref Vector3 yu,
                                     float f,
                                     ref Vector3 zero,
                                     int x,
                                     int y,
                                     Color guiColor,
                                     Texture2D signTexture,
                                     out bool hoveredHandle) {
            Vector3 signCenter = zero + (f * x * xu) + (f * y * yu); // in game coordinates
            bool visible = GeometryUtil.WorldToScreenPoint(signCenter, out Vector3 signScreenPos);

            if (!visible) {
                hoveredHandle = false;
                return;
            }

            Vector3 diff = signCenter - camPos;
            float zoom = (1.0f / diff.magnitude) * 100f * mainTool.GetBaseZoom();
            float size = (small ? 0.75f : 1f) * (viewOnly ? 0.8f : 1f) *
                         JUNCTION_RESTRICTIONS_SIGN_SIZE * zoom;

            var boundingBox = new Rect(
                signScreenPos.x - (size / 2),
                signScreenPos.y - (size / 2),
                size,
                size);
            hoveredHandle = !viewOnly && TrafficManagerTool.IsMouseOver(boundingBox);
            guiColor.a = TrafficManagerTool.GetHandleAlpha(hoveredHandle);

            GUI.color = guiColor;
            GUI.DrawTexture(boundingBox, signTexture);
        }
    } // end class
}