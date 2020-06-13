namespace TrafficManager.API.Manager {
    using TrafficManager.API.Traffic.Data;
    using TrafficManager.API.Traffic.Enums;

    public interface IExtVehicleManager {
        /// <summary>
        /// Extended vehicle data
        /// </summary>
        ExtVehicle[] ExtVehicles { get; }

        /// <summary>
        /// Handles a released vehicle.
        /// </summary>
        /// <param name="vehicleId">vehicle id</param>
        /// <param name="vehicleData">vehicle data</param>
        void OnReleaseVehicle(ushort vehicleId, ref Vehicle vehicleData);

        /// <summary>
        /// Handles a released vehicle.
        /// </summary>
        /// <param name="extVehicle">vehicle</param>
        void OnRelease(ref ExtVehicle extVehicle, ref Vehicle vehicleData);

        /// <summary>
        /// Handles a created vehicle.
        /// </summary>
        /// <param name="vehicleId">vehicle id</param>
        /// <param name="vehicleData">vehicle data</param>
        void OnCreateVehicle(ushort vehicleId, ref Vehicle vehicleData);

        /// <summary>
        /// Handles a created vehicle.
        /// </summary>
        /// <param name="extVehicle">vehicle</param>
        void OnCreate(ref ExtVehicle extVehicle, ref Vehicle vehicleData);

        /// <summary>
        /// Handles a spawned vehicle.
        /// </summary>
        /// <param name="vehicleId">vehicle id</param>
        /// <param name="vehicleData">vehicle data</param>
        void OnSpawnVehicle(ushort vehicleId, ref Vehicle vehicleData);

        /// <summary>
        /// Handles a spawned vehicle.
        /// </summary>
        /// <param name="extVehicle">vehicle</param>
        void OnSpawn(ref ExtVehicle extVehicle, ref Vehicle vehicleData);

        /// <summary>
        /// Handles a despawned vehicle.
        /// </summary>
        /// <param name="vehicleId">vehicle id</param>
        /// <param name="vehicleData">vehicle data</param>
        void OnDespawnVehicle(ushort vehicleId, ref Vehicle vehicleData);

        /// <summary>
        /// Handles a despawned vehicle.
        /// </summary>
        /// <param name="extVehicle">vehicle</param>
        void OnDespawn(ref ExtVehicle extVehicle);

        /// <summary>
        /// Handles a vehicle when path-finding starts.
        /// </summary>
        /// <param name="vehicleId">vehicle id</param>
        /// <param name="vehicleData">vehicle data</param>
        /// <param name="vehicleType">vehicle type to set, optional</param>
        /// <returns>vehicle type</returns>
        ExtVehicleType OnStartPathFind(ushort vehicleId,
                                       ref Vehicle vehicleData,
                                       ExtVehicleType? vehicleType);

        /// <summary>
        /// Handles a vehicle when path-finding starts.
        /// </summary>
        /// <param name="extVehicle">vehicle</param>
        /// <param name="vehicleData">vehicle data</param>
        /// <param name="vehicleType">vehicle type to set, optional</param>
        /// <returns>vehicle type</returns>
        ExtVehicleType OnStartPathFind(ref ExtVehicle extVehicle,
                                       ref Vehicle vehicleData,
                                       ExtVehicleType? vehicleType);

        /// <summary>
        /// Retrieves the driver citizen instance id for the given vehicle.
        /// </summary>
        /// <param name="vehicleId">vehicle id</param>
        /// <param name="data">vehicle data</param>
        /// <returns></returns>
        ushort GetDriverInstanceId(ushort vehicleId, ref Vehicle data);

        /// <summary>
        /// Updates the vehicle's current path position.
        /// </summary>
        /// <param name="vehicleId">vehicle id</param>
        /// <param name="vehicleData">vehicle data</param>
        void UpdateVehiclePosition(ushort vehicleId, ref Vehicle vehicleData);

        /// <summary>
        /// Updates the vehicle's current path position.
        /// </summary>
        /// <param name="extVehicle">vehicle</param>
        /// <param name="vehicleData">vehicle data</param>
        /// <param name="segEnd">ext. segment end</param>
        /// <param name="curPos">current path position</param>
        /// <param name="nextPos">next path position</param>
        void UpdatePosition(ref ExtVehicle extVehicle,
                            ref Vehicle vehicleData,
                            ref ExtSegmentEnd segEnd,
                            ref PathUnit.Position curPos,
                            ref PathUnit.Position nextPos);

        /// <summary>
        /// Unlinks the given vehicle from any segment end that the vehicle is currently linked to.
        /// </summary>
        /// <param name="extVehicle">vehicle</param>
        void Unlink(ref ExtVehicle extVehicle);

        /// <summary>
        /// Updates the vehicle's junction transit state to the given value.
        /// </summary>
        /// <param name="extVehicle">vehicle</param>
        /// <param name="transitState">junction transit state</param>
        void SetJunctionTransitState(ref ExtVehicle extVehicle,
                                     VehicleJunctionTransitState transitState);

        /// <summary>
        /// Determines if the junction transit state has been recently modified
        /// </summary>
        /// /// <param name="extVehicle">vehicle</param>
        /// <returns><code>true</code> if the junction transit state is considered new, <code>false</code> otherwise</returns>
        bool IsJunctionTransitStateNew(ref ExtVehicle extVehicle);

        /// <summary>
        /// Triggers the randomization mechanism that might select a new random value for time-varying vehicle behavior (e.g. speed)
        /// </summary>
        /// <param name="extVehicle">ext. vehicle data</param>
        /// <param name="force">whether to force generation of a new value</param>
        void StepRand(ref ExtVehicle extVehicle, bool force);

        /// <summary>
        /// Calculates the current randomization value for a vehicle.
        /// The value changes over time.
        /// </summary>
        /// <param name="vehicleId">vehicle id</param>
        /// <returns>a number between 0 and 99</returns>
        uint GetTimedVehicleRand(ushort vehicleId);

        /// <summary>
        /// Calculates the randomization value for a vehicle.
        /// The value is static throughout the vehicle's lifetime.
        /// </summary>
        /// <param name="vehicleId">vehicle id</param>
        /// <returns>a number between 0 and 99</returns>
        uint GetStaticVehicleRand(ushort vehicleId);

        /// <summary>
        /// Logs the given vehicle for traffic measurement.
        /// </summary>
        /// <param name="vehicleId">vehicle id</param>
        /// <param name="vehicle">vehicle</param>
        void LogTraffic(ushort vehicleId, ref Vehicle vehicle);

        /// <summary>
        /// Randomly selects a new set of DLS parameters.
        /// </summary>
        /// <param name="extVehicle">ext. vehicle data</param>
        void UpdateDynamicLaneSelectionParameters(ref ExtVehicle extVehicle);
    }
}