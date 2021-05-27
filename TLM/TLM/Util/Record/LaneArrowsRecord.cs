namespace TrafficManager.Util.Record {
    using ColossalFramework;
    using System;
    using System.Collections.Generic;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.Manager.Impl;
    using TrafficManager.State;
    using static TrafficManager.Util.Shortcuts;

    [Serializable]
    public class LaneArrowsRecord : IRecordable {
        public uint LaneId;
        InstanceID InstanceID => new InstanceID { NetLane = LaneId };

        private LaneArrows? arrows_;

        public void Record() {
            arrows_ = Flags.GetLaneArrowFlags(LaneId);
        }

        public void Restore() => Transfer(LaneId);

        public void Transfer(Dictionary<InstanceID, InstanceID> map) =>
            Transfer(map[InstanceID].NetLane);

        public void Transfer(uint laneId) {
            //Log._Debug($"Restore: SetLaneArrows({LaneId}, {arrows_})");
            if (arrows_ == null)
                return;
            LaneArrowManager.Instance.SetLaneArrows(laneId, arrows_.Value);
        }

        public static List<LaneArrowsRecord> GetLanes(ushort segmentId, bool startNode) {
            int maxLaneCount = segmentId.ToSegment().Info.m_lanes.Length;
            var ret = new List<LaneArrowsRecord>(maxLaneCount); 
            var lanes = netService.GetSortedLanes(
                segmentId,
                ref segmentId.ToSegment(),
                startNode,
                LaneArrowManager.LANE_TYPES,
                LaneArrowManager.VEHICLE_TYPES,
                sort: false);
            foreach (var lane in lanes) {
                LaneArrowsRecord laneData = new LaneArrowsRecord {
                    LaneId = lane.laneId,
                };
                ret.Add(laneData);
            }
            ret.TrimExcess();
            return ret;
        }

        public byte[] Serialize() => SerializationUtil.Serialize(this);
    }
}
