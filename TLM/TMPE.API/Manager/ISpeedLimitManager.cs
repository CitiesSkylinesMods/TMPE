namespace TrafficManager.API.Manager {
    using System;

    public interface ISpeedLimitManager {
        // TODO define me!

        public NetInfo.LaneType LaneTypes { get; }

        public VehicleInfo.VehicleType VehicleTypes { get; }

        /// <returns>The override or default road speed limit, in the game units.</returns>
        [Obsolete]
        float GetGameSpeedLimit(uint laneId);

        /// <returns>The override or default road speed limit, in the game units.</returns>
        float GetGameSpeedLimit(uint laneId, NetInfo.Lane laneInfo);
    }
}
