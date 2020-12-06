namespace TrafficManager.Util.Record {
    using System;
    using System.Collections.Generic;
    using GenericGameBridge.Service;
    using TrafficManager.API.Traffic.Data;
    using TrafficManager.Manager.Impl;
    using static TrafficManager.Util.Shortcuts;
    using TrafficManager.State;

    [Serializable]
    public class SpeedLimitLaneRecord : IRecordable {
        public const NetInfo.LaneType LANE_TYPES =
            LaneArrowManager.LANE_TYPES | SpeedLimitManager.LANE_TYPES;

        public const VehicleInfo.VehicleType VEHICLE_TYPES =
            LaneArrowManager.VEHICLE_TYPES | SpeedLimitManager.VEHICLE_TYPES;

        public byte LaneIndex;
        public uint LaneId;

        private float? speedLimit_; // game units

        InstanceID InstanceID => new() {NetLane = LaneId};

        public void Record() {
            GetSpeedLimitResult gsl = SpeedLimitManager.Instance.GetCustomSpeedLimit(LaneId);
            this.speedLimit_ = gsl.OverrideValue.HasValue && gsl.OverrideValue.Value.GameUnits > 0f
                                   ? gsl.OverrideValue.Value.GameUnits
                                   : (float?)null;
        }

        public void Restore() => Transfer(LaneId);

        public void Transfer(Dictionary<InstanceID, InstanceID> map) =>
            Transfer(map[this.InstanceID].NetLane);

        public void Transfer(uint laneId) {
            ushort segmentId = laneId.ToLane().m_segment;
            NetInfo.Lane laneInfo = GetLaneInfo(segmentId, LaneIndex);
            SpeedLimitManager.Instance.SetLaneSpeedLimit(
                segmentId: segmentId,
                laneIndex: LaneIndex,
                laneInfo: laneInfo,
                laneId: LaneId,
                action: SetSpeedLimitAction.FromNullableFloat(speedLimit_));
        }

        public static List<SpeedLimitLaneRecord> GetLanes(ushort segmentId) {
            int maxLaneCount = segmentId.ToSegment().Info.m_lanes.Length;
            var ret = new List<SpeedLimitLaneRecord>(maxLaneCount);
            IList<LanePos> lanes = netService.GetSortedLanes(
                segmentId: segmentId,
                segment: ref segmentId.ToSegment(),
                startNode: null,
                laneTypeFilter: LANE_TYPES,
                vehicleTypeFilter: VEHICLE_TYPES,
                sort: false);
            foreach (LanePos lane in lanes) {
                var laneData = new SpeedLimitLaneRecord {
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