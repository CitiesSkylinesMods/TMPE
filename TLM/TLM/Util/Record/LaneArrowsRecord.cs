namespace TrafficManager.Util.Record {
    using ColossalFramework;
    using CSUtil.Commons;
    using System.Collections.Generic;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.Manager.Impl;
    using static TrafficManager.Util.Shortcuts;

    public class LaneArrowsRecord : IRecordable {
        public uint LaneId;

        private LaneArrows arrows_;

        public void Record() {
            arrows_ = LaneArrowManager.Instance.GetFinalLaneArrows(LaneId);
        }

        public void Restore() {
            //Log._Debug($"Restore: SetLaneArrows({LaneId}, {arrows_})");
            LaneArrowManager.Instance.SetLaneArrows(LaneId, arrows_);
        }

        public static List<LaneArrowsRecord> GetLanes(ushort segmentId, bool startNode) {
            var ret = new List<LaneArrowsRecord>();
            var lanes = netService.GetSortedLanes(
                segmentId,
                ref segmentId.ToSegment(),
                startNode,
                LaneArrowManager.LANE_TYPES,
                LaneArrowManager.VEHICLE_TYPES,
                sort: false);
            foreach(var lane in lanes) {
                LaneArrowsRecord laneData = new LaneArrowsRecord {
                    LaneId = lane.laneId,
                    SegmentId = segmentId,
                };
                ret.Add(laneData);
            }
            return ret;
        }
    }
}
