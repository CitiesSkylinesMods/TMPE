//------------------------------------------------------------------------------
// Rendering part of the Lane Arrow Tool
// Handles drawing overlays and other visual effects
//------------------------------------------------------------------------------
namespace TrafficManager.UI.SubTools.LaneArrows {
    using System;
    using System.Collections.Generic;
    using ColossalFramework;
    using CSUtil.Commons;
    using State;
    using UnityEngine;

    public partial class LaneArrowTool {
        /// <summary>
        /// White. New hovered node in node selection mode.
        /// </summary>
        private static readonly Color PALETTE_HOVERED = Color.white;

        /// <summary>
        /// Blue solid. Currently (active) selected node.
        /// </summary>
        private static readonly Color PALETTE_SELECTED = new Color(0f, 0f, 1f, 1f);

        /// <summary>
        /// White translucent. Outline for current segment, when we are operating lanes.
        /// </summary>
        private static readonly Color PALETTE_CURRENT_SEGMENT_GHOST = new Color(1f, 1f, 1f, 0.33f);

        /// <summary>
        /// Translucent green, for allowed turns.
        /// </summary>
        private static readonly Color PALETTE_TURN_ALLOWED1 = new Color(0f, 0.5f, 0f, 0.3f);

        /// <summary>
        /// Solid green, for blinking green on allowed turn circles.
        /// </summary>
        private static readonly Color PALETTE_TURN_ALLOWED2 = new Color(0f, 1f, 0f, 0.66f);

        /// <summary>
        /// Used for turn circles which are not allowed but should be visible
        /// </summary>
        private static readonly Color PALETTE_TURN_INACTIVE = Color.black;

