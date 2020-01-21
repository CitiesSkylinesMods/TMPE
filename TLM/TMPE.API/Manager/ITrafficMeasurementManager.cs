namespace TrafficManager.API.Manager {
    using TrafficManager.API.Traffic.Data;

    public interface ITrafficMeasurementManager {
        // TODO define me!

        /// <summary>
        /// Traffic data per segment and lane
        /// </summary>
        LaneTrafficData[][] LaneTrafficData { get; }

        /// <summary>
        /// Traffic data per segment and traffic direction
        /// </summary>
        SegmentDirTrafficData[] SegmentDirTrafficData { get; }

        /// <summary>
        /// Handles a segment before a simulation step is performed.
        /// </summary>
        /// <param name="segmentId">segment id</param>
        /// <param name="segment">segment data</param>
        void OnBeforeSimulationStep(ushort segmentId, ref NetSegment segment);

        ushort CalcLaneRelativeMeanSpeed(ushort segmentId,
                                         byte laneIndex,
                                         uint laneId,
                                         NetInfo.Lane laneInfo);

        bool GetLaneTrafficData(ushort segmentId, byte laneIndex, out LaneTrafficData trafficData);

        void DestroySegmentStats(ushort segmentId);

        void ResetTrafficStats();

        void AddTraffic(ushort segmentId, byte laneIndex, ushort speed);

        int GetDirIndex(ushort segmentId, NetInfo.Direction dir);

        int GetDirIndex(NetInfo.Direction dir);
    }
}