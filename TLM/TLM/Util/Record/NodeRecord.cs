namespace TrafficManager.Util.Record {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.Remoting.Messaging;
    using TrafficManager.Manager.Impl;
    using UnityEngine;
    using static TrafficManager.Util.Shortcuts;

    [Serializable]
    public class NodeRecord : IRecordable {
        public NodeRecord(ushort nodeId) => NodeId = nodeId;

        public ushort NodeId { get; private set; }
        InstanceID InstanceID => new InstanceID { NetNode = NodeId};

        private bool? trafficLight_;
        private List<LaneConnectionRecord> lanes_;
        private static TrafficLightManager tlMan => TrafficLightManager.Instance;

        public void Record() {
            trafficLight_ = tlMan.GetTrafficLight(NodeId);
            lanes_ = LaneConnectionRecord.GetLanes(NodeId);
            foreach (LaneConnectionRecord sourceLane in lanes_) {
                sourceLane.Record();
            }
        }

        public bool IsDefault() {
            if (trafficLight_ != null)
                return false;
            foreach (LaneConnectionRecord sourceLane in lanes_) {
                if (!sourceLane.IsDefault())
                    return false;
            }
            return true;
        }

        public void Restore() {
            SetTrafficLight(NodeId, trafficLight_.Value);
            foreach (LaneConnectionRecord sourceLane in lanes_) {
                sourceLane.Restore();
            }
        }

        public void Transfer(Dictionary<InstanceID, InstanceID> map) {
            SetTrafficLight(map[InstanceID].NetNode, trafficLight_.Value);
            foreach (LaneConnectionRecord sourceLane in lanes_)
                sourceLane.Transfer(map);
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

        public byte[] Serialize() => RecordUtil.Serialize(this);
    }
}
