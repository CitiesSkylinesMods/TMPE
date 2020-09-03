namespace TrafficManager.Util.Record {
    using System;
    using System.Collections.Generic;
    using TrafficManager.Manager.Impl;
    using static TrafficManager.Util.Shortcuts;

    [Serializable]
    public class SpeedLimitLaneRecord : IRecordable {
        public const NetInfo.LaneType LANE_TYPES =
            LaneArrowManager.LANE_TYPES | SpeedLimitManager.LANE_TYPES;
        public const VehicleInfo.VehicleType VEHICLE_TYPES =
            LaneArrowManager.VEHICLE_TYPES | SpeedLimitManager.VEHICLE_TYPES;

        public byte LaneIndex;
        public uint LaneId;

        private GetSpeedLimitResult speedLimit_; // game units, unlimited or not set

        InstanceID InstanceID => new InstanceID { NetLane = LaneId };

        public void Record() {
            speedLimit_ = SpeedLimitManager.Instance.GetCustomSpeedLimit(LaneId);
        }

        public void Restore() => Transfer(LaneId);

        public void Transfer(Dictionary<InstanceID, InstanceID> map) =>
            Transfer(map[this.InstanceID].NetLane);

        public void Transfer(uint laneId) {
            ushort segmentId = laneId.ToLane().m_segment;
            var laneInfo = GetLaneInfo(segmentId, LaneIndex);
            // SpeedLimitManager.Instance.SetSpeedLimit(
            //     segmentId: segmentId,
            //     laneIndex: LaneIndex,
            //     laneInfo: laneInfo,
            //     laneId: laneId,
            //     speedLimit: speedLimit_);
            SpeedLimitManager.Instance.SetLaneSpeedLimit(
                segmentId: segmentId,
                LaneIndex,
                laneInfo: laneInfo,
                LaneId,
                SetSpeedLimitAction.FromGetResult(speedLimit_));
        }

        public static List<SpeedLimitLaneRecord> GetLanes(ushort segmentId) {
            int maxLaneCount = segmentId.ToSegment().Info.m_lanes.Length;
            var ret = new List<SpeedLimitLaneRecord>(maxLaneCount);
            var lanes = netService.GetSortedLanes(
                segmentId,
                ref segmentId.ToSegment(),
                null,
                LANE_TYPES,
                VEHICLE_TYPES,
                sort: false);
            foreach (var lane in lanes) {
                SpeedLimitLaneRecord laneData = new SpeedLimitLaneRecord {
                    LaneId = lane.laneId,
                    LaneIndex = lane.laneIndex,
                };
                ret.Add(laneData);
            }
            ret.TrimExcess();
            return ret;
        }

        public byte[] Serialize() => RecordUtil.Serialize(this);

    }
}
