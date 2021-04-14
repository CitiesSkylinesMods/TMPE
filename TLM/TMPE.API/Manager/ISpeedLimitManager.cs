namespace TrafficManager.API.Manager {
    public interface ISpeedLimitManager {
        // TODO define me!

        /// <summary>
        /// Retrieves the speed limit for the given lane without locking.
        /// </summary>
        /// <param name="segmentId">segment id</param>
        /// <param name="laneIndex">lane index</param>
        /// <param name="laneId">lane id</param>
        /// <param name="laneInfo">lane info</param>
        /// <returns>speed limit in game units</returns>
        float GetLockFreeGameSpeedLimit(ushort segmentId,
                                        byte laneIndex,
                                        uint laneId,
                                        NetInfo.Lane laneInfo);
    }
}