namespace TrafficManager.Patch._CitizenAI._HumanAI {
    using System.Reflection;
    using API.Manager;
    using API.Traffic.Data;
    using API.Traffic.Enums;
    using ColossalFramework;
    using Connection;
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
        private delegate void TargetDelegate(ushort instanceID,
                                             ref CitizenInstance data,
                                             Vector3 physicsLodRefPos);

        [UsedImplicitly]
        public static MethodBase TargetMethod() => TranspilerUtil.DeclaredMethod<TargetDelegate>(typeof(HumanAI), nameof(HumanAI.SimulationStep));

        private static SpawnDelegate SpawnCitizenAI;
        private static StartPathFindDelegate StartPathFindCitizenAI;
        private static SimulationStepDelegate SimulationStepCitizenAI;
        private static ArriveAtDestinationDelegate ArriveAtDestination;
        private static InvalidPathHumanAIDelegate InvalidPath;
        private static PathfindFailureHumanAIDelegate PathfindFailure;
        private static PathfindSuccessHumanAIDelegate PathfindSuccess;

        [UsedImplicitly]
        public static void Prepare() {
            SpawnCitizenAI = GameConnectionManager.Instance.HumanAIConnection.SpawnCitizenAI;
            StartPathFindCitizenAI = GameConnectionManager.Instance.HumanAIConnection.StartPathFindCitizenAI;
            SimulationStepCitizenAI = GameConnectionManager.Instance.HumanAIConnection.SimulationStepCitizenAI;
            ArriveAtDestination = GameConnectionManager.Instance.HumanAIConnection.ArriveAtDestination;
            InvalidPath = GameConnectionManager.Instance.HumanAIConnection.InvalidPath;
            PathfindFailure = GameConnectionManager.Instance.HumanAIConnection.PathfindFailure;
            PathfindSuccess = GameConnectionManager.Instance.HumanAIConnection.PathfindSuccess;
        }

