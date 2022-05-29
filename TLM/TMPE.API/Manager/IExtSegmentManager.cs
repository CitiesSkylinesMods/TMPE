namespace TrafficManager.API.Manager {
    using TrafficManager.API.Traffic.Data;

    public interface IExtSegmentManager {
        /// <summary>
        /// Extended segment data
        /// </summary>
        ExtSegment[] ExtSegments { get; }

        /// <summary>
        /// Performs recalcution of the segment with the given id
        /// </summary>
        /// <param name="segmentId">segment id</param>
        void Recalculate(ushort segmentId);

        /// <summary>
        /// Calculates if the given segment is a one-way road.
        /// </summary>
        /// <returns>true, if the managed segment is a one-way road, false otherwise</returns>
        bool CalculateIsOneWay(ushort segmentId);

        /// <summary>
        /// Calculates if the given segment has a buslane.
        /// </summary>
        /// <param name="segmentId">segment to check</param>
        /// <returns>true, if the given segment has a buslane, false otherwise</returns>
        bool CalculateHasBusLane(ushort segmentId);

        /// <summary>
        /// Calculates if the given segment is a highway
        /// </summary>
        /// <param name="segmentId"></param>
        /// <returns></returns>
        bool CalculateIsHighway(ushort segmentId);

        /// <summary>
        /// Returns the Lane ID of the specified lane.
        /// </summary>
        /// <param name="segmentId">a Segment ID</param>
        /// <param name="laneIndex">a lane index on segment <paramref name="segmentId"/></param>
        /// <returns>a Lane ID, or 0 if <paramref name="segmentId"/> and <paramref name="laneIndex"/> do not represent a valid lane</returns>
        public uint GetLaneId(ushort segmentId, int laneIndex);
    }
}