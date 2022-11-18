namespace TrafficManager.Manager.Impl {
    using ColossalFramework;
    using CSUtil.Commons;
    using JetBrains.Annotations;
    using System;
    using System.Collections.Generic;
    using TrafficManager.API.Manager;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.Lifecycle;
    using TrafficManager.State;
    using TrafficManager.Util;
    using TrafficManager.Util.Extensions;
    using TrafficManager.Util.Record;
    using UnityEngine;

    public class UtilityManager : AbstractCustomManager, IUtilityManager {
        static UtilityManager() {
            Instance = new UtilityManager();
        }

        public static UtilityManager Instance { get; }

        private readonly Queue<KeyValuePair<IRecordable, Dictionary<InstanceID, InstanceID>>> _transferRecordables = new();

        public int CountVehiclesMatchingFilter(ExtVehicleType filter) {
            int count = 0;
            var manager = Singleton<VehicleManager>.instance;

            for (uint vehicleId = 0; vehicleId < manager.m_vehicles.m_size; ++vehicleId) {

                ref Vehicle vehicle = ref vehicleId.ToVehicle();

                if (!vehicle.IsValid())
                    continue;

                if ((vehicle.ToExtVehicleType((ushort)vehicleId) & filter) == 0)
                    continue;

                count++;
            }
            return count;
        }

        public void DespawnVehicles(ExtVehicleType? filter = null) {
            var vehicleManager = Singleton<VehicleManager>.instance;

            try {
                var logStr =
                    filter.HasValue
                        ? (filter == 0)
                            ? "Nothing (filter == 0)"
                            : filter.ToString()
                        : "All vehicles";

                Log.Info($"Utility Manager: Despawning {logStr}");

                for (uint vehicleId = 0; vehicleId < vehicleManager.m_vehicles.m_size; ++vehicleId) {

                    ref Vehicle vehicle = ref vehicleId.ToVehicle();

                    if (!vehicle.IsValid())
                        continue;

                    ushort id = (ushort)vehicleId;
                    if (filter.HasValue && (vehicle.ToExtVehicleType(id) & filter) == 0)
                        continue;

                    vehicleManager.ReleaseVehicle(id);
                }

                TrafficMeasurementManager.Instance.ResetTrafficStats();
            }
            catch (Exception ex) {
                ex.LogException();
            }
        }

        public void ClearTraffic() => DespawnVehicles();

        public void RemoveParkedVehicles() {
            var vehicleManager = Singleton<VehicleManager>.instance;

            lock (vehicleManager) {
                try {
                    Log.Info("UtilityManager: Despawning parked vehicles");

                    for (uint parkedVehicleId = 0; parkedVehicleId < vehicleManager.m_parkedVehicles.m_size; ++parkedVehicleId) {
                        if (parkedVehicleId.ToParkedVehicle().IsCreated())
                        {
                            vehicleManager.ReleaseParkedVehicle((ushort)parkedVehicleId);
                        }
                    }
                } catch (Exception ex) {
                    Log.Error($"Error occurred while trying to remove parked vehicles: {ex}");
                }
            }
        }

        [UsedImplicitly]
        public void PrintAllDebugInfo() {
            Log._Debug("UtilityManager.PrintAllDebugInfo(): Pausing simulation.");
            Singleton<SimulationManager>.instance.ForcedSimulationPaused = true;

            Log._Debug("=== Flags.PrintDebugInfo() ===");
            try {
                Flags.PrintDebugInfo();
                SpeedLimitManager.Instance.PrintDebugInfo();
            }
            catch (Exception e) {
                Log.Error($"Error occurred while printing debug info for flags: {e}");
            }

            foreach (ICustomManager manager in TMPELifecycle.Instance.RegisteredManagers) {
                try {
                    manager.PrintDebugInfo();
                }
                catch (Exception e) {
                    Log.Error($"Error occurred while printing debug info for manager {manager.GetType().Name}: {e}");
                }
            }

            Log._Debug("UtilityManager.PrintAllDebugInfo(): Unpausing simulation.");
            Singleton<SimulationManager>.instance.ForcedSimulationPaused = false;
        }

        public void ResetStuckEntities() {
            Log.Info("UtilityManager.RemoveStuckEntities() called.");

            bool wasPausedBeforeReset = Singleton<SimulationManager>.instance.ForcedSimulationPaused;

            if (!wasPausedBeforeReset) {
                Log.Info("UtilityManager.RemoveStuckEntities(): Pausing simulation.");
                Singleton<SimulationManager>.instance.ForcedSimulationPaused = true;
            }

            Log.Info("UtilityManager.RemoveStuckEntities(): Waiting for all paths.");
            Singleton<PathManager>.instance.WaitForAllPaths();

            Log.Info("UtilityManager.RemoveStuckEntities(): Resetting citizen instances that are waiting for a path.");
            PathManager pathManager = Singleton<PathManager>.instance;
            CitizenManager citizenManager = CitizenManager.instance;
            CitizenInstance[] citizenInstancesBuf = citizenManager.m_instances.m_buffer;
            Citizen[] citizensBuf = citizenManager.m_citizens.m_buffer;
            uint maxCitizenInstanceCount = citizenManager.m_instances.m_size;

            for (uint citizenInstanceId = 1; citizenInstanceId < maxCitizenInstanceCount; ++citizenInstanceId) {
                ref CitizenInstance citizenInstance = ref citizenInstancesBuf[citizenInstanceId];

                if (citizenInstance.IsWaitingPath())
                {
                    if (citizenInstance.m_path != 0u) {
                        Log.Info(
                            $"Resetting stuck citizen instance {citizenInstanceId} (waiting for path)");

                        pathManager.ReleasePath(citizenInstance.m_path);
                        citizenInstance.m_path = 0u;
                    }

                    citizenInstance.m_flags &=
                        ~(CitizenInstance.Flags.WaitingTransport |
                          CitizenInstance.Flags.EnteringVehicle |
                          CitizenInstance.Flags.BoredOfWaiting | CitizenInstance.Flags.WaitingTaxi |
                          CitizenInstance.Flags.WaitingPath);
                } else {
#if DEBUG
                    if (citizenInstance.m_path == 0 && citizenInstance.IsCharacter()) {
                        float magnitude = (citizenInstance.GetLastFramePosition() - (Vector3)citizenInstance.m_targetPos).magnitude;
                        Log._DebugFormat(
                            "Found potential floating citizen instance: {0} Source building: {1} " +
                            "Target building: {2} Distance to target position: {3}",
                            citizenInstanceId,
                            citizenInstance.m_sourceBuilding,
                            citizenInstance.m_targetBuilding,
                            magnitude);
                    }
#endif
                }
            }

            Log.Info("UtilityManager.RemoveStuckEntities(): Resetting vehicles that are waiting for a path.");
            VehicleManager vehicleManager = Singleton<VehicleManager>.instance;
            var maxVehicleCount = vehicleManager.m_vehicles.m_buffer.Length;

            for (uint vehicleId = 1; vehicleId < maxVehicleCount; ++vehicleId) {
                ref Vehicle vehicle = ref vehicleId.ToVehicle();
                if (vehicle.IsWaitingPath()) {
                    if (vehicle.m_path != 0u) {
                        Log.Info($"Resetting stuck vehicle {vehicleId} (waiting for path)");

                        pathManager.ReleasePath(vehicle.m_path);
                        vehicle.m_path = 0u;
                    }

                    vehicle.m_flags &= ~Vehicle.Flags.WaitingPath;
                }
            }

            Log.Info(
                "UtilityManager.RemoveStuckEntities(): Resetting vehicles that are parking and " +
                "where no parked vehicle is assigned to the driver.");

            for (uint vehicleId = 1; vehicleId < maxVehicleCount; ++vehicleId) {
                ref Vehicle vehicle = ref vehicleId.ToVehicle();
                if (vehicle.IsParking())
                {
                    ushort driverInstanceId = Constants.ManagerFactory.ExtVehicleManager.GetDriverInstanceId(
                        (ushort)vehicleId, ref vehicle);
                    uint citizenId = citizenInstancesBuf[driverInstanceId].m_citizen;

                    if (citizenId != 0u && citizensBuf[citizenId].m_parkedVehicle == 0)
                    {
                        Log.Info($"Resetting vehicle {vehicleId} (parking without parked vehicle)");
                        vehicle.m_flags &= ~Vehicle.Flags.Parking;
                    }
                }
            }

            if (!wasPausedBeforeReset) {
                Log.Info("UtilityManager.RemoveStuckEntities(): Unpausing simulation.");
                Singleton<SimulationManager>.instance.ForcedSimulationPaused = false;
            }
        }

        /// <summary>
        /// Queues Transfer recordables to be processed at the end of simulation step
        /// </summary>
        /// <param name="recordable">Settings record to by applied</param>
        /// <param name="map">links between source and newly created clones</param>
        public void QueueTransferRecordable(IRecordable recordable,
                                            Dictionary<InstanceID, InstanceID> map) {
            _transferRecordables.Enqueue(
                new KeyValuePair<IRecordable, Dictionary<InstanceID, InstanceID>>(
                    recordable,
                    new Dictionary<InstanceID, InstanceID>(map)));
        }


        /// <summary>
        /// Processes queued transfer recordables
        /// </summary>
        public void ProcessTransferRecordableQueue() {
            while (_transferRecordables.Count > 0) {
                KeyValuePair<IRecordable, Dictionary<InstanceID, InstanceID>> recordablePair = _transferRecordables.Dequeue();
                recordablePair.Key.Transfer(recordablePair.Value);
            }
        }
    }
}