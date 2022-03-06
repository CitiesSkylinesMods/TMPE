namespace TrafficManager.Util.Extensions {
    using ColossalFramework;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.Manager.Impl;

    public static class VehicleExtensions {
        private static Vehicle[] _vehicleBuffer = Singleton<VehicleManager>.instance.m_vehicles.m_buffer;

        /// <summary>Returns a reference to the vehicle instance.</summary>
        /// <param name="vehicleId">The ID of the vehicle instance to obtain.</param>
        /// <returns>The vehicle instance.</returns>
        public static ref Vehicle ToVehicle(this ushort vehicleId) => ref _vehicleBuffer[vehicleId];

        /// <summary>Returns a reference to the vehicle instance.</summary>
        /// <param name="vehicleId">The ID of the vehicle instance to obtain.</param>
        /// <returns>The vehicle instance.</returns>
        public static ref Vehicle ToVehicle(this uint vehicleId) => ref _vehicleBuffer[vehicleId];

        public static bool IsCreated(this ref Vehicle vehicle) =>
            vehicle.m_flags.IsFlagSet(Vehicle.Flags.Created);

        public static bool IsParking(this ref Vehicle vehicle) =>
            vehicle.m_flags.IsFlagSet(Vehicle.Flags.Parking);

        /// <summary>
        /// Checks if the vehicle is Created, but not Deleted.
        /// </summary>
        /// <param name="vehicle">vehicle</param>
        /// <returns>True if the vehicle is valid, otherwise false.</returns>
        public static bool IsValid(this ref Vehicle vehicle) =>
            vehicle.m_flags.CheckFlags(
                required: Vehicle.Flags.Created,
                forbidden: Vehicle.Flags.Deleted);

        public static bool IsWaitingPath(this ref Vehicle vehicle) =>
            vehicle.m_flags.IsFlagSet(Vehicle.Flags.WaitingPath);

        /// <summary>Determines the <see cref="ExtVehicleType"/> for a vehicle based on its AI.</summary>
        /// <param name="vehicle">The vehicle to inspect.</param>
        /// <returns>The extended vehicle type.</returns>
        /// <remarks>This works for any vehicle type, including those which TM:PE doesn't manage.</remarks>
        public static ExtVehicleType ToExtVehicleType(this ref Vehicle vehicle)
            => vehicle.IsValid()
                ? GetTypeFromVehicleAI(ref vehicle)
                : ExtVehicleType.None;

        /// <summary>Determines the <see cref="ExtVehicleType"/> for a managed vehicle type.</summary>
        /// <param name="vehicleId">The id of the vehicle to inspect.</param>
        /// <returns>The extended vehicle type.</returns>
        /// <remarks>
        /// Only works for managed vehicle types listed in <see cref="ExtVehicleManager.VEHICLE_TYPES"/>
        /// </remarks>
        public static ExtVehicleType ToExtVehicleType(this ushort vehicleId)
            => ExtVehicleManager.Instance.ExtVehicles[vehicleId].vehicleType;

        /// <summary>Determines the <see cref="ExtVehicleType"/> for a managed vehicle type.</summary>
        /// <param name="vehicleId">The id of the vehicle to inspect.</param>
        /// <returns>The extended vehicle type.</returns>
        /// <remarks>
        /// Only works for managed vehicle types listed in <see cref="ExtVehicleManager.VEHICLE_TYPES"/>
        /// </remarks>
        public static ExtVehicleType ToExtVehicleType(this uint vehicleId)
            => ExtVehicleManager.Instance.ExtVehicles[vehicleId].vehicleType;

        /// <summary>
        /// Inspects the AI of the <paramref name="vehicle"/> to determine its type.
        /// </summary>
        /// <param name="vehicle">The vehicle to inspect.</param>
        /// <returns>The determined <see cref="ExtVehicleType"/>.</returns>
        /// <remarks>If vehicle AI not recognised, returns <see cref="ExtVehicleType.None"/>.</remarks>
        private static ExtVehicleType GetTypeFromVehicleAI(ref Vehicle vehicle) {
            var vehicleId = vehicle.Info.m_instanceID.Vehicle;
            var vehicleAI = vehicle.Info.m_vehicleAI;
            var emergency = vehicle.m_flags.IsFlagSet(Vehicle.Flags.Emergency2);

            var ret = ExtVehicleManager.Instance.DetermineVehicleTypeFromAIType(
                vehicleId,
                vehicleAI,
                emergency);

            return ret ?? ExtVehicleType.None;
        }
    }
}
