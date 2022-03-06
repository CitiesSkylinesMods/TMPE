namespace TrafficManager.State.ConfigData {
    using API.Traffic.Enums;

    public class Gameplay {
        /// <summary>
        /// Modulo value for time-varying vehicle behavior randomization
        /// </summary>
        public uint VehicleTimedRandModulo = 10;

        /// <summary>
        /// Flags holding types allowed for despawning if "No Vehicle Despawning" option is enabled
        /// </summary>
        public ExtVehicleType AllowedDespawnVehicleTypes = ExtVehicleType.None;
    }
}