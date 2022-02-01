using ColossalFramework;
using TrafficManager.API.Traffic.Enums;
using TrafficManager.Manager.Impl;

namespace TrafficManager.Util.Extensions {
    public static class VehicleExtensions {
        private static Vehicle[] _vehBuffer = Singleton<VehicleManager>.instance.m_vehicles.m_buffer;

        /// <summary>Returns a reference to the vehicle instance.</summary>
        /// <param name="vehicleId">The ID of the vehicle instance to obtain.</param>
        /// <returns>The vehicle instance.</returns>
        public static ref Vehicle ToVehicle(this ushort vehicleId) => ref _vehBuffer[vehicleId];

        /// <summary>
        /// Checks if the vehicle is Created, but not Deleted.
        /// </summary>
        /// <param name="vehicle">vehicle</param>
        /// <returns>True if the vehicle is valid, otherwise false.</returns>
        public static bool IsValid(this ref Vehicle vehicle) =>
            vehicle.m_flags.CheckFlags(
                required: Vehicle.Flags.Created,
                forbidden: Vehicle.Flags.Deleted);

        /// <summary>Determines the <see cref="ExtVehicleType"/> for a vehicle.</summary>
        /// <param name="vehicle">The vehocle to inspect.</param>
        /// <returns>The extended vehicle type.</returns>
        public static ExtVehicleType ToExtVehicleType(this ref Vehicle vehicle) {
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
