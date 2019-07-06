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
        private readonly Color PALETTE_HOVERED = Color.white;

        /// <summary>
        /// Blue solid. Currently (active) selected node.
        /// </summary>
        private readonly Color PALETTE_SELECTED = new Color(0f, 0f, 1f, 1f);

        /// <summary>
        /// White translucent. Outline for current segment, when we are operating lanes.
        /// </summary>
        private readonly Color PALETTE_CURRENT_SEGMENT_GHOST = new Color(1f, 1f, 1f, 0.33f);

        /// <summary>
        /// Green, for allowed turns.
        /// </summary>
        private readonly Color PALETTE_TURN_ALLOWED = new Color(0f, 0.75f, 0f, 0.66f);

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

            // Draw outgoing directions for the last selected segment
            var selectedLane = laneBuffer[SelectedLaneId];
            var selectedLaneFlags = (NetLane.Flags)selectedLane.m_flags;

            // Turns out of this node converted to lanes (we want all turn bits here)
            var outgoingLaneTurns = outgoingTurns_.GetLanesFor(NetLane.Flags.LeftForwardRight);

            foreach (var outDirections in outgoingTurns_.AllTurns) {
                switch (outDirections.Key) {
                    case ArrowDirection.Left:
                        RenderOutgoingDirection(cameraInfo,
                                                (selectedLaneFlags & NetLane.Flags.Left) != 0,
                                                outgoingLaneTurns[outDirections.Key]);
                        break;
                    case ArrowDirection.Forward:
                        RenderOutgoingDirection(cameraInfo,
                                                (selectedLaneFlags & NetLane.Flags.Forward) != 0,
                                                outgoingLaneTurns[outDirections.Key]);
                        break;
                    case ArrowDirection.Right:
                        RenderOutgoingDirection(cameraInfo,
                                                (selectedLaneFlags & NetLane.Flags.Right) != 0,
                                                outgoingLaneTurns[outDirections.Key]);
                        break;
                    case ArrowDirection.Turn:
                    case ArrowDirection.None:
                        break;
                }
            }
        }

        private void RenderOutgoingDirection(RenderManager.CameraInfo cameraInfo,
                                             bool turnEnabled,
                                             HashSet<uint> laneTurns) {
            var t = Time.time - (float) Math.Truncate(Time.time); // fraction
            var pulsatingColor = Color.Lerp(PALETTE_SELECTED, Color.black, t);
            var laneBuffer = Singleton<NetManager>.instance.m_lanes.m_buffer;

            foreach (var laneId in laneTurns) {
                var color = turnEnabled ? PALETTE_TURN_ALLOWED : pulsatingColor;
                RenderLaneOverlay(cameraInfo, laneBuffer[laneId], 2f, color);
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
                                              //ref NetSegment segment,
                                              NetLane lane,
                                              float width,
                                              Color color) {
//            var info = segment.Info;
//            if (info == null ||
//                ((segment.m_flags & NetSegment.Flags.Untouchable) != NetSegment.Flags.None
//                 && !info.m_overlayVisible)) {
//                return;
//            }

            ++Singleton<ToolManager>.instance.m_drawCallData.m_overlayCalls;

            Singleton<RenderManager>.instance.OverlayEffect.DrawBezier(
                cameraInfo, color, lane.m_bezier, width, -100000f, -100000f,
                -1f, 1280f, false, false);
        }
    }
}