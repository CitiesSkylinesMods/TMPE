namespace TrafficManager.Manager.Impl {
    using ColossalFramework;
    using CSUtil.Commons;
    using System.Threading;
    using System;
    using System.Collections.Generic;
    using JetBrains.Annotations;
    using TrafficManager.API.Manager;
    using TrafficManager.State;
    using UnityEngine;
    using TrafficManager.Lifecycle;
    using Util.Record;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.Util.Extensions;

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
                ref Vehicle vehicle = ref ((ushort)vehicleId).ToVehicle();

                if (!vehicle.IsValid()) {
                    continue;
                }

                if ((vehicle.ToExtVehicleType() & filter) == 0) {
                    continue;
                }

                count++;
            }
            return count;
        }

        public void DespawnVehicles(ExtVehicleType? filter = null) {
            lock (Singleton<VehicleManager>.instance) {
                try {
                    var logStr = filter.HasValue
                        ? filter == 0
                            ? "Nothing (filter == 0)"
                            : filter.ToString()
                        : "All vehicles";

                    Log.Info($"Utility Manager: Despawning {logStr}");

                    var manager = Singleton<VehicleManager>.instance;

                    for (uint vehicleId = 0; vehicleId < manager.m_vehicles.m_size; ++vehicleId) {
                        ref Vehicle vehicle = ref ((ushort)vehicleId).ToVehicle();

                        if (!vehicle.IsValid()) {
                            continue;
                        }

                        if (filter.HasValue && (vehicle.ToExtVehicleType() & filter) == 0) {
                            continue;
                        }

                        manager.ReleaseVehicle((ushort)vehicleId);
                    }

                    TrafficMeasurementManager.Instance.ResetTrafficStats();
                }
                catch (Exception ex) {
                    Log.Error($"Error occured while trying to despawn vehicles: {ex}");
                }
            }
        }

        public void ClearTraffic() => DespawnVehicles();

        public void RemoveParkedVehicles() {
            lock (Singleton<VehicleManager>.instance) {
                try {
                    Log.Info("UtilityManager: Despawning parked vehicles");

                    VehicleManager vehicleManager = Singleton<VehicleManager>.instance;

                    for (uint i = 0; i < vehicleManager.m_parkedVehicles.m_size; ++i) {
                        if ((vehicleManager.m_parkedVehicles.m_buffer[i].m_flags &
                             (ushort)VehicleParked.Flags.Created) != 0) {
                            vehicleManager.ReleaseParkedVehicle((ushort)i);
                        }
                    }
                } catch (Exception ex) {
                    Log.Error($"Error occured while trying to remove parked vehicles: {ex}");
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
            CitizenManager citizenManager = Singleton<CitizenManager>.instance;
            PathManager pathManager = Singleton<PathManager>.instance;

            for (uint citizenInstanceId = 1; citizenInstanceId < CitizenManager.MAX_INSTANCE_COUNT; ++citizenInstanceId) {
                ref CitizenInstance citizenInstance = ref ((ushort)citizenInstanceId).ToCitizenInstance();
                
                // Log._Debug($"UtilityManager.RemoveStuckEntities(): Processing instance {citizenInstanceId}.");
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
                // Log._Debug($"UtilityManager.RemoveStuckEntities(): Processing vehicle {vehicleId}.");
                if ((vehicleManager.m_vehicles.m_buffer[vehicleId].m_flags & Vehicle.Flags.WaitingPath) != 0) {
                    if (vehicleManager.m_vehicles.m_buffer[vehicleId].m_path != 0u) {
                        Log.Info($"Resetting stuck vehicle {vehicleId} (waiting for path)");

                        pathManager.ReleasePath(vehicleManager.m_vehicles.m_buffer[vehicleId].m_path);
                        vehicleManager.m_vehicles.m_buffer[vehicleId].m_path = 0u;
                    }

                    vehicleManager.m_vehicles.m_buffer[vehicleId].m_flags &= ~Vehicle.Flags.WaitingPath;
                }
            }

            Log.Info(
                "UtilityManager.RemoveStuckEntities(): Resetting vehicles that are parking and " +
                "where no parked vehicle is assigned to the driver.");

            for (uint vehicleId = 1; vehicleId < maxVehicleCount; ++vehicleId) {
                // Log._Debug($"UtilityManager.RemoveStuckEntities(): Processing vehicle {vehicleId}.");
                if ((vehicleManager.m_vehicles.m_buffer[vehicleId].m_flags
                     & Vehicle.Flags.Parking) != 0)
                {
                    ushort driverInstanceId = Constants.ManagerFactory.ExtVehicleManager.GetDriverInstanceId(
                        (ushort)vehicleId, ref vehicleManager.m_vehicles.m_buffer[vehicleId]);
                    uint citizenId = driverInstanceId.ToCitizenInstance().m_citizen;

                    if (citizenId != 0u && citizenId.ToCitizen().m_parkedVehicle == 0)
                    {
                        Log.Info($"Resetting vehicle {vehicleId} (parking without parked vehicle)");
                        vehicleManager.m_vehicles.m_buffer[vehicleId].m_flags &= ~Vehicle.Flags.Parking;
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