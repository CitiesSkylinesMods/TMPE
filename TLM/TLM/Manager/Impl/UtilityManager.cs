namespace TrafficManager.Manager.Impl {
    using ColossalFramework;
    using CSUtil.Commons;
    using System.Threading;
    using System;
    using JetBrains.Annotations;
    using TrafficManager.API.Manager;
    using TrafficManager.State;
    using UnityEngine;

    public class UtilityManager : AbstractCustomManager, IUtilityManager {
        static UtilityManager() {
            Instance = new UtilityManager();
        }

        public static UtilityManager Instance { get; }

        public void ClearTraffic() {
            lock (Singleton<VehicleManager>.instance) {
                try {
                    VehicleManager vehicleManager = Singleton<VehicleManager>.instance;
                    for (uint i = 0; i < vehicleManager.m_vehicles.m_size; ++i) {
                        if ((vehicleManager.m_vehicles.m_buffer[i].m_flags &
                             Vehicle.Flags.Created) != 0) {
                            vehicleManager.ReleaseVehicle((ushort)i);
                        }
                    }

                    TrafficMeasurementManager.Instance.ResetTrafficStats();
                } catch (Exception ex) {
                    Log.Error($"Error occured while trying to clear traffic: {ex}");
                }
            }
        }

        public void RemoveParkedVehicles() {
            lock (Singleton<VehicleManager>.instance) {
                try {
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
            }
            catch (Exception e) {
                Log.Error($"Error occurred while printing debug info for flags: {e}");
            }

            foreach (ICustomManager manager in LoadingExtension.RegisteredManagers) {
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
                // Log._Debug($"UtilityManager.RemoveStuckEntities(): Processing instance {citizenInstanceId}.");
                if ((citizenManager.m_instances.m_buffer[citizenInstanceId].m_flags &
                     CitizenInstance.Flags.WaitingPath) != CitizenInstance.Flags.None)
                {
                    // CitizenAI ai = citizenManager.m_instances.m_buffer[citizenInstanceId].Info.m_citizenAI;
                    if (citizenManager.m_instances.m_buffer[citizenInstanceId].m_path != 0u) {
                        Log.Info(
                            $"Resetting stuck citizen instance {citizenInstanceId} (waiting for path)");

                        pathManager.ReleasePath(citizenManager.m_instances.m_buffer[citizenInstanceId].m_path);
                        citizenManager.m_instances.m_buffer[citizenInstanceId].m_path = 0u;
                    }

                    citizenManager.m_instances.m_buffer[citizenInstanceId].m_flags &=
                        ~(CitizenInstance.Flags.WaitingTransport |
                          CitizenInstance.Flags.EnteringVehicle |
                          CitizenInstance.Flags.BoredOfWaiting | CitizenInstance.Flags.WaitingTaxi |
                          CitizenInstance.Flags.WaitingPath);
                } else {
#if DEBUG
                    if (citizenManager.m_instances.m_buffer[citizenInstanceId].m_path == 0 &&
                        (citizenManager.m_instances.m_buffer[citizenInstanceId].m_flags &
                         CitizenInstance.Flags.Character) != CitizenInstance.Flags.None)
                    {
                        float magnitude =
                            (citizenManager.m_instances.m_buffer[citizenInstanceId].GetLastFramePosition() -
                             (Vector3)citizenManager.m_instances.m_buffer[citizenInstanceId].m_targetPos
                             ).magnitude;
                        Log._DebugFormat(
                            "Found potential floating citizen instance: {0} Source building: {1} " +
                            "Target building: {2} Distance to target position: {3}",
                            citizenInstanceId,
                            citizenManager.m_instances.m_buffer[citizenInstanceId].m_sourceBuilding,
                            citizenManager.m_instances.m_buffer[citizenInstanceId].m_targetBuilding,
                            magnitude);
                    }
#endif
                }
            }

            Log.Info("UtilityManager.RemoveStuckEntities(): Resetting vehicles that are waiting for a path.");
            VehicleManager vehicleManager = Singleton<VehicleManager>.instance;

            for (uint vehicleId = 1; vehicleId < Constants.ServiceFactory.VehicleService.MaxVehicleCount; ++vehicleId) {
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

            for (uint vehicleId = 1; vehicleId < Constants.ServiceFactory.VehicleService.MaxVehicleCount;
                 ++vehicleId) {
                // Log._Debug($"UtilityManager.RemoveStuckEntities(): Processing vehicle {vehicleId}.");
                if ((vehicleManager.m_vehicles.m_buffer[vehicleId].m_flags
                     & Vehicle.Flags.Parking) != 0)
                {
                    ushort driverInstanceId = Constants.ManagerFactory.ExtVehicleManager.GetDriverInstanceId(
                        (ushort)vehicleId, ref vehicleManager.m_vehicles.m_buffer[vehicleId]);
                    uint citizenId = citizenManager.m_instances.m_buffer[driverInstanceId].m_citizen;

                    if (citizenId != 0u && citizenManager.m_citizens.m_buffer[citizenId].m_parkedVehicle == 0)
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
    }
}