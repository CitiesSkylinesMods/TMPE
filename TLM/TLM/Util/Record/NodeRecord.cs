namespace TrafficManager.Util.Record {
    using System.Collections.Generic;
    using TrafficManager.Manager.Impl;
    using static TrafficManager.Util.Shortcuts;

    class NodeRecord : IRecordable {
        public NodeRecord(ushort nodeId) => NodeId = nodeId;

        public ushort NodeId { get; private set; }
        
        private bool trafficLight_;
        private List<LaneConnectionRecord> lanes_;
        private static TrafficLightManager tlMan => TrafficLightManager.Instance;

        public void Record() {
            trafficLight_ = tlMan.HasTrafficLight(NodeId, ref NodeId.ToNode());
            lanes_ = LaneConnectionRecord.GetLanes(NodeId);
            foreach (LaneConnectionRecord sourceLane in lanes_) {
                sourceLane.Record();
            }
        }

        public void Restore() {
            SetTrafficLight(NodeId, trafficLight_);
            foreach (LaneConnectionRecord sourceLane in lanes_) {
                sourceLane.Restore();
            }
        }

        private static bool SetTrafficLight(ushort nodeId, bool flag) {
            // TODO move code to manager.
            bool currentValue = tlMan.HasTrafficLight(nodeId, ref nodeId.ToNode());
            if (currentValue == flag)
                return true;
            bool canChangeValue = tlMan.CanToggleTrafficLight(
                nodeId,
                flag,
                ref nodeId.ToNode(),
                out _);
            if (!canChangeValue) {
                return false;
            }
            return tlMan.SetTrafficLight(nodeId, flag, ref nodeId.ToNode());
        }
    }
}
