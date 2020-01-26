namespace TrafficManager.API.Manager {
    using TrafficManager.API.Traffic.Data;

    public interface IExtSegmentManager {
        /// <summary>
        /// Extended segment data
        /// </summary>
        ExtSegment[] ExtSegments { get; }

        /// <summary>
        /// Checks if the segment with the given id is valid.
        /// </summary>
        /// <param name="segmentId">segment id</param>
        /// <returns><code>true</code> if the segment is valid, <code>false</code> otherwise</returns>
        bool IsValid(ushort segmentId);

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
    }
}