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
        /// <param name="vehicleId">The vehicle ID to inspect.</param>
        /// <returns>The extended vehicle type.</returns>
        /// <remarks>This works for any vehicle type, including those which TM:PE doesn't manage.</remarks>
        public static ExtVehicleType ToExtVehicleType(this ref Vehicle vehicle, ushort vehicleId)
            => vehicle.IsValid()
                ? GetTypeFromVehicleAI(vehicleId, ref vehicle)
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

        public static ExtVehicleType MapToExtVehicleTypeRestrictions(this VehicleInfo.VehicleCategory category, bool checkTrains) {
            if (category == VehicleInfo.VehicleCategory.None) {
                return ExtVehicleType.None;
            }
            ExtVehicleType type = ExtVehicleType.None;
            if ((category & VehicleInfo.VehicleCategory.PassengerCar) != 0) {
                type |= ExtVehicleType.PassengerCar;
            }
            if ((category & VehicleInfo.VehicleCategory.CargoTruck) != 0) {
                type |= ExtVehicleType.CargoTruck;
            }
            if ((category & VehicleInfo.VehicleCategory.Bus) != 0) {
                type |= ExtVehicleType.Bus;
            }
            if ((category & VehicleInfo.VehicleCategory.Trolleybus) != 0) {
                type |= ExtVehicleType.Bus;
            }
            if ((category & VehicleInfo.VehicleCategory.Taxi) != 0) {
                type |= ExtVehicleType.Taxi;
            }
            if ((category & VehicleInfo.VehicleCategory.Hearse) != 0) {
                type |= ExtVehicleType.Service;
            }
            if ((category & (VehicleInfo.VehicleCategory.CityServices & ~VehicleInfo.VehicleCategory.Emergency)) != 0) {
                type |= ExtVehicleType.Service;
            }
            if ((category & VehicleInfo.VehicleCategory.Emergency) != 0) {
                type |= ExtVehicleType.Emergency;
            }
            if (checkTrains) {
                if ((category & VehicleInfo.VehicleCategory.Trains) != 0) {
                    type |= ExtVehicleType.RailVehicle;
                }
            }
            return type;
        }

        /// <summary>
        /// Maps VehicleCategory to ExtVehicleType - works correctly only for VehicleType.Car or shared with Car (Car | Tram)
        /// Useful as a fallback in case of CreatePath request for not explicitly supported AIs (including custom)
        /// </summary>
        /// <param name="category"></param>
        /// <returns></returns>
        public static ExtVehicleType MapCarVehicleCategoryToExtVehicle(this VehicleInfo.VehicleCategory category) {
            if ((category & (VehicleInfo.VehicleCategory.Ambulance |
                             VehicleInfo.VehicleCategory.FireTruck |
                             VehicleInfo.VehicleCategory.Police |
                             VehicleInfo.VehicleCategory.Disaster |
                             VehicleInfo.VehicleCategory.VacuumTruck)) != 0) {
                return ExtVehicleType.Emergency;
            } else if ((category & (VehicleInfo.VehicleCategory.GarbageTruck |
                                    VehicleInfo.VehicleCategory.Hearse |
                                    VehicleInfo.VehicleCategory.MaintenanceTruck |
                                    VehicleInfo.VehicleCategory.ParkTruck |
                                    VehicleInfo.VehicleCategory.PostTruck |
                                    VehicleInfo.VehicleCategory.SnowTruck |
                                    VehicleInfo.VehicleCategory.BankTruck)) != 0) {
                return ExtVehicleType.Service;
            } else if ((category & VehicleInfo.VehicleCategory.Bus) != 0) {
                return ExtVehicleType.Bus;
            } else if ((category & VehicleInfo.VehicleCategory.Trolleybus) != 0) {
                return ExtVehicleType.Trolleybus;
            } else if ((category & VehicleInfo.VehicleCategory.CargoTruck) != 0) {
                return ExtVehicleType.CargoTruck;
            } else if ((category & VehicleInfo.VehicleCategory.PassengerCar) != 0) {
                return ExtVehicleType.PassengerCar;
            } else if ((category & VehicleInfo.VehicleCategory.Taxi) != 0) {
                return ExtVehicleType.Taxi;
            } else if ((category & VehicleInfo.VehicleCategory.Tram) != 0) {
                return ExtVehicleType.Tram;
            }
            return ExtVehicleType.None;
        }

        /// <summary>
        /// Inspects the AI of the <paramref name="vehicle"/> to determine its type.
        /// </summary>
        /// <param name="vehicleId">The vehicle id to inspect.</param>
        /// <param name="vehicle">The vehicle to inspect.</param>
        /// <returns>The determined <see cref="ExtVehicleType"/>.</returns>
        /// <remarks>If vehicle AI not recognised, returns <see cref="ExtVehicleType.None"/>.</remarks>
        private static ExtVehicleType GetTypeFromVehicleAI(ushort vehicleId, ref Vehicle vehicle) {
            VehicleInfo info = vehicle.Info;
            if (!info) {
                // broken assets may have broken Info instance but still valid flags
                // (probably other mods prevent proper skipping of broken assets)
                return ExtVehicleType.None;
            }

            var vehicleAI = info.m_vehicleAI;
            var emergency = vehicle.m_flags.IsFlagSet(Vehicle.Flags.Emergency2);

            var ret = ExtVehicleManager.Instance.DetermineVehicleTypeFromAIType(
                vehicleId,
                vehicleAI,
                emergency);

            return ret ?? ExtVehicleType.None;
        }
    }
}
