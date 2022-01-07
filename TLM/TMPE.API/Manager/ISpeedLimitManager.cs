namespace TrafficManager.API.Manager {
    using JetBrains.Annotations;

    public interface ISpeedLimitManager {
        // TODO define me!

        public NetInfo.LaneType LaneTypes { get; }

        public VehicleInfo.VehicleType VehicleTypes { get; }

        /// <returns>The override or default road speed limit, in the game units.</returns>
        [UsedImplicitly]
        float GetGameSpeedLimit(uint laneId);

        /// <returns>The override or default road speed limit, in the game units.</returns>
        float GetGameSpeedLimit(uint laneId, NetInfo.Lane laneInfo);
    }
}
