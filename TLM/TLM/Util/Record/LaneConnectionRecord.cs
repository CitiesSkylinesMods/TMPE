namespace TrafficManager.Util.Record {
    using CSUtil.Commons;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using TrafficManager.Manager.Impl;
    using TrafficManager.Manager.Impl.LaneConnection;
    using TrafficManager.State;
    using TrafficManager.Util.Extensions;
    using static TrafficManager.Util.Shortcuts;

    [Serializable]
    public class LaneConnectionRecord : IRecordable {
        public uint LaneId;
        public byte LaneIndex;
        public bool StartNode;

        private uint[] connections_; // legacy
        private uint[] roadConnections_;
        private uint[] trackConnections_;

        private static LaneConnectionManager connMan => LaneConnectionManager.Instance;

        public void Record() {
            connections_ = null;
            roadConnections_ = connMan.Road.GetLaneConnections(LaneId, StartNode)?.Clone() as uint[];
            trackConnections_ = connMan.Track.GetLaneConnections(LaneId, StartNode)?.Clone() as uint[];
        }

        private void RestoreImpl(LaneConnectionSubManager man, uint[] connections) {
            if (connections == null) {
                man.RemoveLaneConnections(LaneId, StartNode);
                return;
            }
            var currentConnections = man.GetLaneConnections(LaneId, StartNode) ?? new uint[0];
            //Log._Debug($"currentConnections=" + currentConnections.ToSTR());
            //Log._Debug($"connections=" + connections.ToSTR());

            foreach (uint targetLaneId in connections) {
                if (currentConnections == null || !currentConnections.Contains(targetLaneId)) {
                    man.AddLaneConnection(LaneId, targetLaneId, StartNode);
                }
            }
            foreach (uint targetLaneId in currentConnections) {
                if (!connections.Contains(targetLaneId)) {
                    man.RemoveLaneConnection(LaneId, targetLaneId, StartNode);
                }
            }
        }

        public bool IsDefault() =>
            connections_.IsNullOrEmpty() &&
            roadConnections_.IsNullOrEmpty() &&
            trackConnections_.IsNullOrEmpty();

        public void Restore() {
            if (connections_ != null) {
                // legacy
                RestoreImpl(connMan.Road, connections_);
                RestoreImpl(connMan.Track, connections_);
            } else {
                RestoreImpl(connMan.Road, roadConnections_);
                RestoreImpl(connMan.Track, trackConnections_);
            }
        }

        private void TransferImpl(
            Dictionary<InstanceID, InstanceID> map,
            LaneConnectionSubManager man,
            uint[] connections) {
            uint MappedLaneId(uint originalLaneID) {
                var originalLaneInstanceID = new InstanceID { NetLane = originalLaneID };
                if (map.TryGetValue(originalLaneInstanceID, out var ret))
                    return ret.NetLane;
                Log._Debug($"Could not map lane:{originalLaneID}. this is expected if move it has not copied all segment[s] from an intersection");
                return 0;
            }
            var mappedLaneId = MappedLaneId(LaneId);

            if (connections == null) {
                man.RemoveLaneConnections(mappedLaneId, StartNode);
                return;
            }

            if (mappedLaneId == 0)
                return;

            //Log._Debug($"connections=" + connections.ToSTR());
            foreach (uint targetLaneId in connections) {
                var mappedTargetLaneId = MappedLaneId(targetLaneId);
                if (mappedTargetLaneId == 0)
                    continue;
                //Log._Debug($"connecting lanes: {mappedLaneId}->{mappedTargetLaneId}");
                man.AddLaneConnection(mappedLaneId, mappedTargetLaneId, StartNode);
            }
        }

        public void Transfer(Dictionary<InstanceID, InstanceID> map) {
            if (connections_ != null) {
                // legacy
                TransferImpl(map, connMan.Road, connections_);
                TransferImpl(map, connMan.Track, connections_);
            } else {
                TransferImpl(map, connMan.Road, roadConnections_);
                TransferImpl(map, connMan.Track, trackConnections_);
            }
        }

        public static List<LaneConnectionRecord> GetLanes(ushort nodeId) {
            var ret = new List<LaneConnectionRecord>();
            ref NetNode node = ref nodeId.ToNode();
            ExtSegmentManager extSegmentManager = ExtSegmentManager.Instance;
            for (int segmentIndex = 0; segmentIndex < Constants.MAX_SEGMENTS_OF_NODE; ++segmentIndex) {
                ushort segmentId = node.GetSegment(segmentIndex);
                if (segmentId == 0) {
                    continue;
                }
                ref NetSegment netSegment = ref segmentId.ToSegment();
                NetInfo netInfo = netSegment.Info;
                if (netInfo == null) {
                    continue;
                }

                foreach (LaneIdAndIndex laneIdAndIndex in netSegment.GetSegmentLaneIdsAndLaneIndexes()) {
                    NetInfo.Lane laneInfo = netInfo.m_lanes[laneIdAndIndex.laneIndex];
                    bool match = (laneInfo.m_laneType & LaneConnectionManager.LANE_TYPES) != 0 &&
                                 (laneInfo.m_vehicleType & LaneConnectionManager.VEHICLE_TYPES) != 0;
                    if (!match) {
                        continue;
                    }

                    var laneData = new LaneConnectionRecord {
                        LaneId = laneIdAndIndex.laneId,
                        LaneIndex = (byte)laneIdAndIndex.laneIndex,
                        StartNode = netSegment.IsStartNode(nodeId),
                    };
                    ret.Add(laneData);
                }
            }
            return ret;
        }

        public byte[] Serialize() => SerializationUtil.Serialize(this);

    }
}
