namespace TrafficManager.Util.Record {
    using ColossalFramework;
    using CSUtil.Commons;
    using System.Collections.Generic;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.Manager.Impl;
    using static TrafficManager.Util.Shortcuts;

    public class SpeedLimitLaneRecord : IRecordable {
        public const NetInfo.LaneType LANE_TYPES =
            LaneArrowManager.LANE_TYPES | SpeedLimitManager.LANE_TYPES;
        public const VehicleInfo.VehicleType VEHICLE_TYPES =
            LaneArrowManager.VEHICLE_TYPES | SpeedLimitManager.VEHICLE_TYPES;

        public byte LaneIndex;
        public uint LaneId;
        NetInfo.Lane LaneInfo;
        ushort SegmentId;

        private float? speedLimit_; // game units

        public void Record() {
            speedLimit_ = SpeedLimitManager.Instance.GetCustomSpeedLimit(LaneId);
            if (speedLimit_ == 0)
                speedLimit_ = null;
        }

        public void Restore() {
            SpeedLimitManager.Instance.SetSpeedLimit(
                SegmentId,
                LaneIndex,
                LaneInfo,
                LaneId,
                speedLimit_);
        }

        public static List<SpeedLimitLaneRecord> GetLanes(ushort segmentId) {
            var ret = new List<SpeedLimitLaneRecord>();
            var lanes = netService.GetSortedLanes(
                segmentId,
                ref segmentId.ToSegment(),
                null,
                LANE_TYPES,
                VEHICLE_TYPES,
                sort: false);
            foreach(var lane in lanes) {
                SpeedLimitLaneRecord laneData = new SpeedLimitLaneRecord {
                    LaneId = lane.laneId,
                    LaneIndex = lane.laneIndex,
                    SegmentId = segmentId,
                    LaneInfo = segmentId.ToSegment().Info.m_lanes[lane.laneIndex],
                };
                ret.Add(laneData);
            }
            return ret;
        }
    }
}
