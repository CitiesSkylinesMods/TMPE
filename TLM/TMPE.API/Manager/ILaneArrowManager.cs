namespace TrafficManager.API.Manager {
    using TrafficManager.API.Traffic.Enums;

    public interface ILaneArrowManager {
        // TODO define me!

        /// <summary>lane types for all road vehicles.</summary>
        public NetInfo.LaneType LaneTypes { get; }

        /// <summary>vehicle types for all road vehicles</summary>
        public VehicleInfo.VehicleType VehicleTypes { get; }

        /// <summary>
        /// Get the final lane arrows considering both the default lane arrows and user modifications.
        /// </summary>
        public LaneArrows GetFinalLaneArrows(uint laneId);
    }
}