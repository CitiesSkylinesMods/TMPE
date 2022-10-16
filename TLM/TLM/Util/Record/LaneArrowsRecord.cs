namespace TrafficManager.Util.Record {
    using System;
    using System.Collections.Generic;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.Manager.Impl;
    using TrafficManager.State;
    using TrafficManager.Util.Extensions;

    [Serializable]
    public class LaneArrowsRecord : IRecordable {
        public uint LaneId;
        InstanceID InstanceID => new InstanceID { NetLane = LaneId };

        private LaneArrows? arrows_;

        public void Record() {
            arrows_ = Flags.GetLaneArrowFlags(LaneId);
        }

        public bool IsDefault() => arrows_ == null;

        public void Restore() => Transfer(LaneId);

        public void Transfer(Dictionary<InstanceID, InstanceID> map) =>
            Transfer(map[InstanceID].NetLane);

        public void Transfer(uint laneId) {
            //Log._Debug($"Restore: SetLaneArrows({LaneId}, {arrows_})");
            if (arrows_ == null)
                return;
            LaneArrowManager.Instance.SetLaneArrows(laneId, arrows_.Value);
        }

        /// <summary>
        /// Obtain lane arrow records for the lanes exiting the specified
        /// end of a segment.
        /// </summary>
        /// <param name="segmentId">The id of the segment to inspect.</param>
        /// <param name="startNode">
        /// If <c>true</c>, get lanes exiting via the start node; otherwise
        /// get lanes exiting via the end node.</param>
        /// <returns>
        /// Returns a sorted list of <see cref="LaneArrowsRecord"/> associated
        /// with each lane exiting via the specified segment end. The list may
        /// be empty if no matching lanes were found.
        /// </returns>
        public static List<LaneArrowsRecord> GetLanes(ushort segmentId, bool startNode) {

            var lanes = segmentId.ToSegment().GetSortedLanes(
                startNode,
                LaneArrowManager.LANE_TYPES,
                LaneArrowManager.VEHICLE_TYPES,
                sort: false);

            var ret = new List<LaneArrowsRecord>(lanes.Count);

            foreach (var lane in lanes)
                ret.Add(new LaneArrowsRecord { LaneId = lane.laneId });

            return ret;
        }

        public byte[] Serialize() => SerializationUtil.Serialize(this);
    }
}
