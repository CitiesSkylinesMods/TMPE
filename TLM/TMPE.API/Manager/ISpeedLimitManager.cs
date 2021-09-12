namespace TrafficManager.API.Manager {
    using JetBrains.Annotations;

    public interface ISpeedLimitManager {
        // TODO define me!

        /// <summary>
        /// For use by external code.
        /// Retrieves the speed limit for the given lane without locking.
        /// </summary>
        /// <param name="segmentId">segment id</param>
        /// <param name="laneIndex">lane index</param>
        /// <param name="laneId">lane id</param>
        /// <param name="laneInfo">lane info</param>
        /// <returns>speed limit in game units</returns>
        [UsedImplicitly]
        float GetLockFreeGameSpeedLimit(ushort segmentId,
                                        byte laneIndex,
                                        uint laneId,
                                        NetInfo.Lane laneInfo);

        /// <summary>
        /// For use by external code.
        /// Returns active speed limit for a lane.
        /// </summary>
        /// <param name="laneId">The lane.</param>
        /// <returns>The override or default road speed limit, in the game units.</returns>
        [UsedImplicitly]
        float GetGameSpeedLimit(uint laneId);
    }
}
