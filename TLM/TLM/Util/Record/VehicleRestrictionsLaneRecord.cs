namespace TrafficManager.Util.Record {
    using System;
    using System.Collections.Generic;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.Manager.Impl;
    using TrafficManager.State;
    using TrafficManager.Util.Extensions;

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

        public bool IsDefault() => allowedVehicleTypes_ == null;

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
            var laneInfo = segmentId.ToSegment().GetLaneInfo(laneIndex);
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

        /// <summary>
        /// Obtain vehicle restriction records for applicable lanes within the segment.
        /// </summary>
        /// <param name="segmentId">The id of the segment to inspect.</param>
        /// <returns>
        /// Returns a list of <see cref="VehicleRestrictionsLaneRecord"/> for all
        /// restriction-applicable lanes in the segment. The list may be empty if no
        /// matching lanes were found.
        /// </returns>
        public static List<VehicleRestrictionsLaneRecord> GetLanes(ushort segmentId) {

            var lanes = segmentId.ToSegment().GetSortedLanes(
                null,
                LANE_TYPES,
                VEHICLE_TYPES,
                sort: false);

            var ret = new List<VehicleRestrictionsLaneRecord>(lanes.Count);

            foreach (var lane in lanes) {
                ret.Add(new VehicleRestrictionsLaneRecord {
                    SegmentId = segmentId,
                    LaneId = lane.laneId,
                    LaneIndex = lane.laneIndex,
                });
            }

            return ret;
        }

        public byte[] Serialize() => SerializationUtil.Serialize(this);
    }
}
