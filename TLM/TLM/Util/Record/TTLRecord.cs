namespace TrafficManager.Util.Record {
    using System;
    using System.Collections.Generic;
    using TrafficManager.Manager.Impl;
    using TrafficManager.TrafficLight.Impl;
    using static TrafficManager.Util.Shortcuts;

    [Serializable]
    public class TTLRecord : IRecordable {
        public TTLRecord(ushort nodeId) => NodeId = nodeId;

        public ushort NodeId { get; private set; }

        InstanceID InstanceID => new InstanceID { NetNode = NodeId };

        private static TrafficLightManager tlMan => TrafficLightManager.Instance;
        private static TrafficLightSimulationManager tlsMan => TrafficLightSimulationManager.Instance;

        public void Record() {
            TimedTrafficLights sourceTimedLights =
                tlsMan.TrafficLightSimulations[NodeId].timedLight as TimedTrafficLights;
        }

        public void Restore() {
            ushort nodeId = NodeId;
            // copy `nodeIdToCopy` to `HoveredNodeId`
            tlsMan.SetUpTimedTrafficLight(
                nodeId,
                new List<ushort> { nodeId });

            tlsMan.TrafficLightSimulations[nodeId].timedLight
                  .PasteSteps(sourceTimedLights);
        }

        public void Transfer(Dictionary<InstanceID, InstanceID> map) {
        }






        public byte[] Serialize() => RecordUtil.Serialize(this);
    }
}
