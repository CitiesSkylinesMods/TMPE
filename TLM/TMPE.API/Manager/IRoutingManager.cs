namespace TrafficManager.API.Manager {
    using System;
    using CSUtil.Commons;
    using TrafficManager.API.Traffic.Enums;

    public interface IRoutingManager {
        // TODO documentation

        /// <summary>
        /// Structs for path-finding that contain required segment-related routing data
        /// </summary>
        SegmentRoutingData[] SegmentRoutings { get; }

        /// <summary>
        /// Structs for path-finding that contain required lane-end-related backward routing data.
        /// Index:
        ///		[0 .. NetManager.MAX_LANE_COUNT-1]: lane ends at start node
        ///		[NetManager.MAX_LANE_COUNT .. 2*NetManger.MAX_LANE_COUNT-1]: lane ends at end node
        /// </summary>
        LaneEndRoutingData[] LaneEndBackwardRoutings { get; }

        /// <summary>
        /// Structs for path-finding that contain required lane-end-related forward routing data.
        /// Index:
        ///		[0 .. NetManager.MAX_LANE_COUNT-1]: lane ends at start node
        ///		[NetManager.MAX_LANE_COUNT .. 2*NetManger.MAX_LANE_COUNT-1]: lane ends at end node
        /// </summary>
        LaneEndRoutingData[] LaneEndForwardRoutings { get; }

        void SimulationStep();
        void RequestFullRecalculation();
        void RequestRecalculation(ushort segmentId, bool propagate = true);
        uint GetLaneEndRoutingIndex(uint laneId, bool startNode);
        int CalcInnerSimilarLaneIndex(ushort segmentId, int laneIndex);
        int CalcInnerSimilarLaneIndex(NetInfo.Lane laneInfo);
        int CalcOuterSimilarLaneIndex(ushort segmentId, int laneIndex);
        int CalcOuterSimilarLaneIndex(NetInfo.Lane laneInfo);
    }

    public struct SegmentRoutingData {
        public bool startNodeOutgoingOneWay;
        public bool endNodeOutgoingOneWay;
        public bool highway;

        public override string ToString() {
            return
                $"[SegmentRoutingData\n\tstartNodeOutgoingOneWay = {startNodeOutgoingOneWay}\n" +
                $"\tendNodeOutgoingOneWay = {endNodeOutgoingOneWay}\n\thighway = {highway}\nSegmentRoutingData]";
        }

        public void Reset() {
            startNodeOutgoingOneWay = false;
            endNodeOutgoingOneWay = false;
            highway = false;
        }
    }

    public struct LaneEndRoutingData {
        public bool routed;
        public LaneTransitionData[] transitions;

        public override string ToString() {
            return
                $"[LaneEndRoutingData\n\trouted = {routed}\n\ttransitions = " +
                $"{(transitions == null ? "<null>" : transitions.ArrayToString())}\nLaneEndRoutingData]";
        }

        public void Reset() {
            routed = false;
            transitions = null;
        }

        public void RemoveTransition(uint laneId) {
            if (transitions == null) {
                return;
            }

            int index = -1;
            for (var i = 0; i < transitions.Length; ++i) {
                if (transitions[i].laneId == laneId) {
                    index = i;
                    break;
                }
            }

            if (index < 0) {
                return;
            }

            if (transitions.Length == 1) {
                Reset();
                return;
            }

            LaneTransitionData[] newTransitions = new LaneTransitionData[transitions.Length - 1];
            if (index > 0) {
                Array.Copy(transitions, 0, newTransitions, 0, index);
            }

            if (index < transitions.Length - 1) {
                Array.Copy(
                    transitions,
                    index + 1,
                    newTransitions,
                    index,
                    transitions.Length - index - 1);
            }

            transitions = newTransitions;
        }

        public void AddTransitions(LaneTransitionData[] transitionsToAdd) {
            if (transitions == null) {
                transitions = transitionsToAdd;
                routed = true;
                return;
            }

            var newTransitions = new LaneTransitionData[transitions.Length + transitionsToAdd.Length];
            Array.Copy(transitions, newTransitions, transitions.Length);
            Array.Copy(
                transitionsToAdd,
                0,
                newTransitions,
                transitions.Length,
                transitionsToAdd.Length);
            transitions = newTransitions;

            routed = true;
        }

        public void AddTransition(LaneTransitionData transition) {
            AddTransitions(new LaneTransitionData[1] { transition });
        }
    }
    public struct LaneTransitionData {
        public uint laneId;
        public byte laneIndex;
        public LaneEndTransitionType type;
        public byte distance;
        public ushort segmentId;
        public bool startNode;
        public LaneEndTransitionGroup group;

        public override string ToString() {
            return string.Format(
                "[LaneTransitionData\n\tlaneId = {0}\n\tlaneIndex = {1}\n\tsegmentId = {2}\n" +
                "\tstartNode = {3}\n\ttype = {4}\n\tgroup = {5}\n\tdistance = {6}\nLaneTransitionData]",
                laneId,
                laneIndex,
                segmentId,
                startNode,
                type,
                group,
                distance);
        }

        public void Set(uint laneId,
                byte laneIndex,
                LaneEndTransitionType type,
                ushort segmentId,
                bool startNode,
                byte distance,
                LaneEndTransitionGroup group) {
            this.laneId = laneId;
            this.laneIndex = laneIndex;
            this.type = type;
            this.distance = distance;
            this.segmentId = segmentId;
            this.startNode = startNode;
            this.group = group;
        }
    }
}