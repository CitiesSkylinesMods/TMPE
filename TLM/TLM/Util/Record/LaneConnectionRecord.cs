namespace TrafficManager.Util.Record {
    using CSUtil.Commons;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using TrafficManager.Manager.Impl;
    using TrafficManager.State;
    using TrafficManager.Util.Extensions;
    using static TrafficManager.Util.Shortcuts;

    [Serializable]
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
                if (currentConnections == null || !currentConnections.Contains(targetLaneId)) {
                    connMan.AddLaneConnection(LaneId, targetLaneId, StartNode);
                }
            }
            foreach (uint targetLaneId in currentConnections ?? Enumerable.Empty<uint>()) {
                if (!connections_.Contains(targetLaneId)) {
                    connMan.RemoveLaneConnection(LaneId, targetLaneId, StartNode);
                }
            }
        }

        public void Transfer(Dictionary<InstanceID, InstanceID> map) {
            uint MappedLaneId(uint originalLaneID) {
                var originalLaneInstanceID = new InstanceID { NetLane = originalLaneID };
                if (map.TryGetValue(originalLaneInstanceID, out var ret))
                    return ret.NetLane;
                Log._Debug($"Could not map lane:{originalLaneID}. this is expected if move it has not copied all segment[s] from an intersection");
                return 0;
            }
            var mappedLaneId = MappedLaneId(LaneId);

            if (connections_ == null) {
                connMan.RemoveLaneConnections(mappedLaneId, StartNode);
                return;
            }

            if (mappedLaneId == 0)
                return;

            //Log._Debug($"connections_=" + connections_.ToSTR());
            foreach (uint targetLaneId in connections_) {
                var mappedTargetLaneId = MappedLaneId(targetLaneId);
                if (mappedTargetLaneId == 0)
                    continue;
                //Log._Debug($"connecting lanes: {mappedLaneId}->{mappedTargetLaneId}");
                connMan.AddLaneConnection(mappedLaneId, mappedTargetLaneId, StartNode);
            }
        }

        public static List<LaneConnectionRecord> GetLanes(ushort nodeId) {
            var ret = new List<LaneConnectionRecord>();
            ref NetNode node = ref nodeId.ToNode();
            ExtSegmentManager extSegmentManager = ExtSegmentManager.Instance;
            for (int i = 0; i < 8; ++i) {
                ushort segmentId = node.GetSegment(i);
                if (segmentId == 0) {
                    continue;
                }
                ref NetSegment netSegment = ref segmentId.ToSegment();
                NetInfo netInfo = netSegment.Info;
                if (netInfo == null) {
                    continue;
                }

                foreach (LaneIdAndIndex laneIdAndIndex in extSegmentManager.GetSegmentLaneIdsAndLaneIndexes(segmentId)) {
                    NetInfo.Lane laneInfo = netInfo.m_lanes[laneIdAndIndex.laneIndex];
                    bool match = (laneInfo.m_laneType & LaneConnectionManager.LANE_TYPES) != 0 &&
                                 (laneInfo.m_vehicleType & LaneConnectionManager.VEHICLE_TYPES) != 0;
                    if (!match) {
                        continue;
                    }

                    var laneData = new LaneConnectionRecord {
                        LaneId = laneIdAndIndex.laneId,
                        LaneIndex = (byte)laneIdAndIndex.laneIndex,
                        StartNode = (bool)segmentId.ToSegment().IsStartNode(nodeId),
                    };
                    ret.Add(laneData);
                }
            }
            return ret;
        }

        public byte[] Serialize() => SerializationUtil.Serialize(this);

    }
}
