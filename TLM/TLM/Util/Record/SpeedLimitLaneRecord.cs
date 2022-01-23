namespace TrafficManager.Util.Record {
    using System;
    using System.Collections.Generic;
    using TrafficManager.Manager.Impl;
    using static TrafficManager.Util.Shortcuts;
    using TrafficManager.State;
    using TrafficManager.Util.Extensions;

    [Serializable]
    public class SpeedLimitLaneRecord : IRecordable {
        public const NetInfo.LaneType LANE_TYPES =
            LaneArrowManager.LANE_TYPES | SpeedLimitManager.LANE_TYPES;
        public const VehicleInfo.VehicleType VEHICLE_TYPES =
            LaneArrowManager.VEHICLE_TYPES | SpeedLimitManager.VEHICLE_TYPES;

        public byte LaneIndex;
        public uint LaneId;

        private float? speedLimit_; // game units

        InstanceID InstanceID => new InstanceID { NetLane = LaneId };

        public void Record() {
            GetSpeedLimitResult gsl = SpeedLimitManager.Instance.CalculateCustomSpeedLimit(this.LaneId);
            this.speedLimit_ = gsl.OverrideValue.HasValue && gsl.OverrideValue.Value.GameUnits > 0f
                                   ? gsl.OverrideValue.Value.GameUnits
                                   : (float?)null;
        }

        public void Restore() => Transfer(LaneId);

        public void Transfer(Dictionary<InstanceID, InstanceID> map) =>
            Transfer(map[this.InstanceID].NetLane);

        public void Transfer(uint laneId) {
            ushort segmentId = laneId.ToLane().m_segment;
            var laneInfo = segmentId.ToSegment().GetLaneInfo(LaneIndex);
            SpeedLimitManager.Instance.SetLaneSpeedLimit(
                segmentId: segmentId,
                laneIndex: this.LaneIndex,
                laneInfo: laneInfo,
                laneId: laneId,
                action: SetSpeedLimitAction.FromNullableFloat(this.speedLimit_));
        }

        public static List<SpeedLimitLaneRecord> GetLanes(ushort segmentId) {
            int maxLaneCount = segmentId.ToSegment().Info.m_lanes.Length;
            var ret = new List<SpeedLimitLaneRecord>(maxLaneCount);
            ExtSegmentManager extSegmentManager = ExtSegmentManager.Instance;
            var lanes = extSegmentManager.GetSortedLanes(
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

        public byte[] Serialize() => SerializationUtil.Serialize(this);

    }
}
