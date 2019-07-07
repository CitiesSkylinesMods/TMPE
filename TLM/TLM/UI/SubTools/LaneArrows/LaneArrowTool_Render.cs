//------------------------------------------------------------------------------
// Rendering part of the Lane Arrow Tool
// Handles drawing overlays and other visual effects
//------------------------------------------------------------------------------
namespace TrafficManager.UI.SubTools.LaneArrows {
    using System;
    using System.Collections.Generic;
    using ColossalFramework;
    using ColossalFramework.Math;
    using CSUtil.Commons;
    using Texture;
    using UnityEngine;
    using Util;

    public partial class LaneArrowTool {
        /// <summary>
        /// Translucent silver. Available nodes for clicking
        /// </summary>
        private static readonly Color PALETTE_AVAILABLE_NODE = new Color(0.7f, 0.7f, 0.7f, 0.5f);

        /// <summary>
        /// White. New hovered node in node selection mode.
        /// </summary>
        private static readonly Color PALETTE_HOVERED = Color.white;

        /// <summary>
        /// Blue solid. Currently (active) selected node.
        /// </summary>
        private static readonly Color PALETTE_SELECTED = new Color(0.7f, 0.7f, 1f, 0.7f);

        /// <summary>
        /// Pulsating color for clicking candidates (first color)
        /// </summary>
        private static readonly Color PALETTE_POSSIBLE_CANDIDATES1 = new Color(0.3f, 0.3f, 0.5f, 1f);

        /// <summary>
        /// Pulsating color for clicking candidates (second color)
        /// </summary>
        private static readonly Color PALETTE_POSSIBLE_CANDIDATES2 = new Color(0.0f, 0.0f, 0.5f, 1f);

        /// <summary>
        /// White translucent. Outline for current segment, when we are operating lanes.
        /// </summary>
        private static readonly Color PALETTE_CURRENT_SEGMENT_GHOST = new Color(1f, 1f, 1f, 0.33f);

        /// <summary>
        /// Translucent white, for allowed turns and lane links.
        /// </summary>
        private static readonly Color PALETTE_TURN_ALLOWED = new Color(1f, 1f, 1f, 0.75f);

        /// <summary>
        /// Translucent blue, for hovering segments for setting up turns.
        /// </summary>
        private static readonly Color PALETTE_TURN_HOVERED = new Color(0f, 0f, 1f, 0.75f);

        /// <summary>
        /// Used for turn circles which are not allowed but should be visible
        /// </summary>
        private static readonly Color PALETTE_TURN_INACTIVE = Color.black;

        private void RenderHoveredNode(RenderManager.CameraInfo cameraInfo) {
            // Render currently selected node
            if (HoveredNodeId != 0 && HoveredNodeId != SelectedNodeId
                                   && IsNodeEditable(HoveredNodeId)) {
                RenderNodeOverlay(cameraInfo, ref World.NodeRef(HoveredNodeId), PALETTE_HOVERED);
            }

            var camPos = Singleton<SimulationManager>.instance.m_simulationView.m_position;

            // Terrible performance here (possibly)
            for (ushort nodeId = 1; nodeId < NetManager.MAX_NODE_COUNT; ++nodeId) {
                if (!Constants.ServiceFactory.NetService.IsNodeValid(nodeId)
                    || nodeId == HoveredNodeId
                    || nodeId == SelectedNodeId
                    || !IsNodeEditable(nodeId)) {
                    continue;
                }

                var diff = World.Node(nodeId).m_position - camPos;
                if (diff.magnitude > TrafficManagerTool.MAX_OVERLAY_DISTANCE) {
                    continue; // do not draw if too distant
                }

                RenderNodeOverlay(cameraInfo, ref World.NodeRef(nodeId), PALETTE_AVAILABLE_NODE);
            }
        }