        [UsedImplicitly]
        public static bool Prefix(HumanAI __instance,
                                  ushort instanceID,
                                  ref CitizenInstance data,
                                  Vector3 physicsLodRefPos) {
#if DEBUG
            bool citizenDebug = (DebugSettings.CitizenInstanceId == 0
                            || DebugSettings.CitizenInstanceId == instanceID)
                           && (DebugSettings.CitizenId == 0
                               || DebugSettings.CitizenId == data.m_citizen)
                           && (DebugSettings.SourceBuildingId == 0
                               || DebugSettings.SourceBuildingId == data.m_sourceBuilding)
                           && (DebugSettings.TargetBuildingId == 0
                               || DebugSettings.TargetBuildingId == data.m_targetBuilding);
            bool logParkingAi = DebugSwitch.BasicParkingAILog.Get() && citizenDebug;
#else
            var logParkingAi = false;
#endif
            CitizenManager citizenManager = Singleton<CitizenManager>.instance;
            Citizen[] citizensBuf = citizenManager.m_citizens.m_buffer;
            uint citizenId = data.m_citizen;
            ref Citizen citizen = ref citizensBuf[data.m_citizen];

            if ((data.m_flags & (CitizenInstance.Flags.Blown
                                         | CitizenInstance.Flags.Floating)) != CitizenInstance.Flags.None
                && (data.m_flags & CitizenInstance.Flags.Character) == CitizenInstance.Flags.None)
            {
                citizenManager.ReleaseCitizenInstance(instanceID);
                if (citizenId != 0u) {
                    citizenManager.ReleaseCitizen(citizenId);
                }

                return false;
            }

            if ((data.m_flags & CitizenInstance.Flags.WaitingPath) != CitizenInstance.Flags.None) {
                PathManager pathManager = Singleton<PathManager>.instance;
                byte pathFindFlags = pathManager.m_pathUnits.m_buffer[data.m_path].m_pathFindFlags;

                // NON-STOCK CODE START
                ExtPathState mainPathState = ExtPathState.Calculating;
                if ((pathFindFlags & PathUnit.FLAG_FAILED) != 0 || data.m_path == 0) {
                    mainPathState = ExtPathState.Failed;
                } else if ((pathFindFlags & PathUnit.FLAG_READY) != 0) {
                    mainPathState = ExtPathState.Ready;
                }

                if (logParkingAi) {
                    Log._Debug(
                        $"CustomHumanAI.CustomSimulationStep({instanceID}): " +
                        $"Path: {data.m_path}, mainPathState={mainPathState}");
                }

                ExtSoftPathState finalPathState = ExtCitizenInstance.ConvertPathStateToSoftPathState(mainPathState);

                if (SavedGameOptions.Instance.parkingAI) {
                    finalPathState = AdvancedParkingManager.Instance.UpdateCitizenPathState(
                        instanceID,
                        ref data,
                        ref ExtCitizenInstanceManager
                            .Instance.ExtInstances[
                                instanceID],
                        ref ExtCitizenManager
                            .Instance.ExtCitizens[
                                citizenId],
                        ref citizen,
                        mainPathState);
                    if (logParkingAi) {
                        Log._Debug(
                            $"CustomHumanAI.CustomSimulationStep({instanceID}): " +
                            $"Applied Parking AI logic. Path: {data.m_path}, " +
                            $"mainPathState={mainPathState}, finalPathState={finalPathState}, " +
                            $"extCitizenInstance={ExtCitizenInstanceManager.Instance.ExtInstances[instanceID]}");
                    }
                } // if SavedGameOptions.Instance.parkingAi

                switch (finalPathState) {
                    case ExtSoftPathState.Ready: {
                        if (logParkingAi) {
                            Log._Debug(
                                $"CustomHumanAI.CustomSimulationStep({instanceID}): Path-finding " +
                                $"succeeded for citizen instance {instanceID} " +
                                $"(finalPathState={finalPathState}). Path: {data.m_path} " +
                                "-- calling HumanAI.PathfindSuccess");
                        }

                        if (citizenId == 0 || citizen.m_vehicle == 0) {
                            SpawnCitizenAI(__instance, instanceID, ref data);
                        }

                        data.m_pathPositionIndex = 255;
                        data.m_flags &= ~CitizenInstance.Flags.WaitingPath;
                        data.m_flags &= ~(CitizenInstance.Flags.HangAround
                                                  | CitizenInstance.Flags.Panicking
                                                  | CitizenInstance.Flags.SittingDown
                                                  | CitizenInstance.Flags.Cheering);

                        PathfindSuccess(__instance, instanceID, ref data);
                        break;
                    }

                    case ExtSoftPathState.Ignore: {
                        if (logParkingAi) {
                            Log._Debug(
                                $"CustomHumanAI.CustomSimulationStep({instanceID}): " +
                                "Path-finding result shall be ignored for citizen instance " +
                                $"{instanceID} (finalPathState={finalPathState}). " +
                                $"Path: {data.m_path} -- ignoring");
                        }

                        return false;
                    }

                    case ExtSoftPathState.Calculating:
                    default: {
                        if (logParkingAi) {
                            Log._Debug(
                                $"CustomHumanAI.CustomSimulationStep({instanceID}): " +
                                      $"Path-finding result undetermined for citizen instance {instanceID} " +
                                      $"(finalPathState={finalPathState}). " +
                                      $"Path: {data.m_path} -- continue");
                        }

                        break;
                    }

                    case ExtSoftPathState.FailedHard: {
                        if (logParkingAi) {
                            Log._Debug(
                                $"CustomHumanAI.CustomSimulationStep({instanceID}): " +
                                $"HARD path-finding failure for citizen instance {instanceID} " +
                                $"(finalPathState={finalPathState}). Path: {data.m_path} " +
                                "-- calling HumanAI.PathfindFailure");
                        }

                        data.m_flags &= ~CitizenInstance.Flags.WaitingPath;
                        data.m_flags &= ~(CitizenInstance.Flags.HangAround
                                                  | CitizenInstance.Flags.Panicking
                                                  | CitizenInstance.Flags.SittingDown
                                                  | CitizenInstance.Flags.Cheering);
                        Singleton<PathManager>.instance.ReleasePath(data.m_path);
                        data.m_path = 0u;
                        PathfindFailure(__instance, instanceID, ref data);
                        return false;
                    }

                    case ExtSoftPathState.FailedSoft: {
                        if (logParkingAi) {
                            Log._Debug(
                                $"CustomHumanAI.CustomSimulationStep({instanceID}): " +
                                $"SOFT path-finding failure for citizen instance {instanceID} " +
                                $"(finalPathState={finalPathState}). Path: {data.m_path} " +
                                "-- calling HumanAI.InvalidPath");
                        }

                        // path mode has been updated, repeat path-finding
                        data.m_flags &= ~CitizenInstance.Flags.WaitingPath;
                        data.m_flags &= ~(CitizenInstance.Flags.HangAround
                                                  | CitizenInstance.Flags.Panicking
                                                  | CitizenInstance.Flags.SittingDown
                                                  | CitizenInstance.Flags.Cheering);
                        InvalidPath(__instance, instanceID, ref data);
                        break;
                    }
                }

                // NON-STOCK CODE END
            }

            // NON-STOCK CODE START
            if (SavedGameOptions.Instance.parkingAI) {
                if (ExtSimulationStep(__instance,
                                      ref citizen,
                                      instanceID,
                                      ref data,
                                      ref ExtCitizenInstanceManager.Instance.ExtInstances[instanceID],
                                      physicsLodRefPos)) {
                    return false;
                }
            }

            // NON-STOCK CODE END
            SimulationStepCitizenAI(__instance, instanceID, ref data, physicsLodRefPos);

            ushort vehicleId = 0;
            if (data.m_citizen != 0u) {
                vehicleId = citizensBuf[data.m_citizen].m_vehicle;
            }

            if (vehicleId != 0) {
                ref Vehicle vehicle = ref vehicleId.ToVehicle();
                VehicleInfo vehicleInfo = vehicle.Info;

                if (vehicleInfo.m_vehicleType == VehicleInfo.VehicleType.Bicycle) {
                    vehicleInfo.m_vehicleAI.SimulationStep(
                        vehicleId,
                        ref vehicle,
                        vehicleId,
                        ref vehicle,
                        0);
                    vehicleId = 0;
                }
            }

            if (vehicleId != 0
                || (data.m_flags & (CitizenInstance.Flags.Character
                                            | CitizenInstance.Flags.WaitingPath
                                            | CitizenInstance.Flags.Blown
                                            | CitizenInstance.Flags.Floating)) !=
                CitizenInstance.Flags.None) {
                return false;
            }

            data.m_flags &= ~(CitizenInstance.Flags.HangAround
                                      | CitizenInstance.Flags.Panicking
                                      | CitizenInstance.Flags.SittingDown
                                      | CitizenInstance.Flags.Cheering);
            ArriveAtDestination(__instance, instanceID, ref data, false);
            citizenManager.ReleaseCitizenInstance(instanceID);

            return false;
        }

