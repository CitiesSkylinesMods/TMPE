namespace TrafficManager.Util.Record {
    using System.Collections.Generic;
    using TrafficManager.Manager.Impl;
    using static TrafficManager.Util.Shortcuts;

    class NodeRecord : IRecordable {
        public ushort NodeId { get; private set; }
        public NodeRecord(ushort nodeId) => NodeId = nodeId;

        private bool TrafficLight;
        private List<LaneConnectionRecord> Lanes;
        private static TrafficLightManager tlMan => TrafficLightManager.Instance;

        public void Record() {
            TrafficLight = tlMan.HasTrafficLight(NodeId, ref NodeId.ToNode());
            Lanes = LaneConnectionRecord.GetLanes(NodeId);
            foreach (LaneConnectionRecord sourceLane in Lanes) {
                sourceLane.Record();
            }
        }

        public void Restore() {
            SetTrafficLight(NodeId, TrafficLight);
            foreach (LaneConnectionRecord sourceLane in Lanes) {
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
