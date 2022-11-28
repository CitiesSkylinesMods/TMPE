namespace TrafficManager.Patch._VehicleAI._CarAI {
    using System;
    using System.Reflection;
    using API.Manager;
    using API.Traffic.Data;
    using API.Traffic.Enums;
    using ColossalFramework;
    using CSUtil.Commons;
    using HarmonyLib;
    using JetBrains.Annotations;
    using Manager.Impl;
    using State;
    using State.ConfigData;
    using UnityEngine;
    using Util;
    using Util.Extensions;

    [UsedImplicitly]
    [HarmonyPatch]
    public class SimulationStepPatch {
        private delegate void SimulationStepTargetDelegate(ushort vehicleID, ref Vehicle data, Vector3 physicsLodRefPos);


        [UsedImplicitly]
        public static MethodBase TargetMethod() => TranspilerUtil.DeclaredMethod<SimulationStepTargetDelegate>(typeof(CarAI), "SimulationStep");

        [UsedImplicitly]
        public static bool Prefix(CarAI __instance,
                                  ushort vehicleID,
                                  ref Vehicle data,
                                  Vector3 physicsLodRefPos) {
            #if DEBUG
            bool vehDebug = DebugSettings.VehicleId == 0
                           || DebugSettings.VehicleId == vehicleID;
            bool logParkingAi = DebugSwitch.BasicParkingAILog.Get() && vehDebug;
#else
            var logParkingAi = false;
#endif

            if ((data.m_flags & Vehicle.Flags.WaitingPath) != 0) {
                PathManager pathManager = Singleton<PathManager>.instance;
                byte pathFindFlags = pathManager.m_pathUnits.m_buffer[data.m_path].m_pathFindFlags;

                // NON-STOCK CODE START
                ExtPathState mainPathState = ExtPathState.Calculating;
                if ((pathFindFlags & PathUnit.FLAG_FAILED) != 0 || data.m_path == 0) {
                    mainPathState = ExtPathState.Failed;
                } else if ((pathFindFlags & PathUnit.FLAG_READY) != 0) {
                    mainPathState = ExtPathState.Ready;
                }

#if DEBUG
                uint logVehiclePath = data.m_path;
                Log._DebugIf(
                    logParkingAi,
                    () => $"CustomCarAI.CustomSimulationStep({vehicleID}): " +
                    $"Path: {logVehiclePath}, mainPathState={mainPathState}");
#endif

                IExtVehicleManager extVehicleManager = Constants.ManagerFactory.ExtVehicleManager;
                ExtSoftPathState finalPathState = ExtCitizenInstance.ConvertPathStateToSoftPathState(mainPathState);
                if (SavedGameOptions.Instance.parkingAI
                    && extVehicleManager.ExtVehicles[vehicleID].vehicleType == ExtVehicleType.PassengerCar)
                {
                    ushort driverInstanceId = extVehicleManager.GetDriverInstanceId(vehicleID, ref data);
                    finalPathState = AdvancedParkingManager.Instance.UpdateCarPathState(
                        vehicleID,
                        ref data,
                        ref CitizenManager.instance.m_instances.m_buffer[driverInstanceId],
                        ref ExtCitizenInstanceManager.Instance.ExtInstances[driverInstanceId],
                        mainPathState);

#if DEBUG
                    if (logParkingAi) {
                        Log._Debug($"CustomCarAI.CustomSimulationStep({vehicleID}): " +
                                   $"Applied Parking AI logic. Path: {data.m_path}, " +
                                   $"mainPathState={mainPathState}, finalPathState={finalPathState}");
                    }
#endif
                }

                switch (finalPathState) {
                    case ExtSoftPathState.Ready: {
#if DEBUG
                        if (logParkingAi) {
                            Log._Debug(
                                $"CustomCarAI.CustomSimulationStep({vehicleID}): Path-finding " +
                                $"succeeded for vehicle {vehicleID} (finalPathState={finalPathState}). " +
                                $"Path: {data.m_path} -- calling CarAI.PathfindSuccess");
                        }
#endif

                        data.m_pathPositionIndex = 255;
                        data.m_flags &= ~Vehicle.Flags.WaitingPath;
                        data.m_flags &= ~Vehicle.Flags.Arriving;
                        GameConnectionManager.Instance.VehicleAIConnection.PathfindSuccess(__instance, vehicleID, ref data);
                        __instance.TrySpawn(vehicleID, ref data);
                        break;
                    }

                    case ExtSoftPathState.Ignore: {
#if DEBUG
                        if (logParkingAi) {
                            Log._Debug(
                                $"CustomCarAI.CustomSimulationStep({vehicleID}): Path-finding " +
                                $"result shall be ignored for vehicle {vehicleID} " +
                                $"(finalPathState={finalPathState}). Path: {data.m_path} -- ignoring");
                        }
#endif
                        return false;
                    }

                    case ExtSoftPathState.Calculating:
                    default: {
#if DEBUG
                        if (logParkingAi) {
                            Log._Debug(
                                $"CustomCarAI.CustomSimulationStep({vehicleID}): Path-finding " +
                                $"result undetermined for vehicle {vehicleID} (finalPathState={finalPathState}). " +
                                $"Path: {data.m_path} -- continue");
                        }
#endif
                        break;
                    }

                    case ExtSoftPathState.FailedHard: {
#if DEBUG
                        if (logParkingAi) {
                            Log._Debug(
                                $"CustomCarAI.CustomSimulationStep({vehicleID}): HARD path-finding " +
                                $"failure for vehicle {vehicleID} (finalPathState={finalPathState}). " +
                                $"Path: {data.m_path} -- calling CarAI.PathfindFailure");
                        }
#endif
                        data.m_flags &= ~Vehicle.Flags.WaitingPath;
                        Singleton<PathManager>.instance.ReleasePath(data.m_path);
                        data.m_path = 0u;
                        GameConnectionManager.Instance.VehicleAIConnection.PathfindFailure(__instance, vehicleID, ref data);
                        return false;
                    }

                    case ExtSoftPathState.FailedSoft: {
#if DEBUG
                        if (logParkingAi) {
                            Log._Debug(
                                $"CustomCarAI.CustomSimulationStep({vehicleID}): SOFT path-finding " +
                                $"failure for vehicle {vehicleID} (finalPathState={finalPathState}). " +
                                $"Path: {data.m_path} -- calling CarAI.InvalidPath");
                        }
#endif

                        // path mode has been updated, repeat path-finding
                        data.m_flags &= ~Vehicle.Flags.WaitingPath;
                        GameConnectionManager.Instance.VehicleAIConnection.InvalidPath(__instance, vehicleID, ref data, vehicleID, ref data);
                        break;
                    }
                }

                // NON-STOCK CODE END
            } else {
                if ((data.m_flags & Vehicle.Flags.WaitingSpace) != 0) {
                    __instance.TrySpawn(vehicleID, ref data);
                }
            }

            // NON-STOCK CODE START
            IExtVehicleManager extVehicleMan = Constants.ManagerFactory.ExtVehicleManager;
            extVehicleMan.UpdateVehiclePosition(vehicleID, ref data);

            if (SavedGameOptions.Instance.advancedAI
                && (data.m_flags & Vehicle.Flags.Spawned) != 0)
            {
                extVehicleMan.LogTraffic(vehicleID, ref data);
            }

            // NON-STOCK CODE END
            Vector3 lastFramePosition = data.GetLastFramePosition();
            int lodPhysics;
            if (Vector3.SqrMagnitude(physicsLodRefPos - lastFramePosition) >= 1100f * 1100f) {
                lodPhysics = 2;
            } else if (Vector3.SqrMagnitude(
                           Singleton<SimulationManager>.instance.m_simulationView.m_position -
                           lastFramePosition) >= 500f * 500f) {
                lodPhysics = 1;
            } else {
                lodPhysics = 0;
            }

            __instance.SimulationStep(vehicleID, ref data, vehicleID, ref data, lodPhysics);
            if (data.m_leadingVehicle == 0 && data.m_trailingVehicle != 0) {
                ushort trailerId = data.m_trailingVehicle;
                int numIters = 0;
                while (trailerId != 0) {
                    ref Vehicle trailer = ref trailerId.ToVehicle();

                    trailer.Info.m_vehicleAI.SimulationStep(
                        trailerId,
                        ref trailer,
                        vehicleID,
                        ref data,
                        lodPhysics);

                    trailerId = trailer.m_trailingVehicle;
                    if (++numIters > 16384) {
                        CODebugBase<LogChannel>.Error(
                            LogChannel.Core,
                            $"Invalid list detected!\n{Environment.StackTrace}");
                        break;
                    }
                }
            }

            int privateServiceIndex = ItemClass.GetPrivateServiceIndex(__instance.m_info.m_class.m_service);
            int maxBlockCounter = (privateServiceIndex == -1) ? 150 : 100;

            if ((data.m_flags & (Vehicle.Flags.Spawned
                                        | Vehicle.Flags.WaitingPath
                                        | Vehicle.Flags.WaitingSpace)) == 0
                && data.m_cargoParent == 0) {
                Singleton<VehicleManager>.instance.ReleaseVehicle(vehicleID);
            } else if (data.m_blockCounter >= maxBlockCounter) {
                // NON-STOCK CODE START
                if (VehicleBehaviorManager.Instance.MayDespawn(vehicleID, ref data)) {
                    // NON-STOCK CODE END
                    Singleton<VehicleManager>.instance.ReleaseVehicle(vehicleID);
                } // NON-STOCK CODE
            }

            return false;
        }
    }
}