        private static bool ExtSimulationStep(HumanAI instance,
                                              ref Citizen citizen,
                                              ushort instanceID,
                                              ref CitizenInstance data,
                                              ref ExtCitizenInstance extInstance,
                                              Vector3 physicsLodRefPos) {
            IExtCitizenInstanceManager extCitInstMan =
                Constants.ManagerFactory.ExtCitizenInstanceManager;
#if DEBUG
            bool citizenDebug
                = (DebugSettings.CitizenInstanceId == 0
                   || DebugSettings.CitizenInstanceId == instanceID)
                  && (DebugSettings.CitizenId == 0
                      || DebugSettings.CitizenId == data.m_citizen)
                  && (DebugSettings.SourceBuildingId == 0
                      || DebugSettings.SourceBuildingId == data.m_sourceBuilding)
                  && (DebugSettings.TargetBuildingId == 0
                      || DebugSettings.TargetBuildingId == data.m_targetBuilding);
            bool logParkingAi = DebugSwitch.BasicParkingAILog.Get() && citizenDebug;
            bool extendedLogParkingAi = DebugSwitch.ExtendedParkingAILog.Get() && citizenDebug;
#else
            var logParkingAi = false;
            var extendedLogParkingAi = false;
#endif

#if DEBUG
            ExtPathMode logPathMode = extInstance.pathMode;
#else
            var logPathMode = 0;
#endif
            switch (extInstance.pathMode) {
                // check if the citizen has reached a parked car or target
                case ExtPathMode.WalkingToParkedCar:
                case ExtPathMode.ApproachingParkedCar: {
                    ushort parkedVehicleId = citizen.m_parkedVehicle;

                    if (parkedVehicleId == 0) {
                        // citizen is reaching their parked car but does not own a parked car
                        Log._DebugOnlyWarningIf(
                            logParkingAi,
                            () => $"CustomHumanAI.ExtSimulationStep({instanceID}): " +
                                  $"Citizen instance {instanceID} was walking to / reaching " +
                                  $"their parked car ({logPathMode}) but parked " +
                                  "car has disappeared. RESET.");

                        extCitInstMan.Reset(ref extInstance);
                        data.m_flags &= ~CitizenInstance.Flags.WaitingPath;
                        data.m_flags &= ~(CitizenInstance.Flags.HangAround
                                                  | CitizenInstance.Flags.Panicking
                                                  | CitizenInstance.Flags.SittingDown
                                                  | CitizenInstance.Flags.Cheering);
                        InvalidPath(instance, instanceID, ref data);
                        return true;
                    }

                    ParkedCarApproachState approachState =
                        AdvancedParkingManager.Instance.CitizenApproachingParkedCarSimulationStep(
                            instanceID,
                            ref data,
                            ref extInstance,
                            physicsLodRefPos,
                            ref parkedVehicleId.ToParkedVehicle());

                    switch (approachState) {
                        case ParkedCarApproachState.None:
                        default:
                            break;
                        case ParkedCarApproachState.Approaching:
                            // citizen approaches their parked car
                            return true;
                        case ParkedCarApproachState.Approached: {
                            // citizen reached their parked car
                            Log._DebugIf(
                                extendedLogParkingAi,
                                () => $"CustomHumanAI.CustomSimulationStep({instanceID}): " +
                                      $"Citizen instance {instanceID} arrived at parked car. " +
                                      $"PathMode={logPathMode}");

                            if (data.m_path != 0) {
                                Singleton<PathManager>.instance.ReleasePath(data.m_path);
                                data.m_path = 0;
                            }

                            data.m_flags &= CitizenInstance.Flags.Created
                                                    | CitizenInstance.Flags.Cheering
                                                    | CitizenInstance.Flags.Deleted
                                                    | CitizenInstance.Flags.Underground
                                                    | CitizenInstance.Flags.CustomName
                                                    | CitizenInstance.Flags.Character
                                                    | CitizenInstance.Flags.BorrowCar
                                                    | CitizenInstance.Flags.HangAround
                                                    | CitizenInstance.Flags.InsideBuilding
                                                    | CitizenInstance.Flags.WaitingPath
                                                    | CitizenInstance.Flags.TryingSpawnVehicle
                                                    | CitizenInstance.Flags.CannotUseTransport
                                                    | CitizenInstance.Flags.Panicking
                                                    | CitizenInstance.Flags.OnPath
                                                    | CitizenInstance.Flags.SittingDown
                                                    | CitizenInstance.Flags.AtTarget
                                                    | CitizenInstance.Flags.RequireSlowStart
                                                    | CitizenInstance.Flags.Transition
                                                    | CitizenInstance.Flags.RidingBicycle
                                                    | CitizenInstance.Flags.OnBikeLane
                                                    | CitizenInstance.Flags.CannotUseTaxi
                                                    | CitizenInstance.Flags.CustomColor
                                                    | CitizenInstance.Flags.Blown
                                                    | CitizenInstance.Flags.Floating
                                                    | CitizenInstance.Flags.TargetIsNode
                                                    | CitizenInstance.Flags.TargetFlags;

                            if (StartPathFindCitizenAI(instance, instanceID, ref data)) {
                                return true;
                            } else {
                                data.Unspawn(instanceID);
                                extCitInstMan.Reset(ref extInstance);
                                return true;
                            }
                        }

                        case ParkedCarApproachState.Failure: {
                            Log._DebugIf(
                                logParkingAi,
                                () => $"CustomHumanAI.ExtSimulationStep({instanceID}): " +
                                      $"Citizen instance {instanceID} failed to arrive at " +
                                      $"parked car. PathMode={logPathMode}");

                            // repeat path-finding
                            data.m_flags &= ~CitizenInstance.Flags.WaitingPath;
                            data.m_flags &= ~(CitizenInstance.Flags.HangAround
                                                      | CitizenInstance.Flags.Panicking
                                                      | CitizenInstance.Flags.SittingDown
                                                      | CitizenInstance.Flags.Cheering);
                            InvalidPath(instance, instanceID, ref data);
                            return true;
                        }
                    }

                    break;
                }

                case ExtPathMode.WalkingToTarget:
                case ExtPathMode.TaxiToTarget: {
                    AdvancedParkingManager.Instance.CitizenApproachingTargetSimulationStep(
                        instanceID,
                        ref data,
                        ref extInstance);
                    break;
                }
            }

            return false;
        }
    }
}