        private void RenderHoveredNode(RenderManager.CameraInfo cameraInfo) {
            var nodeBuffer = Singleton<NetManager>.instance.m_nodes.m_buffer;
            if (!IsCursorInAnyLaneEditor()
                && HoveredNodeId != 0 && HoveredNodeId != SelectedNodeId
                && IsNodeEditable(HoveredNodeId))
            {
                RenderNodeOverlay(cameraInfo, ref nodeBuffer[HoveredNodeId], PALETTE_HOVERED);
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
            var netManager = Singleton<NetManager>.instance;
            var laneBuffer = netManager.m_lanes.m_buffer;
            var nodeBuffer = netManager.m_nodes.m_buffer;

            RenderHoveredNode(cameraInfo);

            // Draw the selected node with blue
            RenderNodeOverlay(cameraInfo, ref nodeBuffer[SelectedNodeId], PALETTE_SELECTED);

            // Draw hovered lane with 2 metres white sausage
            if (HoveredLaneId != 0 && incomingLanes_.Contains(HoveredLaneId)) {
                // TODO: Check if hovered lane has SelectedNodeId as one of its ends
                RenderLaneOverlay(cameraInfo, laneBuffer[HoveredLaneId], 2f, PALETTE_HOVERED);

                RenderOutgoingDirectionsAsLanes(cameraInfo, HoveredLaneId,
                                                RenderOutgoingLaneStyle.Circles);
            }

            // Draw the incoming lanes in black/blue pulsating color
            var t = Time.time - (float) Math.Truncate(Time.time); // fraction
            var pulsatingColor = Color.Lerp(PALETTE_SELECTED, Color.black, t);
            foreach (var laneId in incomingLanes_) {
                if (laneId != HoveredLaneId) {
                    RenderLaneOverlay(cameraInfo, laneBuffer[laneId], 1f, pulsatingColor);
                }
            }
        }

        /// <summary>
        /// Render directions leaving the selected node (segments)
        /// </summary>
        /// <param name="cameraInfo">The camera</param>
        private void RenderOverlay_OutgoingDirections(RenderManager.CameraInfo cameraInfo) {
            var netManager = Singleton<NetManager>.instance;
            var laneBuffer = netManager.m_lanes.m_buffer;
            var nodeBuffer = netManager.m_nodes.m_buffer;

            RenderHoveredNode(cameraInfo);

            // Draw selected node with blue
            RenderNodeOverlay(cameraInfo, ref nodeBuffer[SelectedNodeId], PALETTE_SELECTED);

            // Draw selected lane with orange
            var orange = MainTool.GetToolColor(true, false);
            RenderLaneOverlay(cameraInfo, laneBuffer[SelectedLaneId], 2f, orange);

            RenderOutgoingDirectionsAsLanes(cameraInfo, SelectedLaneId,
                                            RenderOutgoingLaneStyle.Sausages);
        }

        private enum RenderOutgoingLaneStyle {
            Sausages,
            Circles
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

            var laneBuffer = Singleton<NetManager>.instance.m_lanes.m_buffer;

            // Draw outgoing directions for the last selected segment
            var selectedLane = laneBuffer[laneId];
            var selectedLaneFlags = (NetLane.Flags) selectedLane.m_flags;

            // Turns out of this node converted to lanes (we want all turn bits here)
            var outgoingLaneTurns = outgoingTurns_?.GetLanesFor(NetLane.Flags.LeftForwardRight);
            var hoveredDirection = outgoingTurns_?.FindDirection(HoveredSegmentId);

            foreach (var outDirections in outgoingTurns_?.AllTurns) {
                switch (outDirections.Key) {
                    case ArrowDirection.Left:
                        RenderOutgoingDirectionsAsLanes_One(
                            cameraInfo,
                            style,
                            (selectedLaneFlags & NetLane.Flags.Left) != 0,
                            hoveredDirection == ArrowDirection.Left,
                            outgoingLaneTurns[outDirections.Key]);
                        break;
                    case ArrowDirection.Forward:
                        RenderOutgoingDirectionsAsLanes_One(
                            cameraInfo,
                            style,
                            (selectedLaneFlags & NetLane.Flags.Forward) != 0,
                            hoveredDirection == ArrowDirection.Forward,
                            outgoingLaneTurns[outDirections.Key]);
                        break;
                    case ArrowDirection.Right:
                        RenderOutgoingDirectionsAsLanes_One(
                            cameraInfo,
                            style,
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
        private void RenderOutgoingDirectionsAsLanes_One(
            RenderManager.CameraInfo cameraInfo,
            RenderOutgoingLaneStyle style,
            bool turnEnabled,
            bool turnHovered,
            HashSet<uint> laneTurns) {
            var color = PALETTE_TURN_ALLOWED1; // no blinking/translucent green
            if (!turnEnabled) {
                // Replace with pulsating blue
                var t = Time.time - (float) Math.Truncate(Time.time); // fraction
                color = Color.Lerp(PALETTE_SELECTED, Color.black, t);
            }

            if (turnHovered) {
                // Mix with selected color 33% blue pulsating|green to 66% hovered
                color = Color.Lerp(color, PALETTE_HOVERED, 0.66f);
            }

            var laneBuffer = Singleton<NetManager>.instance.m_lanes.m_buffer;
            var nodeBuffer = Singleton<NetManager>.instance.m_nodes.m_buffer;
            var selectedNode = nodeBuffer[SelectedNodeId];

            switch (style) {
                case RenderOutgoingLaneStyle.Sausages:
                    foreach (var laneId in laneTurns) {
                        var lane = laneBuffer[laneId];
                        RenderLaneOverlay(cameraInfo, lane, 2f, color);
                    }

                    break;
                case RenderOutgoingLaneStyle.Circles:
                    foreach (var laneId in laneTurns) {
                        var lane = laneBuffer[laneId];
                        RenderOutgoingLaneAsCircle(cameraInfo, turnEnabled, color, lane, selectedNode);
                    }

                    break;
            }
        }

        /// <summary>
        /// A helper which displays outgoing lane as a circle (or maybe a sprite?)
        /// </summary>
        /// <param name="cameraInfo">The camera</param>
        /// <param name="turnEnabled">Whether the exit is possible, or blocked</param>
        /// <param name="color">Base render color (pulsating blue most likely)</param>
        /// <param name="lane">The lane we are drawing</param>
        /// <param name="selectedNode">The node which is being edited</param>
        private static void RenderOutgoingLaneAsCircle(RenderManager.CameraInfo cameraInfo,
                                                       bool turnEnabled,
                                                       Color color,
                                                       NetLane lane,
                                                       NetNode selectedNode) {
            if (turnEnabled) {
                // blinking bright green/translucent green
                var t = (long)Math.Truncate(Time.time * 2f); // integer part, 2x faster blink
                color = t % 2 == 0 ? PALETTE_TURN_ALLOWED1 : PALETTE_TURN_ALLOWED2;
            } else {
                color = PALETTE_TURN_INACTIVE; // black
            }

            // Pick end of the lane, closest to the node being edited
            var aPosition = lane.CalculatePosition(0f);
            var bPosition = lane.CalculatePosition(1f);
            var aDistSqr = (aPosition - selectedNode.m_position).sqrMagnitude;
            var bDistSqr = (bPosition - selectedNode.m_position).sqrMagnitude;
            var nearestPos = aDistSqr < bDistSqr ? aPosition : bPosition;

            Singleton<ToolManager>.instance.m_drawCallData.m_overlayCalls += 1;
            Singleton<RenderManager>.instance.OverlayEffect.DrawCircle(
                cameraInfo, color, nearestPos, 2f,
                -1f, 1280f, false, false);
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
            ++Singleton<ToolManager>.instance.m_drawCallData.m_overlayCalls;

            Singleton<RenderManager>.instance.OverlayEffect.DrawBezier(
                cameraInfo, color, lane.m_bezier, width, -100000f, -100000f,
                -1f, 1280f, false, false);
        }
    }
}