namespace TrafficManager.Util.Record {
    using System;
    using System.Collections.Generic;
    using TrafficManager.Manager.Impl;
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

        public bool IsDefault() => speedLimit_ == null;

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

        /// <summary>
        /// Obtain speed limit records for applicable lanes within the segment.
        /// </summary>
        /// <param name="segmentId">The id of the segment to inspect.</param>
        /// <returns>
        /// Returns a list of <see cref="SpeedLimitLaneRecord"/> for all
        /// speed-applicable lanes in the segment. The list may be empty if no
        /// matching lanes were found.
        /// </returns>
        public static List<SpeedLimitLaneRecord> GetLanes(ushort segmentId) {

            var lanes = segmentId.ToSegment().GetSortedLanes(
                null,
                LANE_TYPES,
                VEHICLE_TYPES,
                sort: false);

            var ret = new List<SpeedLimitLaneRecord>(lanes.Count);

            foreach (var lane in lanes) {
                ret.Add(new SpeedLimitLaneRecord {
                    LaneId = lane.laneId,
                    LaneIndex = lane.laneIndex,
                });
            }

            return ret;
        }

        public byte[] Serialize() => SerializationUtil.Serialize(this);
    }
}
