namespace TrafficManager.Custom.AI {
    using System.Runtime.CompilerServices;
    using API.Traffic.Enums;
    using API.TrafficLight;
    using ColossalFramework;
    using CSUtil.Commons;
    using JetBrains.Annotations;
    using Manager.Impl;
    using RedirectionFramework.Attributes;
    using State;
    using State.ConfigData;
    using Traffic.Data;
    using Traffic.Enums;
    using UnityEngine;

    [TargetType(typeof(HumanAI))]
    public class CustomHumanAI : CitizenAI {
        [RedirectMethod]
        public void CustomSimulationStep(ushort instanceId,
                                         ref CitizenInstance instanceData,
                                         Vector3 physicsLodRefPos) {
#if DEBUG
            var citizenDebug = (DebugSettings.CitizenInstanceId == 0
                            || DebugSettings.CitizenInstanceId == instanceId)
                           && (DebugSettings.CitizenId == 0
                               || DebugSettings.CitizenId == instanceData.m_citizen)
                           && (DebugSettings.SourceBuildingId == 0
                               || DebugSettings.SourceBuildingId == instanceData.m_sourceBuilding)
                           && (DebugSettings.TargetBuildingId == 0
                               || DebugSettings.TargetBuildingId == instanceData.m_targetBuilding);
            bool logParkingAi = DebugSwitch.BasicParkingAILog.Get() && citizenDebug;
            bool extendedLogParkingAi = DebugSwitch.ExtendedParkingAILog.Get() && citizenDebug;
#endif
            var citizenManager = Singleton<CitizenManager>.instance;
            var citizenId = instanceData.m_citizen;

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

            var ctzBuffer = citizenManager.m_citizens.m_buffer;
            if ((instanceData.m_flags & CitizenInstance.Flags.WaitingPath) != CitizenInstance.Flags.None) {
                var pathManager = Singleton<PathManager>.instance;
                var pathFindFlags = pathManager.m_pathUnits.m_buffer[instanceData.m_path].m_pathFindFlags;

                // NON-STOCK CODE START
                var mainPathState = ExtPathState.Calculating;
                if ((pathFindFlags & PathUnit.FLAG_FAILED) != 0 || instanceData.m_path == 0) {
                    mainPathState = ExtPathState.Failed;
                } else if ((pathFindFlags & PathUnit.FLAG_READY) != 0) {
                    mainPathState = ExtPathState.Ready;
                }

#if DEBUG
                if (logParkingAi) {
                    Log._Debug($"CustomHumanAI.CustomSimulationStep({instanceId}): " +
                               $"Path: {instanceData.m_path}, mainPathState={mainPathState}");
                }
#endif

                var finalPathState = ExtSoftPathState.None;
#if BENCHMARK
                using (var bm = new Benchmark(null, "ConvertPathStateToSoftPathState+UpdateCitizenPathState")) {
#endif
                finalPathState = ExtCitizenInstance.ConvertPathStateToSoftPathState(mainPathState);

                if (Options.parkingAI) {
                    finalPathState = AdvancedParkingManager.Instance.UpdateCitizenPathState(
                        instanceId,
                        ref instanceData,
                        ref ExtCitizenInstanceManager.Instance.ExtInstances[instanceId],
                        ref ExtCitizenManager.Instance.ExtCitizens[citizenId],
                        ref ctzBuffer[instanceData.m_citizen],
                        mainPathState);
#if DEBUG
                    if (logParkingAi) {
                        Log._Debug($"CustomHumanAI.CustomSimulationStep({instanceId}): " +
                                   $"Applied Parking AI logic. Path: {instanceData.m_path}, " +
                                   $"mainPathState={mainPathState}, finalPathState={finalPathState}, " +
                                   $"extCitizenInstance={ExtCitizenInstanceManager.Instance.ExtInstances[instanceId]}");
                    }
#endif
                } // if Options.parkingAi
#if BENCHMARK
                }
#endif

                switch (finalPathState) {
                    case ExtSoftPathState.Ready: {
#if DEBUG
                        if (logParkingAi) {
                            Log._Debug($"CustomHumanAI.CustomSimulationStep({instanceId}): Path-finding " +
                                       $"succeeded for citizen instance {instanceId} " +
                                       $"(finalPathState={finalPathState}). Path: {instanceData.m_path} " +
                                       "-- calling HumanAI.PathfindSuccess");
                        }
#endif
                        if (citizenId == 0
                            || ctzBuffer[instanceData.m_citizen].m_vehicle == 0) {
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
                            && (ctzBuffer[citizenId].m_flags & CTZ_MASK) == Citizen.Flags.MovingIn)
                        {
                            var statisticBase = Singleton<StatisticsManager>
                                                .instance.Acquire<StatisticInt32>(StatisticType.MoveRate);
                            statisticBase.Add(1);
                        }

                        // NON-STOCK CODE END
                        PathfindSuccess(instanceId, ref instanceData);
                        break;
                    }

                    case ExtSoftPathState.Ignore: {
#if DEBUG
                        if (logParkingAi) {
                            Log._Debug($"CustomHumanAI.CustomSimulationStep({instanceId}): " +
                                       $"Path-finding result shall be ignored for citizen instance " +
                                       $"{instanceId} (finalPathState={finalPathState}). " +
                                       $"Path: {instanceData.m_path} -- ignoring");
                        }
#endif
                        return;
                    }

                    case ExtSoftPathState.Calculating:
                    default: {
#if DEBUG
                        if (logParkingAi) {
                            Log._Debug($"CustomHumanAI.CustomSimulationStep({instanceId}): " +
                                       $"Path-finding result undetermined for citizen instance {instanceId} " +
                                       $"(finalPathState={finalPathState}). " +
                                       $"Path: {instanceData.m_path} -- continue");
                        }
#endif
                        break;
                    }

                    case ExtSoftPathState.FailedHard: {
#if DEBUG
                        if (logParkingAi) {
                            Log._Debug($"CustomHumanAI.CustomSimulationStep({instanceId}): " +
                                       $"HARD path-finding failure for citizen instance {instanceId} " +
                                       $"(finalPathState={finalPathState}). Path: {instanceData.m_path} " +
                                       "-- calling HumanAI.PathfindFailure");
                        }
#endif
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
#if DEBUG
                        if (logParkingAi) {
                            Log._Debug($"CustomHumanAI.CustomSimulationStep({instanceId}): " +
                                       $"SOFT path-finding failure for citizen instance {instanceId} " +
                                       $"(finalPathState={finalPathState}). Path: {instanceData.m_path} " +
                                       "-- calling HumanAI.InvalidPath");
                        }
#endif
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
#if BENCHMARK
            using (var bm = new Benchmark(null, "ExtSimulationStep")) {
#endif
            if (Options.parkingAI) {
                if (ExtSimulationStep(
                    instanceId,
                    ref instanceData,
                    ref ExtCitizenInstanceManager.Instance.ExtInstances[instanceId],
                    physicsLodRefPos)) {
                    return;
                }
            }
#if BENCHMARK
            }
#endif

            // NON-STOCK CODE END
            base.SimulationStep(instanceId, ref instanceData, physicsLodRefPos);

            var vehicleManager = Singleton<VehicleManager>.instance;
            ushort vehicleId = 0;
            if (instanceData.m_citizen != 0u) {
                vehicleId = ctzBuffer[instanceData.m_citizen].m_vehicle;
            }

            if (vehicleId != 0) {
                var vehiclesBuffer = vehicleManager.m_vehicles.m_buffer;
                var vehicleInfo = vehiclesBuffer[vehicleId].Info;

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

            if (vehicleId == 0 &&
                (instanceData.m_flags & (CitizenInstance.Flags.Character
                                         | CitizenInstance.Flags.WaitingPath
                                         | CitizenInstance.Flags.Blown
                                         | CitizenInstance.Flags.Floating)) == CitizenInstance.Flags.None) {
                instanceData.m_flags &= ~(CitizenInstance.Flags.HangAround
                                          | CitizenInstance.Flags.Panicking
                                          | CitizenInstance.Flags.SittingDown);
                ArriveAtDestination(instanceId, ref instanceData, false);
                citizenManager.ReleaseCitizenInstance(instanceId);
            }
        }

        public bool ExtSimulationStep(ushort instanceId,
                                      ref CitizenInstance instanceData,
                                      ref ExtCitizenInstance extInstance,
                                      Vector3 physicsLodRefPos) {
            var extCitInstMan = Constants.ManagerFactory.ExtCitizenInstanceManager;
#if DEBUG
            var citizenDebug = (DebugSettings.CitizenInstanceId == 0
                            || DebugSettings.CitizenInstanceId == instanceId)
                           && (DebugSettings.CitizenId == 0
                               || DebugSettings.CitizenId == instanceData.m_citizen)
                           && (DebugSettings.SourceBuildingId == 0
                               || DebugSettings.SourceBuildingId == instanceData.m_sourceBuilding)
                           && (DebugSettings.TargetBuildingId == 0
                               || DebugSettings.TargetBuildingId == instanceData.m_targetBuilding);
            var logParkingAi = DebugSwitch.BasicParkingAILog.Get() && citizenDebug;
            var extendedLogParkingAi = DebugSwitch.ExtendedParkingAILog.Get() && citizenDebug;
#endif

            switch (extInstance.pathMode) {
                // check if the citizen has reached a parked car or target
                case ExtPathMode.WalkingToParkedCar:
                case ExtPathMode.ApproachingParkedCar: {
                    var citizensBuffer = Singleton<CitizenManager>.instance.m_citizens.m_buffer;
                    var parkedVehicleId = citizensBuffer[instanceData.m_citizen].m_parkedVehicle;

                    if (parkedVehicleId == 0) {
                        // citizen is reaching their parked car but does not own a parked car
#if DEBUG
                        if (logParkingAi) {
                            Log.Warning($"CustomHumanAI.ExtSimulationStep({instanceId}): " +
                                        $"Citizen instance {instanceId} was walking to / reaching " +
                                        $"their parked car ({extInstance.pathMode}) but parked " +
                                        $"car has disappeared. RESET.");
                        }
#endif
                        extCitInstMan.Reset(ref extInstance);

                        instanceData.m_flags &= ~CitizenInstance.Flags.WaitingPath;
                        instanceData.m_flags &= ~(CitizenInstance.Flags.HangAround
                                                  | CitizenInstance.Flags.Panicking
                                                  | CitizenInstance.Flags.SittingDown
                                                  | CitizenInstance.Flags.Cheering);
                        InvalidPath(instanceId, ref instanceData);
                        return true;
                    }

                    var parkedVehicles = Singleton<VehicleManager>.instance.m_parkedVehicles.m_buffer;
                    var approachState =
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
#if DEBUG
                            if (extendedLogParkingAi) {
                                Log._Debug($"CustomHumanAI.CustomSimulationStep({instanceId}): " +
                                           $"Citizen instance {instanceId} arrived at parked car. " +
                                           $"PathMode={extInstance.pathMode}");
                            }
#endif
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

                            if (!StartPathFind(instanceId, ref instanceData)) {
                                instanceData.Unspawn(instanceId);
                                extCitInstMan.Reset(ref extInstance);
                            }

                            return true;
                        }

                        case ParkedCarApproachState.Failure: {
#if DEBUG
                            if (logParkingAi) {
                                Log._Debug($"CustomHumanAI.ExtSimulationStep({instanceId}): " +
                                           $"Citizen instance {instanceId} failed to arrive at " +
                                           $"parked car. PathMode={extInstance.pathMode}");
                            }
#endif
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
#if DEBUGTTL
            var logTimedLights = DebugSwitch.TimedTrafficLights.Get() && DebugSettings.NodeId == nodeId;
#endif
            var netManager = Singleton<NetManager>.instance;
            var currentFrameIndex = Singleton<SimulationManager>.instance.m_currentFrameIndex;
            var simGroup = (uint)nodeId >> 7;
            var stepWaitTime = currentFrameIndex - simGroup & 255u;

            // NON-STOCK CODE START
#if BENCHMARK
            using (var bm = new Benchmark(null, "GetNodeSimulation")) {
#endif
            var customSim = Options.timedLightsEnabled &&
                            TrafficLightSimulationManager.Instance.HasActiveSimulation(nodeId);
#if BENCHMARK
            }
#endif
            RoadBaseAI.TrafficLightState pedestrianLightState;
            var segmentsBuffer = netManager.m_segments.m_buffer;
            var startNode = segmentsBuffer[segmentId].m_startNode == nodeId;

            ICustomSegmentLights lights = null;
#if BENCHMARK
            using (var bm = new Benchmark(null, "GetSegmentLights")) {
#endif
            if (customSim) {
                lights = CustomSegmentLightsManager.Instance.GetSegmentLights(segmentId, startNode, false);
            }
#if BENCHMARK
            }
#endif

            if (lights == null) {
                // NON-STOCK CODE END
#if DEBUGTTL
                if (logTimedLights) {
                    Log._Debug($"CustomHumanAI.CustomCheckTrafficLights({nodeId}, " +
                               $"{segmentId}): No custom simulation!");
                }
#endif

                RoadBaseAI.GetTrafficLightState(
                    nodeId,
                    ref segmentsBuffer[segmentId],
                    currentFrameIndex - simGroup,
                    out var vehicleLightState,
                    out pedestrianLightState,
                    out var vehicles,
                    out var pedestrians);

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

#if DEBUGTTL
                if (logTimedLights) {
                    Log._Debug($"CustomHumanAI.CustomCheckTrafficLights({nodeId}, {segmentId}): " +
                               $"Custom simulation! pedestrianLightState={pedestrianLightState}, " +
                               $"lights.InvalidPedestrianLight={lights.InvalidPedestrianLight}");
                }
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
            Log.Error($"HumanAI.PathfindFailure is not overriden!");
        }

        [RedirectReverse]
        [MethodImpl(MethodImplOptions.NoInlining)]
        [UsedImplicitly]
        private void PathfindSuccess(ushort instanceId, ref CitizenInstance data) {
            Log.Error($"HumanAI.PathfindSuccess is not overriden!");
        }

        [RedirectReverse]
        [MethodImpl(MethodImplOptions.NoInlining)]
        [UsedImplicitly]
        private void Spawn(ushort instanceId, ref CitizenInstance data) {
            Log.Error($"HumanAI.Spawn is not overriden!");
        }

        [RedirectReverse]
        [MethodImpl(MethodImplOptions.NoInlining)]
        [UsedImplicitly]
        private void GetBuildingTargetPosition(ushort instanceId, ref CitizenInstance data, float minSqrDistance) {
            Log.Error($"HumanAI.GetBuildingTargetPosition is not overriden!");
        }

        [RedirectReverse]
        [MethodImpl(MethodImplOptions.NoInlining)]
        [UsedImplicitly]
        private void WaitTouristVehicle(ushort instanceId, ref CitizenInstance data, ushort targetBuildingId) {
            Log.Error($"HumanAI.InvokeWaitTouristVehicle is not overriden!");
        }

        [RedirectReverse]
        [MethodImpl(MethodImplOptions.NoInlining)]
        [UsedImplicitly]
        private void ArriveAtDestination(ushort instanceId, ref CitizenInstance citizenData, bool success) {
            Log.Error($"HumanAI.ArriveAtDestination is not overriden!");
        }
    }
}