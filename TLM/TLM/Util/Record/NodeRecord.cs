namespace TrafficManager.Util.Record {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using TrafficManager.Manager.Impl;
    using TrafficManager.State;
    using TrafficManager.Util.Extensions;

    [Serializable]
    public class NodeRecord : IRecordable {
        public NodeRecord(ushort nodeId) => NodeId = nodeId;

        public ushort NodeId { get; private set; }
        InstanceID InstanceID => new InstanceID { NetNode = NodeId};

        private bool? trafficLight_;
        private List<LaneConnectionRecord> lanes_;
        private static TrafficLightManager tlMan => TrafficLightManager.Instance;

        public void Record() {
            trafficLight_ = tlMan.GetHasTrafficLight(NodeId);
            lanes_ = LaneConnectionRecord.GetLanes(NodeId);
            foreach (LaneConnectionRecord sourceLane in lanes_.EmptyIfNull()) {
                sourceLane?.Record();
            }
        }

        public bool IsDefault() =>
            trafficLight_ == null && lanes_.AreDefault();

        public void Restore() {
            tlMan.SetHasTrafficLight(NodeId, trafficLight_);
            foreach (LaneConnectionRecord sourceLane in lanes_.EmptyIfNull()) {
                sourceLane?.Restore();
            }
        }

        public void Transfer(Dictionary<InstanceID, InstanceID> map) {
            tlMan.SetHasTrafficLight(map[InstanceID].NetNode, trafficLight_);
            foreach (LaneConnectionRecord sourceLane in lanes_.EmptyIfNull())
                sourceLane?.Transfer(map);
        }

        public byte[] Serialize() => SerializationUtil.Serialize(this);
    }
}
