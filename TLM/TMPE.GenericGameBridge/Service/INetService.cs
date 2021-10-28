namespace GenericGameBridge.Service {
    using System.Collections.Generic;

    public interface INetService {
        bool CheckLaneFlags(uint laneId,
                            NetLane.Flags flagMask,
                            NetLane.Flags? expectedResult = default);

        bool CheckNodeFlags(ushort nodeId,
                            NetNode.Flags flagMask,
                            NetNode.Flags? expectedResult = default);

        bool CheckSegmentFlags(ushort segmentId,
                               NetSegment.Flags flagMask,
                               NetSegment.Flags? expectedResult = default);

        ushort GetSegmentNodeId(ushort segmentId, bool startNode);

        /// <summary>
        /// Assembles a geometrically sorted list of lanes for the given segment.
        /// If the <paramref name="startNode"/> parameter is set only lanes supporting traffic to flow towards the given node are added to the list, otherwise all matched lanes are added.
        /// </summary>
        /// <param name="segmentId">segment id</param>
        /// <param name="segment">segment data</param>
        /// <param name="startNode">reference node (optional)</param>
        /// <param name="laneTypeFilter">lane type filter, lanes must match this filter mask</param>
        /// <param name="vehicleTypeFilter">vehicle type filter, lanes must match this filter mask</param>
        /// <param name="reverse">if true, lanes are ordered from right to left (relative to the
        ///     segment's start node / the given node), otherwise from left to right</param>
        /// <param name="sort">if false, no sorting takes place
        ///     regardless of <paramref name="reverse"/></param>
        /// <returns>sorted list of lanes for the given segment</returns>
        IList<LanePos> GetSortedLanes(ushort segmentId,
                                      ref NetSegment segment,
                                      bool? startNode,
                                      NetInfo.LaneType? laneTypeFilter = default,
                                      VehicleInfo.VehicleType? vehicleTypeFilter =
                                          default,
                                      bool reverse = false,
                                      bool sort = true);

        bool IsLaneAndItsSegmentValid(uint laneId);

        bool IsSegmentValid(ushort segmentId);

        GetNodeSegmentIdsEnumerable GetNodeSegmentIds(ushort nodeId, ClockDirection clockDirection);

        GetSegmentLaneIdsEnumerable GetSegmentLaneIdsAndLaneIndexes(ushort segmentId);

        void PublishSegmentChanges(ushort segmentId);
    }

    public struct LanePos {
        public uint laneId;
        public byte laneIndex;
        public float position;
        public VehicleInfo.VehicleType vehicleType;
        public NetInfo.LaneType laneType;

        public LanePos(uint laneId,
                       byte laneIndex,
                       float position,
                       VehicleInfo.VehicleType vehicleType,
                       NetInfo.LaneType laneType) {
            this.laneId = laneId;
            this.laneIndex = laneIndex;
            this.position = position;
            this.vehicleType = vehicleType;
            this.laneType = laneType;
        }
    }

    public enum ClockDirection {
        Clockwise,
        CounterClockwise,
    }
}