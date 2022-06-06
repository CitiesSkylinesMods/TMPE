namespace TrafficManager.Util.Extensions {
    using API.Traffic.Data;
    using ColossalFramework;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.Manager.Impl;

    public static class VehicleExtensions {
        private static Vehicle[] _vehicleBuffer = Singleton<VehicleManager>.instance.m_vehicles.m_buffer;
        private static ExtVehicle[] _extVehicles = ExtVehicleManager.Instance.ExtVehicles;

        /// <summary>Returns a reference to the vehicle instance.</summary>
        /// <param name="vehicleId">The ID of the vehicle instance to obtain.</param>
        /// <returns>The vehicle instance.</returns>
        public static ref Vehicle ToVehicle(this ushort vehicleId) => ref _vehicleBuffer[vehicleId];

        /// <summary>Returns a reference to the vehicle instance.</summary>
        /// <param name="vehicleId">The ID of the vehicle instance to obtain.</param>
        /// <returns>The vehicle instance.</returns>
        public static ref Vehicle ToVehicle(this uint vehicleId) => ref _vehicleBuffer[vehicleId];

        /// <summary>Returns a reference to the ext vehicle instance.</summary>
        /// <param name="vehicleId">The ID of the ext vehicle instance to obtain.</param>
        /// <returns>The vehicle instance.</returns>
        public static ref ExtVehicle ToExtVehicle(this ushort vehicleId) => ref _extVehicles[vehicleId];

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
            => _extVehicles[vehicleId].vehicleType;

        /// <summary>Determines the <see cref="ExtVehicleType"/> for a managed vehicle type.</summary>
        /// <param name="vehicleId">The id of the vehicle to inspect.</param>
        /// <returns>The extended vehicle type.</returns>
        /// <remarks>
        /// Only works for managed vehicle types listed in <see cref="ExtVehicleManager.VEHICLE_TYPES"/>
        /// </remarks>
        public static ExtVehicleType ToExtVehicleType(this uint vehicleId)
            => _extVehicles[vehicleId].vehicleType;

        public static bool IsEmergencyRespondingCar(this ref Vehicle vehicle) =>
            (vehicle.m_flags & Vehicle.Flags.Emergency2) != 0 &&
            vehicle.Info.m_vehicleType == VehicleInfo.VehicleType.Car;

        /// <summary>
        /// Inspects the AI of the <paramref name="vehicle"/> to determine its type.
        /// </summary>
        /// <param name="vehicle">The vehicle to inspect.</param>
        /// <returns>The determined <see cref="ExtVehicleType"/>.</returns>
        /// <remarks>If vehicle AI not recognised, returns <see cref="ExtVehicleType.None"/>.</remarks>
        private static ExtVehicleType GetTypeFromVehicleAI(ref Vehicle vehicle) {
            VehicleInfo info = vehicle.Info;
            var vehicleId = info.m_instanceID.Vehicle;
            var vehicleAI = info.m_vehicleAI;
            // plane can have Emergency2 flag set in normal conditions
            var emergency = vehicle.m_flags.IsFlagSet(Vehicle.Flags.Emergency2) &&
                            info.m_vehicleType.IsFlagSet(VehicleInfo.VehicleType.Car);

            var ret = ExtVehicleManager.Instance.DetermineVehicleTypeFromAIType(
                vehicleId,
                vehicleAI,
                emergency);

            return ret ?? ExtVehicleType.None;
        }
    }
}
