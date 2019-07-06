namespace TrafficManager.UI.SubTools.LaneArrows {
    using System;
    using ColossalFramework;
    using CSUtil.Commons;
    using Manager.Impl;
    using State;
    using UnityEngine;

    public partial class LaneArrowTool {
        /// <summary>
        /// Called from base class when mouse is clicked somewhere in the world.
        /// Left click on a node should enter the node edit mode.
        /// </summary>
        public override void OnPrimaryClickOverlay() {
            switch (fsm_.State) {
                case State.NodeSelect:
                    OnPrimaryClickOverlay_NodeSelect();
                    break;
                case State.IncomingSelect:
                    OnPrimaryClickOverlay_IncomingSelect();
                    break;
                case State.OutgoingDirections:
                    OnPrimaryClickOverlay_OutgoingDirections();
                    break;
                case State.Off:
                    // Do nothing
                    break;
            }
        }

        /// <summary>
        /// Left click on the world in State.NodeSelect
        /// </summary>
        private void OnPrimaryClickOverlay_NodeSelect() {
            if (HoveredNodeId == 0) {
                // We are only interested in clicking some nodes in this state
                return;
            }

            // Check if the node is an accepted type of node
            if (!IsNodeEditable(HoveredNodeId)) {
                return;
            }

            // Try change state to IncomingSelect, or do nothing
            SelectedNodeId = HoveredNodeId;
            if (!fsm_.SendTrigger(Trigger.NodeClick)) {
                Deselect();
            }

            // This will call OnEnterState_IncomingSelect()
            // NEW STATE -> SELECT INCOMING
        }

        private void OnPrimaryClickOverlay_IncomingSelect() {
            // Allow to click a node and start over
            if (SelectedNodeId != HoveredNodeId && IsNodeEditable(HoveredNodeId)) {
                // Try change state to IncomingSelect, or do nothing
                SelectedNodeId = HoveredNodeId;
                fsm_.SendTrigger(Trigger.NodeClick);

                // State did not change, just reenter with another nodeId
                return;
            }

            if (HoveredLaneId == 0 || !incomingLanes_.Contains(HoveredLaneId)) {
                return;
            }

            // This will change the state and call OnEnterState_OutgoingDirections()
            SelectedLaneId = HoveredLaneId;
            SelectedSegmentId = HoveredSegmentId;
            if (!fsm_.SendTrigger(Trigger.LaneClick)) {
                SelectedLaneId = 0;
                SelectedSegmentId = 0;
            }
            // NEW STATE -> SELECT OUTGOING
        }

        private void OnPrimaryClickOverlay_OutgoingDirections() {
            if (HoveredSegmentId != 0) {
                var hoveredDirection = outgoingTurns_.FindDirection(HoveredSegmentId);
                var segmentBuffer = Singleton<NetManager>.instance.m_segments.m_buffer;
                var hoveredSegment = segmentBuffer[HoveredSegmentId];
                var startNode = hoveredSegment.m_startNode == SelectedNodeId;

                switch (hoveredDirection) {
                    case ArrowDirection.Left:
                        ForwardSegmentsClicked(SelectedLaneId, startNode, Flags.LaneArrows.Left);
                        break;
                    case ArrowDirection.Forward:
                        ForwardSegmentsClicked(SelectedLaneId, startNode, Flags.LaneArrows.Forward);
                        break;
                    case ArrowDirection.Right:
                        ForwardSegmentsClicked(SelectedLaneId, startNode, Flags.LaneArrows.Right);
                        break;
                    case ArrowDirection.Turn:
                    case ArrowDirection.None:
                        break;
                }

                // End click here
                return;
            }

            // Allow to click a node and start over
            if (SelectedNodeId != HoveredNodeId && IsNodeEditable(HoveredNodeId)) {
                // Try changed back to IncomingSelect
                SelectedNodeId = HoveredNodeId;
                fsm_.SendTrigger(Trigger.NodeClick);

                // State change, just reenter with another nodeId
            }
        }

        private void ForwardSegmentsClicked(uint laneId, bool startNode, Flags.LaneArrows toggle) {
            LaneArrowManager.Instance.ToggleLaneArrows(laneId, startNode, toggle, out var res);

            if (res == Flags.LaneArrowChangeResult.Invalid ||
                res == Flags.LaneArrowChangeResult.Success) {
                // success
            }
        }


        /// <summary>
        /// Right click on the world should remove the selection
        /// </summary>
        public override void OnSecondaryClickOverlay() {
            fsm_.SendTrigger(Trigger.RightMouseClick);

            Log._Debug($"Rclick: hovn={HoveredNodeId} seln={SelectedNodeId}");

//            if (!IsCursorInPanel()) {
//                Deselect();
//            }
        }
    }
}