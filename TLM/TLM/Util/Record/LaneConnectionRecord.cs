namespace TrafficManager.Util.Record {
    using ColossalFramework;
    using CSUtil.Commons;
    using GenericGameBridge.Service;
    using System.Collections.Generic;
    using System.Linq;
    using TrafficManager.Manager.Impl;
    using static TrafficManager.Util.Shortcuts;

    public class LaneConnectionRecord : IRecordable {
        public uint LaneId;
        public byte LaneIndex;
        public bool StartNode;

        private uint[] connections_;

        private static LaneConnectionManager connMan => LaneConnectionManager.Instance;

        private uint[] GetCurrentConnections() => connMan.GetLaneConnections(LaneId, StartNode);

        public void Record() {
            connections_ = GetCurrentConnections();
            //Log._Debug($"LaneConnectionRecord.Record: connections_=" + connections_.ToSTR());

            if (connections_ != null)
                connections_ = (uint[])connections_.Clone();
        }

        public void Restore() {
            if (connections_ == null) {
                connMan.RemoveLaneConnections(LaneId, StartNode);
                return;
            }
            var currentConnections = GetCurrentConnections();
            //Log._Debug($"currentConnections=" + currentConnections.ToSTR());
            //Log._Debug($"connections_=" + connections_.ToSTR());

            foreach (uint targetLaneId in connections_) {
                if (currentConnections ==  null || !currentConnections.Contains(targetLaneId)) {
                    connMan.AddLaneConnection(LaneId, targetLaneId, StartNode);
                }
            }
            foreach (uint targetLaneId in currentConnections ?? Enumerable.Empty<uint>()) {
                if (!connections_.Contains(targetLaneId)) {
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
                bool Handler(
                    uint laneId,
                    ref NetLane lane,
                    NetInfo.Lane laneInfo,
                    ushort segmentId,
                    ref NetSegment segment,
                    byte laneIndex) {
                    bool match = (laneInfo.m_laneType & LaneConnectionManager.LANE_TYPES) != 0 &&
                                  (laneInfo.m_vehicleType & LaneConnectionManager.VEHICLE_TYPES) != 0;
                    if (!match)
                        return true;
                    var laneData = new LaneConnectionRecord {
                        LaneId = laneId,
                        LaneIndex = laneIndex,
                        StartNode = (bool)netService.IsStartNode(segmentId, nodeId),
                    };
                    ret.Add(laneData);
                    return true;
                }
                netService.IterateSegmentLanes(
                    segmentId,
                    Handler);
            }
            return ret;
        }
    }
}
