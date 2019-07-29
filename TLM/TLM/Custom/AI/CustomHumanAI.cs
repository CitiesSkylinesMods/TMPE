namespace TrafficManager.Custom.AI {
    using System.Runtime.CompilerServices;
    using API.Manager;
    using API.Traffic.Data;
    using API.Traffic.Enums;
    using API.TrafficLight;
    using ColossalFramework;
    using CSUtil.Commons;
    using CSUtil.Commons.Benchmark;
    using JetBrains.Annotations;
    using Manager;
    using Manager.Impl;
    using RedirectionFramework.Attributes;
    using State;
    using State.ConfigData;
    using Traffic.Data;
    using UnityEngine;

    [TargetType(typeof(HumanAI))]
    public class CustomHumanAI : CitizenAI {
        [RedirectMethod]
        public void CustomSimulationStep(ushort instanceId,
                                         ref CitizenInstance instanceData,
                                         Vector3 physicsLodRefPos) {
#if DEBUG
            bool citizenDebug = (DebugSettings.CitizenInstanceId == 0
                            || DebugSettings.CitizenInstanceId == instanceId)
                           && (DebugSettings.CitizenId == 0
                               || DebugSettings.CitizenId == instanceData.m_citizen)
                           && (DebugSettings.SourceBuildingId == 0
                               || DebugSettings.SourceBuildingId == instanceData.m_sourceBuilding)
                           && (DebugSettings.TargetBuildingId == 0
                               || DebugSettings.TargetBuildingId == instanceData.m_targetBuilding);
            bool logParkingAi = DebugSwitch.BasicParkingAILog.Get() && citizenDebug;
#else
            var logParkingAi = false;
#endif
            CitizenManager citizenManager = Singleton<CitizenManager>.instance;
            uint citizenId = instanceData.m_citizen;

            if ((instanceData.m_flags & (CitizenInstance.Flags.Blown
                                         | CitizenInstance.Flags.Floating)) != CitizenInstance.Flags.None
                && (instanceData.m_flags & CitizenInstance.Flags.Character) == CitizenInstance.Flags.None)
            {
                citizenManager.ReleaseCitizenInstance(instanceId);
                if (citizenId != 0u) {
                    citizenManager.ReleaseCitizen(citizenId);
                }

                return;
            }

            Citizen[] citizensBuffer = citizenManager.m_citizens.m_buffer;
            if ((instanceData.m_flags & CitizenInstance.Flags.WaitingPath) != CitizenInstance.Flags.None) {
                PathManager pathManager = Singleton<PathManager>.instance;
                byte pathFindFlags = pathManager.m_pathUnits.m_buffer[instanceData.m_path].m_pathFindFlags;

                // NON-STOCK CODE START
                ExtPathState mainPathState = ExtPathState.Calculating;
                if ((pathFindFlags & PathUnit.FLAG_FAILED) != 0 || instanceData.m_path == 0) {
                    mainPathState = ExtPathState.Failed;
                } else if ((pathFindFlags & PathUnit.FLAG_READY) != 0) {
                    mainPathState = ExtPathState.Ready;
                }

                if (logParkingAi) {
                    Log._Debug(
                        $"CustomHumanAI.CustomSimulationStep({instanceId}): " +
                        $"Path: {instanceData.m_path}, mainPathState={mainPathState}");
                }

                ExtSoftPathState finalPathState;
                using (var bm = Benchmark.MaybeCreateBenchmark(
                    null,
                    "ConvertPathStateToSoftPathState+UpdateCitizenPathState"))
                {
                    finalPathState = ExtCitizenInstance.ConvertPathStateToSoftPathState(mainPathState);

                    if (Options.parkingAI) {
                        finalPathState = AdvancedParkingManager.Instance.UpdateCitizenPathState(
                            instanceId,
                            ref instanceData,
                            ref ExtCitizenInstanceManager
                                .Instance.ExtInstances[
                                    instanceId],
                            ref ExtCitizenManager
                                .Instance.ExtCitizens[
                                    citizenId],
                            ref citizensBuffer[
                                instanceData.m_citizen],
                            mainPathState);
                        if (logParkingAi) {
                            Log._Debug(
                                $"CustomHumanAI.CustomSimulationStep({instanceId}): " +
                                $"Applied Parking AI logic. Path: {instanceData.m_path}, " +
                                $"mainPathState={mainPathState}, finalPathState={finalPathState}, " +
                                $"extCitizenInstance={ExtCitizenInstanceManager.Instance.ExtInstances[instanceId]}");
                        }
                    } // if Options.parkingAi
                }

                switch (finalPathState) {
                    case ExtSoftPathState.Ready: {
                        if (logParkingAi) {
                            Log._Debug(
                                $"CustomHumanAI.CustomSimulationStep({instanceId}): Path-finding " +
                                $"succeeded for citizen instance {instanceId} " +
                                $"(finalPathState={finalPathState}). Path: {instanceData.m_path} " +
                                "-- calling HumanAI.PathfindSuccess");
                        }

                        if (citizenId == 0
                            || citizensBuffer[instanceData.m_citizen].m_vehicle == 0) {
                            Spawn(instanceId, ref instanceData);
                        }

                        instanceData.m_pathPositionIndex = 255;
                        instanceData.m_flags &= ~CitizenInstance.Flags.WaitingPath;
                        instanceData.m_flags &= ~(CitizenInstance.Flags.HangAround
                                                  | CitizenInstance.Flags.Panicking
                                                  | CitizenInstance.Flags.SittingDown
                                                  | CitizenInstance.Flags.Cheering);

                        // NON-STOCK CODE START (transferred from ResidentAI.PathfindSuccess)
                        const Citizen.Flags CTZ_MASK = Citizen.Flags.Tourist
                                                       | Citizen.Flags.MovingIn
                                                       | Citizen.Flags.DummyTraffic;
                        if (citizenId != 0
                            && (citizensBuffer[citizenId].m_flags & CTZ_MASK) == Citizen.Flags.MovingIn)
                        {
                            StatisticBase statisticBase = Singleton<StatisticsManager>
                                                .instance.Acquire<StatisticInt32>(StatisticType.MoveRate);
                            statisticBase.Add(1);
                        }

                        // NON-STOCK CODE END
                        PathfindSuccess(instanceId, ref instanceData);
                        break;
                    }

                    case ExtSoftPathState.Ignore: {
                        if (logParkingAi) {
                            Log._Debug(
                                $"CustomHumanAI.CustomSimulationStep({instanceId}): " +
                                "Path-finding result shall be ignored for citizen instance " +
                                $"{instanceId} (finalPathState={finalPathState}). " +
                                $"Path: {instanceData.m_path} -- ignoring");
                        }

                        return;
                    }

                    case ExtSoftPathState.Calculating:
                    default: {
                        if (logParkingAi) {
                            Log._Debug(
                                $"CustomHumanAI.CustomSimulationStep({instanceId}): " +
                                      $"Path-finding result undetermined for citizen instance {instanceId} " +
                                      $"(finalPathState={finalPathState}). " +
                                      $"Path: {instanceData.m_path} -- continue");
                        }

                        break;
                    }

                    case ExtSoftPathState.FailedHard: {
                        if (logParkingAi) {
                            Log._Debug(
                                $"CustomHumanAI.CustomSimulationStep({instanceId}): " +
                                $"HARD path-finding failure for citizen instance {instanceId} " +
                                $"(finalPathState={finalPathState}). Path: {instanceData.m_path} " +
                                "-- calling HumanAI.PathfindFailure");
                        }

                        instanceData.m_flags &= ~CitizenInstance.Flags.WaitingPath;
                        instanceData.m_flags &= ~(CitizenInstance.Flags.HangAround
                                                  | CitizenInstance.Flags.Panicking
                                                  | CitizenInstance.Flags.SittingDown
                                                  | CitizenInstance.Flags.Cheering);
                        Singleton<PathManager>.instance.ReleasePath(instanceData.m_path);
                        instanceData.m_path = 0u;
                        PathfindFailure(instanceId, ref instanceData);
                        return;
                    }

                    case ExtSoftPathState.FailedSoft: {
                        if (logParkingAi) {
                            Log._Debug(
                                $"CustomHumanAI.CustomSimulationStep({instanceId}): " +
                                $"SOFT path-finding failure for citizen instance {instanceId} " +
                                $"(finalPathState={finalPathState}). Path: {instanceData.m_path} " +
                                "-- calling HumanAI.InvalidPath");
                        }

                        // path mode has been updated, repeat path-finding
                        instanceData.m_flags &= ~CitizenInstance.Flags.WaitingPath;
                        instanceData.m_flags &= ~(CitizenInstance.Flags.HangAround
                                                  | CitizenInstance.Flags.Panicking
                                                  | CitizenInstance.Flags.SittingDown
                                                  | CitizenInstance.Flags.Cheering);
                        InvalidPath(instanceId, ref instanceData);
                        break;
                    }
                }

                // NON-STOCK CODE END
            }

            // NON-STOCK CODE START
            using (var bm = Benchmark.MaybeCreateBenchmark(null, "ExtSimulationStep")) {
                if (Options.parkingAI) {
                    if (ExtSimulationStep(
                        instanceId,
                        ref instanceData,
                        ref ExtCitizenInstanceManager.Instance.ExtInstances[instanceId],
                        physicsLodRefPos)) {
                        return;
                    }
                }
            }

            // NON-STOCK CODE END
            base.SimulationStep(instanceId, ref instanceData, physicsLodRefPos);

            VehicleManager vehicleManager = Singleton<VehicleManager>.instance;
            ushort vehicleId = 0;
            if (instanceData.m_citizen != 0u) {
                vehicleId = citizensBuffer[instanceData.m_citizen].m_vehicle;
            }

            if (vehicleId != 0) {
                Vehicle[] vehiclesBuffer = vehicleManager.m_vehicles.m_buffer;
                VehicleInfo vehicleInfo = vehiclesBuffer[vehicleId].Info;

                if (vehicleInfo.m_vehicleType == VehicleInfo.VehicleType.Bicycle) {
                    vehicleInfo.m_vehicleAI.SimulationStep(
                        vehicleId,
                        ref vehiclesBuffer[vehicleId],
                        vehicleId,
                        ref vehiclesBuffer[vehicleId],
                        0);
                    vehicleId = 0;
                }
            }

            if (vehicleId != 0
                || (instanceData.m_flags & (CitizenInstance.Flags.Character
                                            | CitizenInstance.Flags.WaitingPath
                                            | CitizenInstance.Flags.Blown
                                            | CitizenInstance.Flags.Floating)) !=
                CitizenInstance.Flags.None) {
                return;
            }

            instanceData.m_flags &= ~(CitizenInstance.Flags.HangAround
                                      | CitizenInstance.Flags.Panicking
                                      | CitizenInstance.Flags.SittingDown);
            ArriveAtDestination(instanceId, ref instanceData, false);
            citizenManager.ReleaseCitizenInstance(instanceId);
        }

        public bool ExtSimulationStep(ushort instanceId,
                                      ref CitizenInstance instanceData,
                                      ref ExtCitizenInstance extInstance,
                                      Vector3 physicsLodRefPos) {
            IExtCitizenInstanceManager extCitInstMan = Constants.ManagerFactory.ExtCitizenInstanceManager;
#if DEBUG
            bool citizenDebug
                = (DebugSettings.CitizenInstanceId == 0
                   || DebugSettings.CitizenInstanceId == instanceId)
                  && (DebugSettings.CitizenId == 0
                      || DebugSettings.CitizenId == instanceData.m_citizen)
                  && (DebugSettings.SourceBuildingId == 0
                      || DebugSettings.SourceBuildingId == instanceData.m_sourceBuilding)
                  && (DebugSettings.TargetBuildingId == 0
                      || DebugSettings.TargetBuildingId == instanceData.m_targetBuilding);
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
                    Citizen[] citizensBuffer = Singleton<CitizenManager>.instance.m_citizens.m_buffer;
                    ushort parkedVehicleId = citizensBuffer[instanceData.m_citizen].m_parkedVehicle;

                    if (parkedVehicleId == 0) {
                        // citizen is reaching their parked car but does not own a parked car
                        Log._DebugOnlyWarningIf(
                            logParkingAi,
                            () => $"CustomHumanAI.ExtSimulationStep({instanceId}): " +
                            $"Citizen instance {instanceId} was walking to / reaching " +
                            $"their parked car ({logPathMode}) but parked " +
                            "car has disappeared. RESET.");

                        extCitInstMan.Reset(ref extInstance);
                        instanceData.m_flags &= ~CitizenInstance.Flags.WaitingPath;
                        instanceData.m_flags &= ~(CitizenInstance.Flags.HangAround
                                                  | CitizenInstance.Flags.Panicking
                                                  | CitizenInstance.Flags.SittingDown
                                                  | CitizenInstance.Flags.Cheering);
                        InvalidPath(instanceId, ref instanceData);
                        return true;
                    }

                    VehicleParked[] parkedVehicles = Singleton<VehicleManager>.instance.m_parkedVehicles.m_buffer;
                    ParkedCarApproachState approachState =
                        AdvancedParkingManager.Instance.CitizenApproachingParkedCarSimulationStep(
                            instanceId,
                            ref instanceData,
                            ref extInstance,
                            physicsLodRefPos,
                            ref parkedVehicles[parkedVehicleId]);

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
                                () => $"CustomHumanAI.CustomSimulationStep({instanceId}): " +
                                $"Citizen instance {instanceId} arrived at parked car. " +
                                $"PathMode={logPathMode}");

                            if (instanceData.m_path != 0) {
                                Singleton<PathManager>.instance.ReleasePath(instanceData.m_path);
                                instanceData.m_path = 0;
                            }

                            instanceData.m_flags &= CitizenInstance.Flags.Created
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
                                                    | CitizenInstance.Flags.TargetFlags;

                            if (StartPathFind(instanceId, ref instanceData)) {
                                return true;
                            } else {
                                instanceData.Unspawn(instanceId);
                                extCitInstMan.Reset(ref extInstance);
                                return true;
                            }
                        }

                        case ParkedCarApproachState.Failure: {
                            Log._DebugIf(
                                logParkingAi,
                                () => $"CustomHumanAI.ExtSimulationStep({instanceId}): " +
                                $"Citizen instance {instanceId} failed to arrive at " +
                                $"parked car. PathMode={logPathMode}");

                            // repeat path-finding
                            instanceData.m_flags &= ~CitizenInstance.Flags.WaitingPath;
                            instanceData.m_flags &= ~(CitizenInstance.Flags.HangAround
                                                      | CitizenInstance.Flags.Panicking
                                                      | CitizenInstance.Flags.SittingDown
                                                      | CitizenInstance.Flags.Cheering);
                            InvalidPath(instanceId, ref instanceData);
                            return true;
                        }
                    }

                    break;
                }

                case ExtPathMode.WalkingToTarget:
                case ExtPathMode.TaxiToTarget: {
                    AdvancedParkingManager.Instance.CitizenApproachingTargetSimulationStep(
                        instanceId,
                        ref instanceData,
                        ref extInstance);
                    break;
                }
            }

            return false;
        }

        [RedirectMethod]
        [UsedImplicitly]
        public bool CustomCheckTrafficLights(ushort nodeId, ushort segmentId) {
#if DEBUG
            bool logTimedLights = DebugSwitch.TimedTrafficLights.Get()
                                 && DebugSettings.NodeId == nodeId;
#endif
            NetManager netManager = Singleton<NetManager>.instance;
            uint currentFrameIndex = Singleton<SimulationManager>.instance.m_currentFrameIndex;
            uint simGroup = (uint)nodeId >> 7;
            uint stepWaitTime = currentFrameIndex - simGroup & 255u;

            // NON-STOCK CODE START
            bool customSim = Options.timedLightsEnabled &&
                            TrafficLightSimulationManager.Instance.HasActiveSimulation(nodeId);

            RoadBaseAI.TrafficLightState pedestrianLightState;
            NetSegment[] segmentsBuffer = netManager.m_segments.m_buffer;
            bool startNode = segmentsBuffer[segmentId].m_startNode == nodeId;

            ICustomSegmentLights lights = null;
            using (var bm = Benchmark.MaybeCreateBenchmark(null, "GetSegmentLights")) {
                if (customSim) {
                    lights = CustomSegmentLightsManager.Instance.GetSegmentLights(
                        segmentId,
                        startNode,
                        false);
                }
            }

            if (lights == null) {
                // NON-STOCK CODE END
#if DEBUG
                Log._DebugIf(
                    logTimedLights,
                    () => $"CustomHumanAI.CustomCheckTrafficLights({nodeId}, " +
                    $"{segmentId}): No custom simulation!");
#endif

                RoadBaseAI.GetTrafficLightState(
                    nodeId,
                    ref segmentsBuffer[segmentId],
                    currentFrameIndex - simGroup,
                    out RoadBaseAI.TrafficLightState vehicleLightState,
                    out pedestrianLightState,
                    out bool vehicles,
                    out bool pedestrians);

                if (pedestrianLightState == RoadBaseAI.TrafficLightState.GreenToRed
                    || pedestrianLightState == RoadBaseAI.TrafficLightState.Red) {
                    if (!pedestrians && stepWaitTime >= 196u) {
                        RoadBaseAI.SetTrafficLightState(
                            nodeId,
                            ref segmentsBuffer[segmentId],
                            currentFrameIndex - simGroup,
                            vehicleLightState,
                            pedestrianLightState,
                            vehicles,
                            true);
                    }

                    return false;
                }

                // NON-STOCK CODE START
            } else {
                if (lights.InvalidPedestrianLight) {
                    pedestrianLightState = RoadBaseAI.TrafficLightState.Green;
                } else {
                    pedestrianLightState = (RoadBaseAI.TrafficLightState)lights.PedestrianLightState;
                }

#if DEBUG
                Log._DebugIf(
                    logTimedLights,
                    () => $"CustomHumanAI.CustomCheckTrafficLights({nodeId}, {segmentId}): " +
                    $"Custom simulation! pedestrianLightState={pedestrianLightState}, " +
                    $"lights.InvalidPedestrianLight={lights.InvalidPedestrianLight}");
#endif
            }

            // NON-STOCK CODE END
            switch (pedestrianLightState) {
                case RoadBaseAI.TrafficLightState.RedToGreen:
                    if (stepWaitTime < 60u) {
                        return false;
                    }

                    break;
                case RoadBaseAI.TrafficLightState.Red:
                case RoadBaseAI.TrafficLightState.GreenToRed:
                    return false;
            }

            return true;
        }

        [RedirectReverse]
        [MethodImpl(MethodImplOptions.NoInlining)]
        [UsedImplicitly]
        private void PathfindFailure(ushort instanceId, ref CitizenInstance data) {
            Log._DebugOnlyError("HumanAI.PathfindFailure is not overriden!");
        }

        [RedirectReverse]
        [MethodImpl(MethodImplOptions.NoInlining)]
        [UsedImplicitly]
        private void PathfindSuccess(ushort instanceId, ref CitizenInstance data) {
            Log._DebugOnlyError("HumanAI.PathfindSuccess is not overriden!");
        }

        [RedirectReverse]
        [MethodImpl(MethodImplOptions.NoInlining)]
        [UsedImplicitly]
        private void Spawn(ushort instanceId, ref CitizenInstance data) {
            Log._DebugOnlyError("HumanAI.Spawn is not overriden!");
        }

        [RedirectReverse]
        [MethodImpl(MethodImplOptions.NoInlining)]
        [UsedImplicitly]
        private void GetBuildingTargetPosition(ushort instanceId, ref CitizenInstance data, float minSqrDistance) {
            Log._DebugOnlyError("HumanAI.GetBuildingTargetPosition is not overriden!");
        }

        [RedirectReverse]
        [MethodImpl(MethodImplOptions.NoInlining)]
        [UsedImplicitly]
        private void WaitTouristVehicle(ushort instanceId, ref CitizenInstance data, ushort targetBuildingId) {
            Log._DebugOnlyError("HumanAI.InvokeWaitTouristVehicle is not overriden!");
        }

        [RedirectReverse]
        [MethodImpl(MethodImplOptions.NoInlining)]
        [UsedImplicitly]
        private void ArriveAtDestination(ushort instanceId, ref CitizenInstance citizenData, bool success) {
            Log._DebugOnlyError("HumanAI.ArriveAtDestination is not overriden!");
        }
    }
}
