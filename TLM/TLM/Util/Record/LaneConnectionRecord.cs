namespace TrafficManager.Util.Record {
    using ColossalFramework;
    using System.Collections.Generic;
    using System.Linq;
    using TrafficManager.Manager.Impl;
    using static TrafficManager.Util.Shortcuts;

    public class LaneConnectionRecord : IRecordable {
        public uint LaneId;
        public byte LaneIndex;
        public bool StartNode;

        private uint[] Connections;

        private static LaneConnectionManager connMan => LaneConnectionManager.Instance;

        private uint[] GetCurrentConnections() => connMan.GetLaneConnections(LaneId, StartNode);

        public void Record() {
            Connections = (uint[])GetCurrentConnections().Clone();
        }

        public void Restore() {
            var currentConnections = GetCurrentConnections();
            foreach (uint targetLaneId in Connections) {
                if (currentConnections.Contains(targetLaneId)) {
                    connMan.AddLaneConnection(LaneId, targetLaneId, StartNode);
                }
            }
            foreach (uint targetLaneId in currentConnections) {
                if (Connections.Contains(targetLaneId)) {
                    connMan.RemoveLaneConnection(LaneId, targetLaneId, StartNode);
                }
            }
        }

        public static List<LaneConnectionRecord> GetLanes(ushort nodeId) {
            var ret = new List<LaneConnectionRecord>();
            ref NetNode node = ref nodeId.ToNode();
            for (int i = 0; i < 8; ++i) {
                ushort segmentId = node.GetSegment(i);
                if (segmentId == 0) continue;
                ref NetSegment segment = ref segmentId.ToSegment();
                uint laneId = segment.m_lanes;
                NetInfo.Lane[] lanes = segment.Info.m_lanes;

                for (byte laneIndex = 0; (laneIndex < lanes.Length) && (laneId != 0); laneIndex++) {
                    NetInfo.Lane laneInfo = lanes[laneIndex];
                    if (!laneInfo.m_laneType.IsFlagSet(LaneConnectionManager.LANE_TYPES) ||
                        !laneInfo.m_vehicleType.IsFlagSet(LaneConnectionManager.VEHICLE_TYPES)) {
                        continue;
                    }
                    var laneData = new LaneConnectionRecord {
                        LaneId = laneId,
                        LaneIndex = laneIndex,
                        StartNode = (bool)netService.IsStartNode(segmentId, nodeId),
                    };
                    ret.Add(laneData);
                    laneId = laneId.ToLane().m_nextLane;
                }
            }
            return ret;
        }
    }
}
