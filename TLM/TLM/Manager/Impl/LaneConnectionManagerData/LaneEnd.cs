namespace TrafficManager.Manager.Impl.LaneConnectionManagerData {
    using TrafficManager.Util.Extensions;

    public struct LaneEnd {
        internal uint LaneId;
        internal bool StartNode;

        public LaneEnd(uint laneId, bool startNode) {
            LaneId = laneId;
            StartNode = startNode;
        }
        public LaneEnd(uint laneId, ushort nodeId) {
            LaneId = laneId;
            StartNode = laneId.ToLane().IsStartNode(nodeId);
        }

        public override int GetHashCode() {
            if (StartNode)
                return (int)(LaneId * 2);
            else
                return (int)(LaneId * 2 + 1);
        }
    }
}