        /// <summary>
        /// Render selection and hovered nodes and segments
        /// </summary>
        /// <param name="cameraInfo">The camera</param>
        public override void RenderOverlay(RenderManager.CameraInfo cameraInfo) {
            switch (fsm_.State) {
                case State.NodeSelect:
                    RenderOverlay_NodeSelect(cameraInfo);
                    break;
                case State.IncomingSelect:
                    RenderOverlay_IncomingSelect(cameraInfo);
                    break;
                case State.OutgoingDirections:
                    RenderOverlay_OutgoingDirections(cameraInfo);
                    break;
                case State.Off:
                    break;
            }
        }

        /// <summary>
        /// Highlight the selected node (white)
        /// Highlight the other hovered node than the selected (blue).
        /// </summary>
        /// <param name="cameraInfo">The camera</param>
        private void RenderOverlay_NodeSelect(RenderManager.CameraInfo cameraInfo) {
            var nodeBuffer = Singleton<NetManager>.instance.m_nodes.m_buffer;
            RenderHoveredNode(cameraInfo);

            // Draw the selected node
            if (SelectedNodeId != 0) {
                RenderNodeOverlay(cameraInfo,
                                  ref nodeBuffer[SelectedNodeId],
                                  PALETTE_SELECTED);
            }
        }

        /// <summary>
        /// Draw incoming lane choices, the hovered lane, and the selected node
        /// </summary>
        /// <param name="cameraInfo">The camera</param>
        private void RenderOverlay_IncomingSelect(RenderManager.CameraInfo cameraInfo) {
            RenderHoveredNode(cameraInfo);

            // Draw the selected node with blue
            // RenderNodeOverlay(cameraInfo, ref nodeBuffer[SelectedNodeId], PALETTE_SELECTED);

            // Draw hovered lane with 2 metres white sausage
            if (HoveredLaneId != 0 && incomingLanes_.Contains(HoveredLaneId)) {
                // TODO: Check if hovered lane has SelectedNodeId as one of its ends
                RenderLaneOverlay(cameraInfo, World.Lane(HoveredLaneId), 2f, PALETTE_HOVERED);

                RenderOutgoingDirectionsAsLanes(cameraInfo, HoveredLaneId,
                                                RenderOutgoingLaneStyle.LaneLinks);
            }

            // Draw the incoming lanes in black/blue pulsating color
            var pulsatingColor = GetPulsatingColor(PALETTE_POSSIBLE_CANDIDATES1,
                                                   PALETTE_POSSIBLE_CANDIDATES2,
                                                   0.5f);
            foreach (var laneId in incomingLanes_) {
                if (laneId != HoveredLaneId) {
                    RenderLaneOverlay(cameraInfo, World.Lane(laneId), 1f, pulsatingColor);
                }
            }
        }

        /// <summary>
        /// Produces pulsating color linearly between colors a and b, with given time period.
        /// </summary>
        /// <param name="a">Color from</param>
        /// <param name="b">Color to</param>
        /// <param name="periodSec">How soon the pulsation repeats</param>
        /// <returns>The linearly interpolated color between A and B</returns>
        private static Color GetPulsatingColor(Color a, Color b, float periodSec) {
            var t = Time.time * periodSec * 2f * Mathf.PI;
            var pulsatingColor = Color.Lerp(a, b, Mathf.Sin(t));
            return pulsatingColor;
        }

        /// <summary>
        /// Render directions leaving the selected node (segments)
        /// </summary>
        /// <param name="cameraInfo">The camera</param>
        private void RenderOverlay_OutgoingDirections(RenderManager.CameraInfo cameraInfo) {
            RenderHoveredNode(cameraInfo);

            // Draw selected node with blue
            // RenderNodeOverlay(cameraInfo, ref nodeBuffer[SelectedNodeId], PALETTE_SELECTED);

            // Draw selected lane with orange
            var orange = MainTool.GetToolColor(true, false);
            RenderLaneOverlay(cameraInfo, World.Lane(SelectedLaneId), 2f, orange);

            RenderOutgoingDirectionsAsLanes(cameraInfo, SelectedLaneId,
                                            RenderOutgoingLaneStyle.SausagesAndLinks);
        }

        private enum RenderOutgoingLaneStyle {
            SausagesAndLinks,
            LaneLinks
        }

