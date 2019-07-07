namespace TrafficManager.UI.SubTools.LaneArrows {
    using System.Collections.Generic;
    using ColossalFramework;
    using CSUtil.Commons;
    using GenericGameBridge.Service;
    using global::TrafficManager.Geometry.Impl;
    using Manager.Impl;
    using State;
    using UnityEngine;
    using Util;

    public partial class LaneArrowTool : SubTool {
        private const int MAX_NODE_SEGMENTS = 8;

        private enum State {
            NodeSelect,         // click a node to edit
            IncomingSelect,     // click an incoming lane to edit
            OutgoingDirections, // click approximate direction to allow turns
            OutgoingLanes,      // click each allowed lane to toggle
            Off
        }

        /// <summary>
        /// Events which trigger state transitions
        /// </summary>
        private enum Trigger {
            NodeClick,
            SegmentClick,
            LaneClick,
            RightMouseClick,
        }

        /// <summary>
        /// State machine for the tool, see state graph in
        /// https://github.com/krzychu124/Cities-Skylines-Traffic-Manager-President-Edition/pull/391#issuecomment-508410325
        /// </summary>
        private GenericFsm<State, Trigger> fsm_;

        /// <summary>
        /// For selected node, stores lanes incoming to that node
        /// </summary>
        private HashSet<uint> incomingLanes_;

        /// <summary>
        /// Allowed outgoing turns grouped by the direction
        /// </summary>
        private OutgoingTurnsCollection? outgoingTurns_;

        public LaneArrowTool(TrafficManagerTool mainTool)
            : base(mainTool) {
            fsm_ = InitFsm();
        }

        /// <summary>
        /// Creates FSM ready to begin editing. Or recreates it when ESC is pressed
        /// and the tool is canceled.
        /// </summary>
        /// <returns>The new FSM in the initial state.</returns>
        private GenericFsm<State, Trigger> InitFsm() {
            var fsm = new GenericFsm<State, Trigger>(State.NodeSelect);

            // From Node Select mode, user can either click a node, or right click
            // to quit the tool
            fsm.Configure(State.NodeSelect)
                .OnEntry(() => { SelectedNodeId = 0; })
                .Permit(Trigger.NodeClick, State.IncomingSelect)
                .Permit(Trigger.RightMouseClick, State.Off);

            // From Incoming Select the user can click another node, or click a lane
            // or a segment, or right click to leave back to node select
            fsm.Configure(State.IncomingSelect)
                .OnEntry(OnEnterState_IncomingSelect)
                .Permit(Trigger.NodeClick, State.IncomingSelect)
                .Permit(Trigger.LaneClick, State.OutgoingDirections)
                .Permit(Trigger.RightMouseClick, State.NodeSelect);

            // In Outgoing Select the user can click an outgoing lane or an outgoing
            // segment to apply routing, or right click to return to Incoming Select,
            // or click another node
            fsm.Configure(State.OutgoingDirections)
               .OnEntry(OnEnterState_OutgoingDirections)
                .Permit(Trigger.RightMouseClick, State.IncomingSelect)
                .Permit(Trigger.NodeClick, State.IncomingSelect);

            return fsm;
        }

        /// <summary>
        /// This is linked in the ctor to be called when FSM enters Incoming Select
        /// state, and sets up the on-screen display to see the incoming lanes
        /// to the clicked node.
        /// </summary>
        private void OnEnterState_IncomingSelect() {
            SelectedLaneId = 0;
            incomingLanes_ = GetAllIncomingLanes(SelectedNodeId);
        }

        /// <summary>
        /// This is linked in the ctor to be called when FSM enters Outgoing Select
        /// state, and sets up possible outgoing lanes, and also extracts current state
        /// of the allowed outgoing lanes.
        /// </summary>
        private void OnEnterState_OutgoingDirections() {
            outgoingTurns_ = GetOutgoingTurns(SelectedNodeId, SelectedSegmentId);

            // Some sanity check?
            if (!Flags.applyLaneArrowFlags(SelectedLaneId)) {
                Flags.removeLaneArrowFlags(SelectedLaneId);
            }

//            var segmentBuffer = Singleton<NetManager>.instance.m_segments.m_buffer;
//            var segment = segmentBuffer[SelectedSegmentId];
//            var otherNodeId = SelectedNodeId == segment.m_startNode
//                                  ? segment.m_endNode : segment.m_startNode;
//
//            outgoingLanes_ = new HashSet<uint>();
//            foreach (var ln in GetIncomingLaneList(SelectedSegmentId, otherNodeId)) {
//                outgoingLanes_.Add(ln.laneId);
//            }
        }

        /// <summary>
        /// Return true if cursor is in TM:PE menu or over any world-space UI control
        /// </summary>
        /// <returns>Cursor is over some UI</returns>
        public override bool IsCursorInPanel() {
            // True if cursor is inside TM:PE GUI or in any of the world space
            // GUIs that we have
            return base.IsCursorInPanel() || IsCursorInAnyLaneEditor();
        }

        /// <summary>
        /// Return whether mouse is over some part of the tool UI.
        /// </summary>
        /// <returns>Cursor is in some tool UI</returns>
        private bool IsCursorInAnyLaneEditor() {
            return false;
        }

        private static bool IsNodeEditable(ushort nodeId) {
            // TODO: Other node types? Basically check if the node has some incoming and some outgoing lanes
            var nodeBuffer = Singleton<NetManager>.instance.m_nodes.m_buffer;
            var netFlags = nodeBuffer[nodeId].m_flags;

            const NetNode.Flags MASK = NetNode.Flags.Junction;
//                                       | NetNode.Flags.AsymBackward
//                                       | NetNode.Flags.AsymForward
//                                       | NetNode.Flags.Transition
                                       // Also allow middle and bend, to control the road flow
//                                       | NetNode.Flags.Bend | NetNode.Flags.Middle;
            if ((netFlags & MASK) == NetNode.Flags.None) {
                return false;
            }

            ItemClass connectionClass = nodeBuffer[nodeId].Info.GetConnectionClass();
            return connectionClass != null && connectionClass.m_service == ItemClass.Service.Road;
        }

        public override void OnToolGUI(Event e) {
            switch (fsm_.State) {
                case State.NodeSelect:
                    OnToolGUI_NodeSelect();
                    break;
                case State.IncomingSelect:
                    OnToolGUI_IncomingSelect();
                    break;
                case State.OutgoingDirections:
                    OnToolGUI_OutgoingDirections();
                    break;
                case State.Off:
                    // Do nothing
                    break;
            }
        }

        /// <summary>
        /// Handle events for NodeSelect state
        /// </summary>
        private void OnToolGUI_NodeSelect() {
            // No GUI interaction in this mode
        }

        /// <summary>
        /// Handle events for IncomingSelect state
        /// </summary>
        private void OnToolGUI_IncomingSelect() {
            // No GUI interaction in this mode
        }

        /// <summary>
        /// Handle events for OutgoingDirections state
        /// </summary>
        private void OnToolGUI_OutgoingDirections() {
            // No GUI interaction in this mode
        }

        /// <summary>
        /// User right-clicked or other reasons, the selection is cleared
        /// </summary>
        private void Deselect() {
            fsm_ = InitFsm();
            SelectedSegmentId = 0;
            SelectedNodeId = 0;
            SelectedLaneId = 0;
        }

        internal static IList<LanePos> GetIncomingLaneList(ushort segmentId, ushort nodeId) {
            var segmentsBuffer = Singleton<NetManager>.instance.m_segments.m_buffer;
            return Constants.ServiceFactory.NetService.GetSortedLanes(
                segmentId,
                ref segmentsBuffer[segmentId],
                segmentsBuffer[segmentId].m_startNode == nodeId,
                LaneArrowManager.LANE_TYPES,
                LaneArrowManager.VEHICLE_TYPES,
                true);
        }

        /// <summary>
        /// For incoming segment into a node, get allowed directions to leave the segment.
        /// This is used to disable some of the lane turn buttons.
        /// </summary>
        /// <param name="nodeId">The currently edited node</param>
        /// <param name="ignoreSegmentId">The currently edited segment to exclude, or 0</param>
        /// <returns>Dict where keys are allowed lane turns, and values are sets of segment ids</returns>
        private OutgoingTurnsCollection GetOutgoingTurns(ushort nodeId, ushort ignoreSegmentId) {
            var result = new OutgoingTurnsCollection(nodeId, ignoreSegmentId);

            var geometry = SegmentGeometry.Get(ignoreSegmentId);
            if (geometry == null) {
                Log.Error(
                    $"LaneArrowsTool: No geometry information available for segment {ignoreSegmentId}");
                return result;
            }

            var nodesBuffer = Singleton<NetManager>.instance.m_nodes.m_buffer;
            var node = nodesBuffer[nodeId];
            var incomingSegment = Singleton<NetManager>.instance.m_segments.m_buffer[ignoreSegmentId];
            var isStartNode = nodeId == incomingSegment.m_startNode;

            for (var i = 0; i < MAX_NODE_SEGMENTS; ++i) {
                var outgoingSegId = node.GetSegment(i);
                if (outgoingSegId == 0) {
                    continue;
                }

                if (outgoingSegId == ignoreSegmentId) {
                    continue;
                }

                result.AddTurn(geometry.GetDirection(outgoingSegId, isStartNode), outgoingSegId);
            }

            return result;
        }

        /// <summary>
        /// Retrieve a unique set of all lanes that enter a given node
        /// </summary>
        /// <param name="nodeId">The node</param>
        /// <returns>The unique set of lane ids</returns>
        private HashSet<uint> GetAllIncomingLanes(ushort nodeId) {
            var result = new HashSet<uint>();

            var nodeBuffer = Singleton<NetManager>.instance.m_nodes.m_buffer;
            var node = nodeBuffer[nodeId];

            for (var i = 0; i < MAX_NODE_SEGMENTS; ++i) {
                var connectedSegId = node.GetSegment(i);
                if (connectedSegId == 0) {
                    continue;
                }

                foreach (var ln in GetIncomingLaneList(connectedSegId, nodeId)) {
                    result.Add(ln.laneId);
                }
            }

            return result;
        }


        /// <summary>
        /// Escape is pressed, or the tool was closed.
        /// </summary>
        public override void Cleanup() {
            Deselect();
        }
    } // class
} // namespace