namespace TrafficManager.Util.Record {
    using System;
    using System.Collections.Generic;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.Manager.Impl;
    using TrafficManager.State;
    using static TrafficManager.Util.Shortcuts;

    [Serializable]
    public class VehicleRestrictionsLaneRecord : IRecordable {
        public const NetInfo.LaneType LANE_TYPES =
            LaneArrowManager.LANE_TYPES | VehicleRestrictionsManager.LANE_TYPES;
        public const VehicleInfo.VehicleType VEHICLE_TYPES =
            LaneArrowManager.VEHICLE_TYPES | VehicleRestrictionsManager.VEHICLE_TYPES;

        public ushort SegmentId;
        public byte LaneIndex;
        public uint LaneId;

        private ExtVehicleType? allowedVehicleTypes_;

        InstanceID InstanceID => new InstanceID { NetLane = LaneId };

        public void Record() {
            allowedVehicleTypes_ = VehicleRestrictionsManager.Instance.GetAllowedVehicleTypesRaw(SegmentId, LaneIndex);
        }

        public void Restore() => Transfer(SegmentId, LaneIndex, LaneId);

        public void Transfer(Dictionary<InstanceID, InstanceID> map) {
            ushort newSegmentId = map[new InstanceID { NetSegment = SegmentId }].NetSegment;
            uint newLaneId = map[this.InstanceID].NetLane;
            Transfer(
                segmentId: newSegmentId,
                laneIndex: LaneIndex,
                laneId: newLaneId);
        }

        public void Transfer(ushort segmentId, byte laneIndex, uint laneId) {
            var laneInfo = GetLaneInfo(segmentId, laneIndex);
            var segmentInfo = segmentId.ToSegment().Info;
            if (allowedVehicleTypes_ == null) {
                VehicleRestrictionsManager.Instance.ClearVehicleRestrictions(segmentId, laneIndex, laneId);
            } else {
                VehicleRestrictionsManager.Instance.SetAllowedVehicleTypes(
                    segmentId: segmentId,
                    segmentInfo,
                    laneIndex: LaneIndex,
                    laneInfo: laneInfo,
                    laneId: laneId,
                    allowedTypes: allowedVehicleTypes_.Value);
            }
        }

        public static List<VehicleRestrictionsLaneRecord> GetLanes(ushort segmentId) {
            int maxLaneCount = segmentId.ToSegment().Info.m_lanes.Length;
            var ret = new List<VehicleRestrictionsLaneRecord>(maxLaneCount);
            var lanes = netService.GetSortedLanes(
                segmentId,
                ref segmentId.ToSegment(),
                null,
                LANE_TYPES,
                VEHICLE_TYPES,
                sort: false);
            foreach (var lane in lanes) {
                VehicleRestrictionsLaneRecord laneData = new VehicleRestrictionsLaneRecord {
                    SegmentId = segmentId,
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