        /// <summary>
        /// A helper which relies on outgoingTurns_ being set up, renders outgoing lanes
        /// for the given laneId, in green
        /// </summary>
        /// <param name="cameraInfo"></param>
        /// <param name="laneId"></param>
        private void RenderOutgoingDirectionsAsLanes(RenderManager.CameraInfo cameraInfo,
                                                     uint laneId,
                                                     RenderOutgoingLaneStyle style) {
            if (!outgoingTurns_.HasValue || laneId == 0) {
                return;
            }

            // Draw outgoing directions for the last selected segment
            var selectedLane = World.Lane(laneId);
            var selectedLaneFlags = (NetLane.Flags) selectedLane.m_flags;

            // Turns out of this node converted to lanes (we want all turn bits here)
            var outgoingLaneTurns = outgoingTurns_?.GetLanesFor(NetLane.Flags.LeftForwardRight);
            var hoveredDirection = outgoingTurns_?.FindDirection(HoveredSegmentId);

            foreach (var outDirections in outgoingTurns_?.AllTurns) {
                switch (outDirections.Key) {
                    case ArrowDirection.Left:
                        RenderOutgoingDirectionsAsLanes_One(
                            cameraInfo, style, laneId,
                            (selectedLaneFlags & NetLane.Flags.Left) != 0,
                            hoveredDirection == ArrowDirection.Left,
                            outgoingLaneTurns[outDirections.Key]);
                        break;
                    case ArrowDirection.Forward:
                        RenderOutgoingDirectionsAsLanes_One(
                            cameraInfo, style, laneId,
                            (selectedLaneFlags & NetLane.Flags.Forward) != 0,
                            hoveredDirection == ArrowDirection.Forward,
                            outgoingLaneTurns[outDirections.Key]);
                        break;
                    case ArrowDirection.Right:
                        RenderOutgoingDirectionsAsLanes_One(
                            cameraInfo, style, laneId,
                            (selectedLaneFlags & NetLane.Flags.Right) != 0,
                            hoveredDirection == ArrowDirection.Right,
                            outgoingLaneTurns[outDirections.Key]);
                        break;
                    case ArrowDirection.Turn:
                    case ArrowDirection.None:
                        break;
                }
            }
        }

        /// <summary>
        /// Render outgoing directions as separate lanes
        /// </summary>
        /// <param name="cameraInfo">The camera</param>
        /// <param name="turnEnabled">Render green if the turn is enabled</param>
        /// <param name="turnHovered">Render green+white, or white if hovered</param>
        /// <param name="laneTurns">Set of all lane ids to render</param>
        private void RenderOutgoingDirectionsAsLanes_One(RenderManager.CameraInfo cameraInfo,
                                                         RenderOutgoingLaneStyle style,
                                                         uint fromLaneId,
                                                         bool turnEnabled,
                                                         bool turnHovered,
                                                         HashSet<uint> laneTurns) {
            var color = PALETTE_TURN_ALLOWED; // no blinking/translucent green
            if (!turnEnabled) {
                color = GetPulsatingColor(PALETTE_POSSIBLE_CANDIDATES1,
                                          PALETTE_POSSIBLE_CANDIDATES2,
                                          0.5f);
            }

            if (turnHovered) {
                // Mix with selected color 33% white | 66% hovered blue
                color = Color.Lerp(color, PALETTE_TURN_HOVERED, 0.66f);
            }

            var fromLane = World.Lane(fromLaneId);

            switch (style) {
                case RenderOutgoingLaneStyle.SausagesAndLinks:
                    foreach (var laneId in laneTurns) {
                        var toLane = World.Lane(laneId);
                        RenderLaneOverlay(cameraInfo, toLane, 2f, color);

                        if (turnEnabled) {
                            RenderLaneLink(cameraInfo,
                                           fromLane,
                                           toLane,
                                           0.1f,
                                           PALETTE_TURN_ALLOWED);
                        }
                    }

                    break;

                case RenderOutgoingLaneStyle.LaneLinks:
                    foreach (var laneId in laneTurns) {
                        var toLane = World.Lane(laneId);

                        // RenderOutgoingLaneAsCircle(cameraInfo, turnEnabled, color, lane, selectedNode);
                        if (turnEnabled) {
                            RenderLaneLink(cameraInfo,
                                           fromLane,
                                           toLane,
                                           0.1f,
                                           PALETTE_TURN_ALLOWED);
                        } else {
                            // Draw a black square on the destination lane
                            var worldPos = Geometry.GetClosestLaneEnd(toLane, World.Node(SelectedNodeId).m_position);

                            Singleton<ToolManager>.instance.m_drawCallData.m_overlayCalls++;
                            RenderManager.instance.OverlayEffect.DrawCircle(
                                cameraInfo, PALETTE_TURN_INACTIVE, worldPos,
                                1f, -1f, 1280f, false, true);
                        }
                    }

                    break;
            }
        }

