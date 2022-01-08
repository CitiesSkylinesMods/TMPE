namespace TrafficManager.API.Manager {
    using JetBrains.Annotations;

    public interface ISpeedLimitManager {
        // TODO define me!

        public NetInfo.LaneType LaneTypes { get; }

        public VehicleInfo.VehicleType VehicleTypes { get; }

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
