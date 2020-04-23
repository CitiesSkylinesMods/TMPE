namespace TrafficManager.Util.Record {
    using ColossalFramework;
    using System.Collections.Generic;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.Manager.Impl;
    using static TrafficManager.Util.Shortcuts;

    public class LaneRecord : IRecordable {
        public const NetInfo.LaneType LANE_TYPES =
            LaneArrowManager.LANE_TYPES | SpeedLimitManager.LANE_TYPES;
        public const VehicleInfo.VehicleType VEHICLE_TYPES =
            LaneArrowManager.VEHICLE_TYPES | SpeedLimitManager.VEHICLE_TYPES;

        public byte LaneIndex;
        public uint LaneId;
        NetInfo.Lane LaneInfo;
        ushort SegmentId;

        private LaneArrows Arrows;
        private float SpeedLimit; // game units

        public void Record() {
            Arrows = LaneArrowManager.Instance.GetFinalLaneArrows(LaneId);
            SpeedLimit = SpeedLimitManager.Instance.GetCustomSpeedLimit(LaneId);
        }

        public void Restore() {
            LaneArrowManager.Instance.SetLaneArrows(LaneId, Arrows);
            SpeedLimitManager.Instance.SetSpeedLimit(
                SegmentId,
                LaneIndex,
                LaneInfo,
                LaneId,
                SpeedLimit);
        }

        public static List<LaneRecord> GetLanes(ushort segmentId) {
            var ret = new List<LaneRecord>();
            ref NetSegment segment = ref segmentId.ToSegment();
            uint laneId = segment.m_lanes;
            NetInfo.Lane[] lanes = segment.Info.m_lanes;

            for (byte laneIndex = 0; (laneIndex < lanes.Length) && (laneId != 0); laneIndex++) {
                NetInfo.Lane laneInfo = lanes[laneIndex];
                if (!laneInfo.m_laneType.IsFlagSet(LANE_TYPES) ||
                    !laneInfo.m_vehicleType.IsFlagSet(VEHICLE_TYPES)) {
                    continue;
                }
                var laneData = new LaneRecord {
                    LaneId = laneId,
                    LaneIndex = laneIndex,
                    SegmentId = segmentId,
                    LaneInfo = laneInfo,
                };
                ret.Add(laneData);
                laneId = laneId.ToLane().m_nextLane;
            }
            return ret;
        }
    }
}