        /// <summary>
        /// Draw a HOVER_RADIUS=10m circle on the node
        /// </summary>
        /// <param name="cameraInfo">The camera</param>
        /// <param name="node">The node to take the position</param>
        /// <param name="color">The color</param>
        private static void RenderNodeOverlay(RenderManager.CameraInfo cameraInfo,
                                              ref NetNode node,
                                              Color color) {
            ++Singleton<ToolManager>.instance.m_drawCallData.m_overlayCalls;

            const float NODE_HOVER_RADIUS = 10f;
            Singleton<RenderManager>.instance.OverlayEffect.DrawCircle(
                cameraInfo, color, node.m_position, NODE_HOVER_RADIUS,
                -1f, 1280f, false, false);
        }

        private static void RenderLaneOverlay(RenderManager.CameraInfo cameraInfo,
                                              NetLane lane,
                                              float width,
                                              Color color) {
            Singleton<ToolManager>.instance.m_drawCallData.m_overlayCalls++;
            Singleton<RenderManager>.instance.OverlayEffect.DrawBezier(
                cameraInfo, color, lane.m_bezier, width, -100000f, -100000f,
                -1f, 1280f, false, false);
        }

        /// <summary>Links two lanes</summary>
        /// <param name="cameraInfo">The camera</param>
        /// <param name="fromLane">The lane A</param>
        /// <param name="toLane">The lane B</param>
        /// <param name="width">Render width</param>
        /// <param name="color">Render color</param>
        private void RenderLaneLink(RenderManager.CameraInfo cameraInfo,
                                    NetLane fromLane,
                                    NetLane toLane,
                                    float width,
                                    Color color) {
            var selectedNode = World.Node(SelectedNodeId);
            var fromLanePos = Geometry.GetClosestLaneEnd(fromLane, selectedNode.m_position);
            var toLanePos = Geometry.GetClosestLaneEnd(toLane, selectedNode.m_position);

            RenderLanesJoinCurve(
                cameraInfo, fromLanePos, toLanePos, selectedNode.m_position,
                color, width);
        }


        /// <summary>
        /// Copied from LaneConnectorTool, connects two lanes
        /// </summary>
        /// <param name="cameraInfo">The camera</param>
        /// <param name="start">Start</param>
        /// <param name="end">End</param>
        /// <param name="middlePoint">Curve center</param>
        /// <param name="color">Color</param>
        /// <param name="size">Width</param>
        private void RenderLanesJoinCurve(RenderManager.CameraInfo cameraInfo,
                                          Vector3 start,
                                          Vector3 end,
                                          Vector3 middlePoint,
                                          Color color,
                                          float size = 0.1f) {
            Bezier3 bezier;
            bezier.a = start;
            bezier.d = end;
            NetSegment.CalculateMiddlePoints(
                bezier.a, (middlePoint - bezier.a).normalized, bezier.d,
                (middlePoint - bezier.d).normalized, false, false,
                out bezier.b, out bezier.c);

            Singleton<ToolManager>.instance.m_drawCallData.m_overlayCalls++;
            RenderManager.instance.OverlayEffect.DrawBezier(
                cameraInfo, color, bezier, size, 0, 0,
                -1f, 1280f, false, true);
        }
    }
}