namespace TrafficManager.UI.SubTools.LaneArrows {
    using CSUtil.Commons;

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
            // Allow to click a node and start over
            if (SelectedNodeId != HoveredNodeId && IsNodeEditable(HoveredNodeId)) {
                // Try change state to IncomingSelect, or do nothing
                SelectedNodeId = HoveredNodeId;
                fsm_.SendTrigger(Trigger.NodeClick);

                // State did not change, just reenter with another nodeId
                return;
            }

//            if (HoveredLaneId == 0 || HoveredSegmentId == 0) {
//                return;
//            }


            // var hoveredSegment = Singleton<NetManager>.instance.m_segments.m_buffer[HoveredSegmentId];
            // if (hoveredSegment.m_startNode != HoveredNodeId &&
            //     hoveredSegment.m_endNode != HoveredNodeId) {
            //     return;
            // }
            //
            // SelectedSegmentId = HoveredSegmentId;
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