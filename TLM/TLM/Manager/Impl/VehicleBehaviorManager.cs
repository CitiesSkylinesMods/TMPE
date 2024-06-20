namespace TrafficManager.Manager.Impl {
    using ColossalFramework;
    using CSUtil.Commons;
    using JetBrains.Annotations;
    using System;
    using TrafficManager.API.Manager;
    using TrafficManager.API.Traffic.Data;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.Custom.PathFinding;
    using TrafficManager.State.ConfigData;
    using TrafficManager.State;
    using TrafficManager.UI.SubTools.SpeedLimits;
    using TrafficManager.Util;
    using UnityEngine;
    using TrafficManager.Util.Extensions;

    public class VehicleBehaviorManager : AbstractCustomManager, IVehicleBehaviorManager {
        public const float MIN_SPEED = 8f * 0.2f; // 10 km/h

        [UsedImplicitly]
        public const float MAX_EVASION_SPEED = 8f * 1f; // 50 km/h

        [UsedImplicitly]
        public const float EVASION_SPEED = 8f * 0.2f; // 10 km/h

        public const float ICY_ROADS_MIN_SPEED = 8f * 0.4f; // 20 km/h

        public const float ICY_ROADS_STUDDED_MIN_SPEED = 8f * 0.8f; // 40 km/h

        public const float WET_ROADS_MAX_SPEED = 8f * 2f; // 100 km/h

        public const float WET_ROADS_FACTOR = 0.75f;

        public const float BROKEN_ROADS_MAX_SPEED = 8f * 1.6f; // 80 km/h

        public const float BROKEN_ROADS_FACTOR = 0.75f;

        public const VehicleInfo.VehicleType RECKLESS_VEHICLE_TYPES = VehicleInfo.VehicleType.Car;

        private static PathUnit.Position DUMMY_POS = default;

        private static readonly uint[] POW2MASKS = {
            1u << 0, 1u << 1, 1u << 2, 1u << 3,
            1u << 4, 1u << 5, 1u << 6, 1u << 7,
            1u << 8, 1u << 9, 1u << 10, 1u << 11,
            1u << 12, 1u << 13, 1u << 14, 1u << 15,
            1u << 16, 1u << 17, 1u << 18, 1u << 19,
            1u << 20, 1u << 21, 1u << 22, 1u << 23,
            1u << 24, 1u << 25, 1u << 26, 1u << 27,
            1u << 28, 1u << 29, 1u << 30, 1u << 31,
        };

        public static readonly VehicleBehaviorManager Instance = new VehicleBehaviorManager();

        private VehicleBehaviorManager() { }

        public bool ParkPassengerCar(ushort vehicleID,
                                     ref Vehicle vehicleData,
                                     VehicleInfo vehicleInfo,
                                     uint driverCitizenId,
                                     ref Citizen driverCitizen,
                                     ushort driverCitizenInstanceId,
                                     ref CitizenInstance driverInstance,
                                     ref ExtCitizenInstance driverExtInstance,
                                     ushort targetBuildingId,
                                     PathUnit.Position pathPos,
                                     uint nextPath,
                                     int nextPositionIndex,
                                     out byte segmentOffset) {
#if DEBUG
            bool citizenDebug
                = (DebugSettings.VehicleId == 0
                   || DebugSettings.VehicleId == vehicleID)
                  && (DebugSettings.CitizenInstanceId == 0
                      || DebugSettings.CitizenInstanceId == driverExtInstance.instanceId)
                  && (DebugSettings.CitizenId == 0
                      || DebugSettings.CitizenId == driverInstance.m_citizen)
                  && (DebugSettings.SourceBuildingId == 0
                      || DebugSettings.SourceBuildingId == driverInstance.m_sourceBuilding)
                  && (DebugSettings.TargetBuildingId == 0
                      || DebugSettings.TargetBuildingId == driverInstance.m_targetBuilding);

            bool logParkingAi = DebugSwitch.BasicParkingAILog.Get() && citizenDebug;
            bool extendedLogParkingAi = DebugSwitch.ExtendedParkingAILog.Get() && citizenDebug;
#else
            const bool logParkingAi = false;
            const bool extendedLogParkingAi = false;
#endif
            var extCitizenInstanceManager = Constants.ManagerFactory.ExtCitizenInstanceManager;
            PathManager pathManager = Singleton<PathManager>.instance;
            CitizenManager citizenManager = Singleton<CitizenManager>.instance;
            NetManager netManager = Singleton<NetManager>.instance;
            VehicleManager vehicleManager = Singleton<VehicleManager>.instance;
            uint maxUnitCount = citizenManager.m_units.m_size;
            CitizenInstance[] citizenInstancesBuf = citizenManager.m_instances.m_buffer;
            CitizenUnit[] citizenUnitsBuf = citizenManager.m_units.m_buffer;
            Citizen[] citizensBuf = citizenManager.m_citizens.m_buffer;

            // NON-STOCK CODE START
            bool prohibitPocketCars = false;
            // NON-STOCK CODE END

            if (driverCitizenId != 0u) {
                if (SavedGameOptions.Instance.parkingAI && driverCitizenInstanceId != 0) {
                    prohibitPocketCars = true;
                }

                uint laneID = PathManager.GetLaneID(pathPos);
                segmentOffset = (byte)Singleton<SimulationManager>.instance.m_randomizer.Int32(1, 254);

                laneID.ToLane().CalculatePositionAndDirection(
                    segmentOffset * 0.003921569f,
                    out Vector3 refPos,
                    out Vector3 vector);

                ref NetSegment netSegment = ref pathPos.m_segment.ToSegment();

                NetInfo info = netSegment.Info;
                bool isSegmentInverted = (netSegment.m_flags & NetSegment.Flags.Invert) != NetSegment.Flags.None;
                bool isPosNegative = info.m_lanes[pathPos.m_lane].m_position < 0f;
                Vector3 searchDir;

                vector.Normalize();

                if (isSegmentInverted != isPosNegative) {
                    searchDir.x = -vector.z;
                    searchDir.y = 0f;
                    searchDir.z = vector.x;
                } else {
                    searchDir.x = vector.z;
                    searchDir.y = 0f;
                    searchDir.z = -vector.x;
                }

                ushort homeID = 0;

                if (driverCitizenId != 0u) {
                    homeID = driverCitizen.m_homeBuilding;
                }

                Vector3 parkPos = default;
                Quaternion parkRot = default;
                float parkOffset = -1f;

                // NON-STOCK CODE START
                bool foundParkingSpace = false;
                bool searchedParkingSpace = false;

                if (prohibitPocketCars) {
                    if (logParkingAi) {
                        Log._DebugFormat(
                            "CustomPassengerCarAI.ExtParkVehicle({0}): Vehicle {1} tries to park on " +
                            "a parking position now (flags: {2})! CurrentPathMode={3} path={4} " +
                            "pathPositionIndex={5} segmentId={6} laneIndex={7} offset={8} nextPath={9} " +
                            "refPos={10} searchDir={11} home={12} driverCitizenId={13} driverCitizenInstanceId={14}",
                            vehicleID,
                            vehicleID,
                            vehicleData.m_flags,
                            driverExtInstance.pathMode,
                            vehicleData.m_path,
                            vehicleData.m_pathPositionIndex,
                            pathPos.m_segment,
                            pathPos.m_lane,
                            pathPos.m_offset,
                            nextPath,
                            refPos,
                            searchDir,
                            homeID,
                            driverCitizenId,
                            driverCitizenInstanceId);
                    }

                    if (driverExtInstance.pathMode == ExtPathMode.DrivingToAltParkPos
                        || driverExtInstance.pathMode == ExtPathMode.DrivingToKnownParkPos) {
                        // try to use previously found parking space
                        if (logParkingAi) {
                            Log._DebugFormat(
                                "Vehicle {0} tries to park on an (alternative) parking position now! " +
                                "CurrentPathMode={1} altParkingSpaceLocation={2} altParkingSpaceLocationId={3}",
                                vehicleID,
                                driverExtInstance.pathMode,
                                driverExtInstance.parkingSpaceLocation,
                                driverExtInstance.parkingSpaceLocationId);
                        }

                        searchedParkingSpace = true;

                        switch (driverExtInstance.parkingSpaceLocation) {
                            case ExtParkingSpaceLocation.RoadSide: {
                                if (logParkingAi) {
                                    Log._Debug($"Vehicle {vehicleID} wants to park road-side @ " +
                                               $"segment {driverExtInstance.parkingSpaceLocationId}");
                                }

                                foundParkingSpace =
                                    AdvancedParkingManager.Instance.FindParkingSpaceRoadSideForVehiclePos(
                                            vehicleInfo,
                                            0,
                                            driverExtInstance.parkingSpaceLocationId,
                                            refPos,
                                            out parkPos,
                                            out parkRot,
                                            out parkOffset,
                                            out uint _,
                                            out int _);
                                break;
                            }

                            case ExtParkingSpaceLocation.Building: {
                                float maxDist = 9999f;

                                if (logParkingAi) {
                                    Log._Debug($"Vehicle {vehicleID} wants to park @ " +
                                               $"building {driverExtInstance.parkingSpaceLocationId}");
                                }

                                ref Building parkingSpaceBuilding = ref driverExtInstance.parkingSpaceLocationId.ToBuilding();

                                foundParkingSpace =
                                    AdvancedParkingManager.Instance.FindParkingSpacePropAtBuilding(
                                        vehicleInfo,
                                        homeID,
                                        0,
                                        driverExtInstance.parkingSpaceLocationId,
                                        ref parkingSpaceBuilding,
                                        pathPos.m_segment,
                                        refPos,
                                        ref maxDist,
                                        true,
                                        out parkPos,
                                        out parkRot,
                                        out parkOffset);
                                break;
                            }

                            default: {
                                Log.Error($"No alternative parking position stored for vehicle {vehicleID}! " +
                                          $"PathMode={driverExtInstance.pathMode}");

                                foundParkingSpace =
                                    Constants.ManagerFactory.AdvancedParkingManager.FindParkingSpaceInVicinity(
                                                 refPos,
                                                 searchDir,
                                                 vehicleInfo,
                                                 homeID,
                                                 vehicleID,
                                                 GlobalConfig.Instance.ParkingAI.MaxBuildingToPedestrianLaneDistance,
                                                 out ExtParkingSpaceLocation _,
                                                 out ushort _,
                                                 out parkPos,
                                                 out parkRot,
                                                 out parkOffset);
                                break;
                            }
                        }
                    }
                }

                if (!searchedParkingSpace) {
                    bool isElectric = vehicleInfo.m_class.m_subService != ItemClass.SubService.ResidentialLow;
                    foundParkingSpace =
                        Constants.ManagerFactory.AdvancedParkingManager.VanillaFindParkingSpaceWithoutRestrictions(
                            isElectric,
                            homeID,
                            refPos,
                            searchDir,
                            pathPos.m_segment,
                            vehicleInfo.m_generatedInfo.m_size.x,
                            vehicleInfo.m_generatedInfo.m_size.z,
                            out parkPos,
                            out parkRot,
                            out parkOffset);

                    if (logParkingAi) {
                        Log._Debug(
                            $"CustomPassengerCarAI.ExtParkVehicle({vehicleID}): Found parking space? " +
                            $"{foundParkingSpace}. parkPos={parkPos}, parkRot={parkRot}, parkOffset={parkOffset}");
                    }
                }

                // NON-STOCK CODE END
                ushort parkedVehicleId = 0;
                bool parkedCarCreated = foundParkingSpace && vehicleManager.CreateParkedVehicle(
                                            out parkedVehicleId,
                                            ref Singleton<SimulationManager>.instance.m_randomizer,
                                            vehicleInfo,
                                            parkPos,
                                            parkRot,
                                            driverCitizenId);
                if (logParkingAi) {
                    Log._Debug($"CustomPassengerCarAI.ExtParkVehicle({vehicleID}): " +
                               $"Parked car created? {parkedCarCreated}");
                }

                IExtBuildingManager extBuildingManager = Constants.ManagerFactory.ExtBuildingManager;
                if (foundParkingSpace && parkedCarCreated) {
                    // we have reached a parking position
                    float sqrDist = (refPos - parkPos).sqrMagnitude;
                    if (extendedLogParkingAi) {
                        Log._Debug(
                            $"CustomPassengerCarAI.ExtParkVehicle({vehicleID}): Vehicle {vehicleID} " +
                            $"succeeded in parking! CurrentPathMode={driverExtInstance.pathMode} sqrDist={sqrDist}");
                    }

                    driverCitizen.SetParkedVehicle(driverCitizenId, parkedVehicleId);
                    if (parkOffset >= 0f) {
                        segmentOffset = (byte)(parkOffset * 255f);
                    }

                    // NON-STOCK CODE START
                    if (prohibitPocketCars) {
                        if ((driverExtInstance.pathMode == ExtPathMode.DrivingToAltParkPos
                             || driverExtInstance.pathMode == ExtPathMode.DrivingToKnownParkPos)
                            && targetBuildingId != 0)
                        {
                            // decrease parking space demand of target building
                            Constants.ManagerFactory.ExtBuildingManager.ModifyParkingSpaceDemand(
                                ref extBuildingManager.ExtBuildings[targetBuildingId],
                                parkPos,
                                GlobalConfig.Instance.ParkingAI.MinFoundParkPosParkingSpaceDemandDelta,
                                GlobalConfig.Instance.ParkingAI.MaxFoundParkPosParkingSpaceDemandDelta);
                        }

                        // if (driverExtInstance.CurrentPathMode == ExtCitizenInstance.PathMode.DrivingToAltParkPos
                        // || driverExtInstance.CurrentPathMode == ExtCitizenInstance.PathMode.DrivingToKnownParkPos) {
                        //
                        // we have reached an (alternative) parking position and succeeded in finding a parking space
                        driverExtInstance.pathMode = ExtPathMode.RequiresWalkingPathToTarget;
                        driverExtInstance.failedParkingAttempts = 0;
                        driverExtInstance.parkingSpaceLocation = ExtParkingSpaceLocation.None;
                        driverExtInstance.parkingSpaceLocationId = 0;

                        if (logParkingAi) {
                            Log._Debug(
                                $"CustomPassengerCarAI.ExtParkVehicle({vehicleID}): Vehicle {vehicleID} " +
                                "has reached an (alternative) parking position! " +
                                $"CurrentPathMode={driverExtInstance.pathMode} position={parkPos}");
                        }

                        //}
                    }
                } else if (prohibitPocketCars) {
                    // could not find parking space. vehicle would despawn.
                    ref Building targetBuilding = ref targetBuildingId.ToBuilding();

                    if (targetBuildingId != 0
                        && (targetBuilding.m_flags & Building.Flags.IncomingOutgoing) != Building.Flags.None
                        && (refPos - targetBuilding.m_position).magnitude
                        <= GlobalConfig.Instance.ParkingAI.MaxBuildingToPedestrianLaneDistance)
                    {
                        // vehicle is at target and target is an outside connection: accept despawn
                        Log._DebugIf(
                            logParkingAi,
                            () => $"CustomPassengerCarAI.ExtParkVehicle({vehicleID}): Driver citizen " +
                            $"instance {driverCitizenInstanceId} wants to park at an outside connection. Aborting.");

                        return true;
                    }

                    // Find parking space in the vicinity, redo path-finding to the parking space,
                    // park the vehicle and do citizen path-finding to the current target
                    //-------------------------------------------
                    if (!foundParkingSpace
                        && (driverExtInstance.pathMode == ExtPathMode.DrivingToAltParkPos
                            || driverExtInstance.pathMode == ExtPathMode.DrivingToKnownParkPos)
                        && targetBuildingId != 0) {
                        // increase parking space demand of target building
                        if (driverExtInstance.failedParkingAttempts > 1) {
                            extBuildingManager.AddParkingSpaceDemand(
                                ref extBuildingManager.ExtBuildings[targetBuildingId],
                                GlobalConfig.Instance.ParkingAI.FailedParkingSpaceDemandIncrement *
                                (uint)(driverExtInstance.failedParkingAttempts - 1));
                        }
                    }

                    if (!foundParkingSpace) {
                        Log._DebugIf(
                            logParkingAi,
                            () => $"CustomPassengerCarAI.ExtParkVehicle({vehicleID}): Parking failed " +
                            $"for vehicle {vehicleID}: Could not find parking space. ABORT.");

                        ++driverExtInstance.failedParkingAttempts;
                    } else {
                        Log._DebugIf(
                            logParkingAi,
                            () => $"CustomPassengerCarAI.ExtParkVehicle({vehicleID}): Parking failed " +
                            $"for vehicle {vehicleID}: Parked car could not be created. ABORT.");

                        driverExtInstance.failedParkingAttempts = GlobalConfig.Instance.ParkingAI.MaxParkingAttempts + 1;
                    }

                    driverExtInstance.pathMode = ExtPathMode.ParkingFailed;
                    driverExtInstance.parkingPathStartPosition = pathPos;

                    if (logParkingAi) {
                        Log._DebugFormat(
                            "CustomPassengerCarAI.ExtParkVehicle({0}): Parking failed for vehicle {1}! " +
                            "(flags: {2}) pathPos segment={3}, lane={4}, offset={5}. Trying to find " +
                            "parking space in the vicinity. FailedParkingAttempts={6}, " +
                            "CurrentPathMode={7} foundParkingSpace={8}",
                            vehicleID,
                            vehicleID,
                            vehicleData.m_flags,
                            pathPos.m_segment,
                            pathPos.m_lane,
                            pathPos.m_offset,
                            driverExtInstance.failedParkingAttempts,
                            driverExtInstance.pathMode,
                            foundParkingSpace);
                    }

                    // invalidate paths of all passengers in order to force path recalculation
                    uint curUnitId = vehicleData.m_citizenUnits;
                    int numIter = 0;

                    while (curUnitId != 0u) {
                        ref CitizenUnit currentCitizenUnit = ref citizenUnitsBuf[curUnitId];

                        for (int i = 0; i < 5; i++) {
                            uint curCitizenId = currentCitizenUnit.GetCitizen(i);

                            if (curCitizenId != 0u) {
                                ushort citizenInstanceId = citizensBuf[curCitizenId].m_instance;
                                if (citizenInstanceId == 0) {
                                    continue;
                                }

                                ref CitizenInstance citizenInstance = ref citizenInstancesBuf[citizenInstanceId];

                                if (logParkingAi) {
                                    Log._DebugFormat(
                                        "CustomPassengerCarAI.ExtParkVehicle({0}): Releasing path " +
                                        "for citizen instance {1} sitting in vehicle {2} (was {3}).",
                                        vehicleID,
                                        citizenInstanceId,
                                        vehicleID,
                                        citizenInstance.m_path);
                                }

                                if (citizenInstanceId != driverCitizenInstanceId) {
                                    if (logParkingAi) {
                                        Log._DebugFormat(
                                            "CustomPassengerCarAI.ExtParkVehicle({0}): Resetting pathmode " +
                                            "for passenger citizen instance {1} sitting in " +
                                            "vehicle {2} (was {3}).",
                                            vehicleID,
                                            citizenInstanceId,
                                            vehicleID,
                                            ExtCitizenInstanceManager.Instance.ExtInstances[citizenInstanceId].pathMode);
                                    }

                                    extCitizenInstanceManager.Reset(ref extCitizenInstanceManager.ExtInstances[citizenInstanceId]);
                                }

                                if (citizenInstance.m_path != 0) {
                                    Singleton<PathManager>.instance.ReleasePath(
                                        citizenInstance.m_path);

                                    citizenInstance.m_path = 0u;
                                }
                            }
                        }

                        curUnitId = currentCitizenUnit.m_nextUnit;

                        if (++numIter > maxUnitCount) {
                            CODebugBase<LogChannel>.Error(
                                LogChannel.Core,
                                $"Invalid list detected!\n{Environment.StackTrace}");
                            break;
                        }
                    }

                    return false;

                    // NON-STOCK CODE END
                }
            } else {
                segmentOffset = pathPos.m_offset;
            }

            //-------------------------------
            // parking has succeeded
            //-------------------------------
            if (driverCitizenId != 0u) {
                uint curCitizenUnitId = vehicleData.m_citizenUnits;
                int numIter = 0;

                while (curCitizenUnitId != 0u) {
                    ref CitizenUnit currentCitizenUnit = ref citizenUnitsBuf[curCitizenUnitId];

                    for (int j = 0; j < 5; j++) {
                        uint citId = currentCitizenUnit.GetCitizen(j);
                        if (citId == 0u) {
                            continue;
                        }

                        ushort citizenInstanceId = citizensBuf[citId].m_instance;
                        if (citizenInstanceId == 0) {
                            continue;
                        }

                        ref CitizenInstance citizenInstance = ref citizenInstancesBuf[citizenInstanceId];

                        // NON-STOCK CODE START
                        if (prohibitPocketCars) {
                            if (driverExtInstance.pathMode == ExtPathMode.RequiresWalkingPathToTarget) {
                                if (logParkingAi) {
                                    Log._Debug(
                                        $"CustomPassengerCarAI.ExtParkVehicle({vehicleID}): Parking succeeded: " +
                                        $"Doing nothing for citizen instance {citizenInstanceId}! " +
                                        $"path: {citizenInstance.m_path}");
                                }

                                extCitizenInstanceManager.ExtInstances[citizenInstanceId].pathMode = ExtPathMode.RequiresWalkingPathToTarget;
                                continue;
                            }
                        }

                        // NON-STOCK CODE END
                        if (!pathManager.AddPathReference(nextPath)) {
                            continue;
                        }

                        if (citizenInstance.m_path != 0u) {
                            pathManager.ReleasePath(citizenInstance.m_path);
                        }

                        citizenInstance.m_path = nextPath;
                        citizenInstance.m_pathPositionIndex = (byte)nextPositionIndex;
                        citizenInstance.m_lastPathOffset = segmentOffset;

                        if (logParkingAi) {
                            Log._Debug(
                                $"CustomPassengerCarAI.ExtParkVehicle({vehicleID}): Parking succeeded " +
                                  $"(default): Setting path of citizen instance {citizenInstanceId} to {nextPath}!");
                        }
                    }

                    curCitizenUnitId = currentCitizenUnit.m_nextUnit;

                    if (++numIter > maxUnitCount) {
                        CODebugBase<LogChannel>.Error(
                            LogChannel.Core,
                            $"Invalid list detected!\n{Environment.StackTrace}");
                        break;
                    }
                }
            }

            if (prohibitPocketCars) {
                if (driverExtInstance.pathMode == ExtPathMode.RequiresWalkingPathToTarget) {
                    if (logParkingAi) {
                        Log._Debug(
                            $"CustomPassengerCarAI.ExtParkVehicle({vehicleID}): Parking succeeded " +
                            $"(alternative parking spot): Citizen instance {driverExtInstance} has " +
                            "to walk for the remaining path!");
                    }

                    // driverExtInstance.CurrentPathMode = ExtCitizenInstance.PathMode.CalculatingWalkingPathToTarget;
                    // if (debug)
                    //     Log._Debug($"Setting CurrentPathMode of vehicle {vehicleID} to
                    //     {driverExtInstance.CurrentPathMode}");
                }
            }

            return true;
        }

        public bool StartPassengerCarPathFind(ushort vehicleID,
                                              ref Vehicle vehicleData,
                                              VehicleInfo vehicleInfo,
                                              ushort driverInstanceId,
                                              ref CitizenInstance driverInstance,
                                              ref ExtCitizenInstance driverExtInstance,
                                              Vector3 startPos,
                                              Vector3 endPos,
                                              bool startBothWays,
                                              bool endBothWays,
                                              bool undergroundTarget,
                                              bool isHeavyVehicle,
                                              bool hasCombustionEngine,
                                              bool ignoreBlocked)
        {
            var extCitizenInstanceManager = Constants.ManagerFactory.ExtCitizenInstanceManager;
            CitizenManager citizenManager = CitizenManager.instance;
#if DEBUG
            bool citizenDebug
                = (DebugSettings.VehicleId == 0
                   || DebugSettings.VehicleId == vehicleID)
                  && (DebugSettings.CitizenInstanceId == 0
                      || DebugSettings.CitizenInstanceId == driverExtInstance.instanceId)
                  && (DebugSettings.CitizenId == 0
                      || DebugSettings.CitizenId == driverInstance.m_citizen)
                  && (DebugSettings.SourceBuildingId == 0
                      || DebugSettings.SourceBuildingId == driverInstance.m_sourceBuilding)
                  && (DebugSettings.TargetBuildingId == 0
                      || DebugSettings.TargetBuildingId == driverInstance.m_targetBuilding);

            bool logParkingAi = DebugSwitch.BasicParkingAILog.Get() && citizenDebug;
            bool extendedLogParkingAi = DebugSwitch.ExtendedParkingAILog.Get() && citizenDebug;
#else
            const bool logParkingAi = false;
            const bool extendedLogParkingAi = false;
#endif
            if (logParkingAi) {
                Log.WarningFormat(
                    "CustomPassengerCarAI.ExtStartPathFind({0}): called for vehicle {1}, " +
                    "driverInstanceId={2}, startPos={3}, endPos={4}, sourceBuilding={5}, " +
                    "targetBuilding={6} pathMode={7}",
                    vehicleID,
                    vehicleID,
                    driverInstanceId,
                    startPos,
                    endPos,
                    vehicleData.m_sourceBuilding,
                    vehicleData.m_targetBuilding,
                    driverExtInstance.pathMode);
            }

            PathUnit.Position startPosA = default;
            PathUnit.Position startPosB = default;
            PathUnit.Position endPosA = default;
            float sqrDistA = 0f;

            ushort targetBuildingId = driverInstance.m_targetBuilding;
            uint driverCitizenId = driverInstance.m_citizen;

            // NON-STOCK CODE START
            bool calculateEndPos = true;
            bool allowRandomParking = true;
            bool movingToParkingPos = false;
            bool foundStartingPos = false;
            bool skipQueue = (vehicleData.m_flags & Vehicle.Flags.Spawned) != 0;
            ExtPathType extPathType = ExtPathType.None;
            ref Building targetBuilding = ref targetBuildingId.ToBuilding();

            if (SavedGameOptions.Instance.parkingAI) {
                // if (driverExtInstance != null) {
                if (logParkingAi) {
                    Log.WarningFormat(
                        "CustomPassengerCarAI.ExtStartPathFind({0}): PathMode={1} for vehicle {2}, " +
                        "driver citizen instance {3}!",
                        vehicleID,
                        driverExtInstance.pathMode,
                        vehicleID,
                        driverExtInstance.instanceId);
                }

                if (driverExtInstance.pathMode == ExtPathMode.RequiresMixedCarPathToTarget) {
                    driverExtInstance.pathMode = ExtPathMode.CalculatingCarPathToTarget;
                    startBothWays = false;

                    if (logParkingAi) {
                        Log._DebugFormat(
                            "CustomPassengerCarAI.ExtStartPathFind({0}): PathMode was " +
                            "RequiresDirectCarPathToTarget: Parking spaces will NOT be searched " +
                            "beforehand. Setting pathMode={1}",
                            vehicleID,
                            driverExtInstance.pathMode);
                    }
                } else if (driverExtInstance.pathMode != ExtPathMode.ParkingFailed
                           && targetBuildingId != 0
                           && (targetBuilding.m_flags &
                               Building.Flags.IncomingOutgoing)
                           != Building.Flags.None) {
                    // target is outside connection
                    driverExtInstance.pathMode = ExtPathMode.CalculatingCarPathToTarget;

                    if (logParkingAi) {
                        Log._DebugFormat(
                            "CustomPassengerCarAI.ExtStartPathFind({0}): PathMode was not ParkingFailed " +
                            "and target is outside connection: Setting pathMode={1}",
                            vehicleID,
                            driverExtInstance.pathMode);
                    }
                } else {
                    if (driverExtInstance.pathMode == ExtPathMode.DrivingToTarget ||
                        driverExtInstance.pathMode == ExtPathMode.DrivingToKnownParkPos ||
                        driverExtInstance.pathMode == ExtPathMode.ParkingFailed) {
                        if (logParkingAi) {
                            Log._DebugFormat(
                                "CustomPassengerCarAI.ExtStartPathFind({0}): Skipping queue. pathMode={1}",
                                vehicleID,
                                driverExtInstance.pathMode);
                        }

                        skipQueue = true;
                    }

                    bool allowTourists = false;
                    bool searchAtCurrentPos = false;

                    if (driverExtInstance.pathMode == ExtPathMode.ParkingFailed) {
                        // previous parking attempt failed
                        driverExtInstance.pathMode = ExtPathMode.CalculatingCarPathToAltParkPos;
                        allowTourists = true;
                        searchAtCurrentPos = true;

                        if (logParkingAi) {
                            Log._DebugFormat(
                                "CustomPassengerCarAI.ExtStartPathFind({0}): Vehicle {1} shall move " +
                                "to an alternative parking position! CurrentPathMode={2} FailedParkingAttempts={3}",
                                vehicleID,
                                vehicleID,
                                driverExtInstance.pathMode,
                                driverExtInstance.failedParkingAttempts);
                        }

                        if (driverExtInstance.parkingPathStartPosition != null) {
                            startPosA =
                                (PathUnit.Position)driverExtInstance.parkingPathStartPosition;
                            foundStartingPos = true;

                            if (logParkingAi) {
                                Log._DebugFormat(
                                    "CustomPassengerCarAI.ExtStartPathFind({0}): Setting starting pos " +
                                    "for {1} to segment={2}, laneIndex={3}, offset={4}",
                                    vehicleID,
                                    vehicleID,
                                    startPosA.m_segment,
                                    startPosA.m_lane,
                                    startPosA.m_offset);
                            }
                        }

                        startBothWays = false;

                        if (driverExtInstance.failedParkingAttempts
                            > GlobalConfig.Instance.ParkingAI.MaxParkingAttempts) {
                            // maximum number of parking attempts reached
                            Log._DebugIf(
                                logParkingAi,
                                () =>
                                    $"CustomPassengerCarAI.ExtStartPathFind({vehicleID}): Reached " +
                                    $"maximum number of parking attempts for vehicle {vehicleID}! GIVING UP.");

                            extCitizenInstanceManager.Reset(ref driverExtInstance);

                            // pocket car fallback
                            // vehicleData.m_flags |= Vehicle.Flags.Parking;
                            return false;
                        }

                        if (extendedLogParkingAi) {
                            Log._DebugFormat(
                                "CustomPassengerCarAI.ExtStartPathFind({0}): Increased number of " +
                                "parking attempts for vehicle {1}: {2}/{3}",
                                vehicleID,
                                vehicleID,
                                driverExtInstance.failedParkingAttempts,
                                GlobalConfig.Instance.ParkingAI.MaxParkingAttempts);
                        }
                    } else {
                        driverExtInstance.pathMode =
                            ExtPathMode.CalculatingCarPathToKnownParkPos;

                        if (logParkingAi) {
                            Log._DebugFormat(
                                "CustomPassengerCarAI.ExtStartPathFind({0}): No parking involved: " +
                                "Setting pathMode={1}",
                                vehicleID,
                                driverExtInstance.pathMode);
                        }
                    }

                    ushort homeId = citizenManager.m_citizens.m_buffer[driverCitizenId].m_homeBuilding;
                    Vector3 returnPos =
                        searchAtCurrentPos ? (Vector3)vehicleData.m_targetPos3 : endPos;

                    if (AdvancedParkingManager.Instance.FindParkingSpaceForCitizen(
                        returnPos,
                        vehicleData.Info,
                        ref driverInstance,
                        ref driverExtInstance,
                        homeId,
                        targetBuildingId == homeId,
                        vehicleID,
                        allowTourists,
                        out Vector3 parkPos,
                        ref endPosA,
                        out bool calcEndPos)) {
                        calculateEndPos = calcEndPos;
                        allowRandomParking = false;
                        movingToParkingPos = true;

                        if (!extCitizenInstanceManager.CalculateReturnPath(
                                ref driverExtInstance,
                                parkPos,
                                returnPos)) {
                            if (logParkingAi) {
                                Log._DebugFormat(
                                    "CustomPassengerCarAI.ExtStartPathFind({0}): Could not calculate " +
                                    "return path for citizen instance {1}, vehicle {2}. Resetting instance.",
                                    vehicleID,
                                    driverExtInstance.instanceId,
                                    vehicleID);
                            }

                            extCitizenInstanceManager.Reset(ref driverExtInstance);
                            return false;
                        }
                    } else if (driverExtInstance.pathMode ==
                               ExtPathMode.CalculatingCarPathToAltParkPos) {
                        // no alternative parking spot found: abort
                        if (logParkingAi) {
                            Log._DebugFormat(
                                "CustomPassengerCarAI.ExtStartPathFind({0}): No alternative parking " +
                                "spot found for vehicle {1}, citizen instance {2} with CurrentPathMode={3}! " +
                                "GIVING UP.",
                                vehicleID,
                                vehicleID,
                                driverExtInstance.instanceId,
                                driverExtInstance.pathMode);
                        }

                        extCitizenInstanceManager.Reset(ref driverExtInstance);
                        return false;
                    } else {
                        // calculate a direct path to target
                        if (logParkingAi) {
                            Log._DebugFormat(
                                "CustomPassengerCarAI.ExtStartPathFind({0}): No alternative parking " +
                                "spot found for vehicle {1}, citizen instance {2} with CurrentPathMode={3}! " +
                                "Setting CurrentPathMode to 'CalculatingCarPath'.",
                                vehicleID,
                                vehicleID,
                                driverExtInstance.instanceId,
                                driverExtInstance.pathMode);
                        }

                        driverExtInstance.pathMode = ExtPathMode.CalculatingCarPathToTarget;
                    }
                }

                extPathType = driverExtInstance.GetPathType();
                driverExtInstance.atOutsideConnection =
                    Constants.ManagerFactory.ExtCitizenInstanceManager.IsAtOutsideConnection(
                        driverInstanceId,
                        ref driverInstance,
                        ref driverExtInstance,
                        startPos);
            } // end if SavedGameOptions.Instance.ParkingAi4

            var laneTypes = NetInfo.LaneType.Vehicle;

            if (!movingToParkingPos) {
                laneTypes |= NetInfo.LaneType.Pedestrian;

                if (SavedGameOptions.Instance.parkingAI
                    && (driverInstance.m_flags & CitizenInstance.Flags.CannotUseTransport)
                    == CitizenInstance.Flags.None)
                {
                    //---------------------------------
                    // citizen may use public transport
                    //---------------------------------
                    laneTypes |= NetInfo.LaneType.PublicTransport;

                    uint citizenId = driverInstance.m_citizen;
                    if (citizenId != 0u
                        && (citizenManager.m_citizens.m_buffer[citizenId].m_flags & Citizen.Flags.Evacuating) != Citizen.Flags.None)
                    {
                        laneTypes |= NetInfo.LaneType.EvacuationTransport;
                    }
                }
            }

            // NON-STOCK CODE END
            VehicleInfo.VehicleType vehicleTypes = vehicleInfo.m_vehicleType;
            bool allowUnderground = (vehicleData.m_flags & Vehicle.Flags.Underground) != 0;
            bool randomParking = false;
            bool combustionEngine = vehicleInfo.m_class.m_subService == ItemClass.SubService.ResidentialLow;

            if (allowRandomParking && // NON-STOCK CODE
                !movingToParkingPos &&
                targetBuildingId != 0 &&
                (targetBuilding.Info.m_class.m_service > ItemClass.Service.Office
                 || (driverInstance.m_flags & CitizenInstance.Flags.TargetIsNode)
                 != CitizenInstance.Flags.None))
            {
                randomParking = true;
            }

            Log._DebugIf(
                extendedLogParkingAi,
                () => $"CustomPassengerCarAI.ExtStartPathFind({vehicleID}): Requesting path-finding for " +
                $"passenger car {vehicleID}, startPos={startPos}, endPos={endPos}, extPathType={extPathType}");

            // NON-STOCK CODE START
            if (!foundStartingPos) {
                foundStartingPos = PathManager.FindPathPosition(
                    startPos,
                    ItemClass.Service.Road,
                    NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle,
                    vehicleTypes,
                    VehicleInfo.VehicleCategory.All,
                    allowUnderground,
                    false,
                    32f,
                    false,
                    false,
                    out startPosA,
                    out startPosB,
                    out sqrDistA,
                    out float sqrDistB);
            }

            bool foundEndPos = !calculateEndPos || driverInstance.Info.m_citizenAI.FindPathPosition(
                                   driverInstanceId,
                                   ref driverInstance,
                                   endPos,
                                   SavedGameOptions.Instance.parkingAI &&
                                   (targetBuildingId == 0 ||
                                    (targetBuilding.m_flags & Building.Flags.IncomingOutgoing)
                                    == Building.Flags.None)
                                       ? NetInfo.LaneType.Pedestrian
                                       : (laneTypes | NetInfo.LaneType.Pedestrian),
                                   vehicleTypes,
                                   VehicleInfo.VehicleCategory.All,
                                   undergroundTarget,
                                   out endPosA);
            // NON-STOCK CODE END

            if (foundStartingPos && foundEndPos) { // NON-STOCK CODE
                if (!startBothWays || sqrDistA < 10f) {
                    startPosB = default;
                }

                PathUnit.Position endPosB = default;
                SimulationManager simMan = Singleton<SimulationManager>.instance;
                PathUnit.Position dummyPathPos = default;

                // NON-STOCK CODE START
                PathCreationArgs args = new PathCreationArgs {
                    extPathType = extPathType,
                    extVehicleType = ExtVehicleType.PassengerCar,
                    vehicleCategories = VehicleInfo.VehicleCategory.PassengerCar,
                    vehicleId = vehicleID,
                    spawned = (vehicleData.m_flags & Vehicle.Flags.Spawned) != 0,
                    buildIndex = simMan.m_currentBuildIndex,
                    startPosA = startPosA,
                    startPosB = startPosB,
                    endPosA = endPosA,
                    endPosB = endPosB,
                    vehiclePosition = dummyPathPos,
                    laneTypes = laneTypes,
                    vehicleTypes = vehicleTypes,
                    maxLength = 20000f,
                    isHeavyVehicle = isHeavyVehicle,
                    hasCombustionEngine = hasCombustionEngine,
                    ignoreBlocked = ignoreBlocked,
                    ignoreFlooded = false,
                    ignoreCosts = false,
                    randomParking = randomParking,
                    stablePath = false,
                    skipQueue = (vehicleData.m_flags & Vehicle.Flags.Spawned) != 0,
                };

                if (CustomPathManager._instance.CustomCreatePath(
                    out uint path,
                    ref simMan.m_randomizer,
                    args))
                {
                    if (logParkingAi) {
                        Log._DebugFormat(
                            "CustomPassengerCarAI.ExtStartPathFind({0}): Path-finding starts for " +
                            "passenger car {1}, path={2}, startPosA.segment={3}, startPosA.lane={4}, " +
                            "startPosA.offset={5}, startPosB.segment={6}, startPosB.lane={7}, " +
                            "startPosB.offset={8}, laneType={9}, vehicleType={10}, endPosA.segment={11}, " +
                            "endPosA.lane={12}, endPosA.offset={13}, endPosB.segment={14}, endPosB.lane={15}, " +
                            "endPosB.offset={16}",
                            vehicleID,
                            vehicleID,
                            path,
                            startPosA.m_segment,
                            startPosA.m_lane,
                            startPosA.m_offset,
                            startPosB.m_segment,
                            startPosB.m_lane,
                            startPosB.m_offset,
                            laneTypes,
                            vehicleTypes,
                            endPosA.m_segment,
                            endPosA.m_lane,
                            endPosA.m_offset,
                            endPosB.m_segment,
                            endPosB.m_lane,
                            endPosB.m_offset);
                    }

                    // NON-STOCK CODE END
                    if (vehicleData.m_path != 0u) {
                        Singleton<PathManager>.instance.ReleasePath(vehicleData.m_path);
                    }

                    vehicleData.m_path = path;
                    vehicleData.m_flags |= Vehicle.Flags.WaitingPath;
                    return true;
                }
            }

            if (SavedGameOptions.Instance.parkingAI) {
                extCitizenInstanceManager.Reset(ref driverExtInstance);
            }

            return false;
        }

        public bool IsSpaceReservationAllowed(ushort transitNodeId,
                                              PathUnit.Position sourcePos,
                                              PathUnit.Position targetPos)
        {
            if (!SavedGameOptions.Instance.timedLightsEnabled) {
                return true;
            }

            if (TrafficLightSimulationManager.Instance.HasActiveTimedSimulation(transitNodeId)) {
#if DEBUG
                Vehicle dummyVeh = default;
#endif
                TrafficLightSimulationManager.Instance.GetTrafficLightState(
#if DEBUG
                    0,
                    ref dummyVeh,
#endif
                    transitNodeId,
                    sourcePos.m_segment,
                    sourcePos.m_lane,
                    targetPos.m_segment,
                    ref sourcePos.m_segment.ToSegment(),
                    0,
                    out RoadBaseAI.TrafficLightState vehLightState,
                    out RoadBaseAI.TrafficLightState _);

                if (vehLightState == RoadBaseAI.TrafficLightState.Red) {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Checks for traffic lights and priority signs when changing segments (for rail vehicles).
        ///     Sets the maximum allowed speed <paramref name="maxSpeed"/> if segment change is not
        ///     allowed (otherwise <paramref name="maxSpeed"/> has to be set by the calling method).
        /// </summary>
        /// <param name="frontVehicleId">vehicle id</param>
        /// <param name="vehicleData">vehicle data</param>
        /// <param name="sqrVelocity">last frame squared velocity</param>
        /// <param name="prevPos">previous path position</param>
        /// <param name="prevTargetNodeId">previous target node</param>
        /// <param name="prevLaneID">previous lane</param>
        /// <param name="position">current path position</param>
        /// <param name="targetNodeId">transit node</param>
        /// <param name="laneID">current lane</param>
        /// <returns>true, if the vehicle may change segments, false otherwise.</returns>
        public bool MayChangeSegment(ushort frontVehicleId,
                                     ref Vehicle vehicleData,
                                     float sqrVelocity,
                                     ref PathUnit.Position prevPos,
                                     ref NetSegment prevSegment,
                                     ushort prevTargetNodeId,
                                     uint prevLaneID,
                                     ref PathUnit.Position position,
                                     ushort targetNodeId,
                                     ref NetNode targetNode,
                                     uint laneID,
                                     out float maxSpeed) {
            VehicleJunctionTransitState transitState = MayChangeSegment(
                frontVehicleId,
                ref Constants.ManagerFactory.ExtVehicleManager.ExtVehicles[frontVehicleId],
                ref vehicleData,
                sqrVelocity,
                ref prevPos,
                ref prevSegment,
                prevTargetNodeId,
                prevLaneID,
                ref position,
                targetNodeId,
                ref targetNode,
                laneID,
                ref DUMMY_POS,
                0,
                out maxSpeed);

            Constants.ManagerFactory.ExtVehicleManager.SetJunctionTransitState(
                ref Constants.ManagerFactory.ExtVehicleManager.ExtVehicles[frontVehicleId],
                transitState);

            return transitState ==
                   VehicleJunctionTransitState
                       .Leave /* || transitState == VehicleJunctionTransitState.Blocked*/;
        }

        /// <summary>
        /// Checks for traffic lights and priority signs when changing segments (for road & rail
        /// vehicles). Sets the maximum allowed speed <paramref name="maxSpeed"/> if segment change
        /// is not allowed (otherwise <paramref name="maxSpeed"/> has to be set by the calling method).
        /// </summary>
        /// <param name="frontVehicleId">vehicle id</param>
        /// <param name="vehicleData">vehicle data</param>
        /// <param name="sqrVelocity">last frame squared velocity</param>
        /// <param name="prevPos">previous path position</param>
        /// <param name="prevTargetNodeId">previous target node</param>
        /// <param name="prevLaneID">previous lane</param>
        /// <param name="position">current path position</param>
        /// <param name="targetNodeId">transit node</param>
        /// <param name="laneID">current lane</param>
        /// <param name="nextPosition">next path position</param>
        /// <param name="nextTargetNodeId">next target node</param>
        /// <returns>true, if the vehicle may change segments, false otherwise.</returns>
        public bool MayChangeSegment(ushort frontVehicleId,
                                     ref Vehicle vehicleData,
                                     float sqrVelocity,
                                     ref PathUnit.Position prevPos,
                                     ref NetSegment prevSegment,
                                     ushort prevTargetNodeId,
                                     uint prevLaneID,
                                     ref PathUnit.Position position,
                                     ushort targetNodeId,
                                     ref NetNode targetNode,
                                     uint laneID,
                                     ref PathUnit.Position nextPosition,
                                     ushort nextTargetNodeId,
                                     out float maxSpeed) {
            VehicleJunctionTransitState transitState = MayChangeSegment(
                frontVehicleId,
                ref Constants.ManagerFactory.ExtVehicleManager.ExtVehicles[frontVehicleId],
                ref vehicleData,
                sqrVelocity,
                ref prevPos,
                ref prevSegment,
                prevTargetNodeId,
                prevLaneID,
                ref position,
                targetNodeId,
                ref targetNode,
                laneID,
                ref nextPosition,
                nextTargetNodeId,
                out maxSpeed);

            Constants.ManagerFactory.ExtVehicleManager.SetJunctionTransitState(
                ref Constants.ManagerFactory.ExtVehicleManager.ExtVehicles[frontVehicleId],
                transitState);

            return transitState == VehicleJunctionTransitState.Leave;
            // || transitState == VehicleJunctionTransitState.Blocked
        }

        protected VehicleJunctionTransitState MayChangeSegment(
            ushort frontVehicleId,
            ref ExtVehicle extVehicle,
            ref Vehicle vehicleData,
            float sqrVelocity,
            ref PathUnit.Position prevPos,
            ref NetSegment prevSegment,
            ushort prevTargetNodeId,
            uint prevLaneId,
            ref PathUnit.Position position,
            ushort targetNodeId,
            ref NetNode targetNode,
            uint laneId,
            ref PathUnit.Position nextPosition,
            ushort nextTargetNodeId,
            out float maxSpeed)
        {
#if DEBUG
            bool logPriority = DebugSwitch.PriorityRules.Get() &&
                         (DebugSettings.NodeId <= 0 || targetNodeId == DebugSettings.NodeId);
#else
            const bool logPriority = false;
#endif
            maxSpeed = 0;
            if (prevTargetNodeId != targetNodeId
                || (vehicleData.m_blockCounter == 255
                    && !MayDespawn(frontVehicleId, ref vehicleData)) // NON-STOCK CODE
                ) {
                // method should only be called if targetNodeId == prevTargetNode
                return VehicleJunctionTransitState.Leave;
            }

            if (extVehicle.junctionTransitState == VehicleJunctionTransitState.Leave) {
                // vehicle was already allowed to leave the junction
                if (sqrVelocity <= GlobalConfig.Instance.PriorityRules.MaxStopVelocity *
                    GlobalConfig.Instance.PriorityRules.MaxStopVelocity &&
                    (extVehicle.vehicleType & ExtVehicleType.RoadVehicle) != ExtVehicleType.None)
                {
                    // vehicle is not moving. reset allowance to leave junction
                    if (logPriority) {
                        Log._Debug(
                            $"VehicleBehaviorManager.MayChangeSegment({frontVehicleId}): Setting " +
                            "JunctionTransitState from LEAVE to BLOCKED (speed to low)");
                    }

                    return VehicleJunctionTransitState.Blocked;
                }

                // allow for re-checking if vehicle still has priority (might be expensive)
                if (SavedGameOptions.Instance.simulationAccuracy < SimulationAccuracy.VeryHigh) {
                    return VehicleJunctionTransitState.Leave;
                }
            }

            uint currentFrameIndex = Singleton<SimulationManager>.instance.m_currentFrameIndex;
            if ((extVehicle.junctionTransitState == VehicleJunctionTransitState.Stop
                 || extVehicle.junctionTransitState == VehicleJunctionTransitState.Blocked)
                && extVehicle.lastTransitStateUpdate >> ExtVehicleManager.JUNCTION_RECHECK_SHIFT
                >= currentFrameIndex >> ExtVehicleManager.JUNCTION_RECHECK_SHIFT)
            {
                // reuse recent result
                return extVehicle.junctionTransitState;
            }

            bool isRecklessDriver = extVehicle.recklessDriver;
            var netManager = Singleton<NetManager>.instance;
            // IExtVehicleManager extVehicleMan = Constants.ManagerFactory.ExtVehicleManager;
            bool hasActiveTimedSimulation = (SavedGameOptions.Instance.timedLightsEnabled &&
                                             TrafficLightSimulationManager
                                                 .Instance.HasActiveTimedSimulation(targetNodeId));
            NetNode.FlagsLong targetNodeFlagsLong = targetNode.flags;
            bool hasTrafficLightFlag = (targetNodeFlagsLong & NetNode.FlagsLong.TrafficLights) != NetNode.FlagsLong.None;

            if (hasActiveTimedSimulation && !hasTrafficLightFlag) {
                TrafficLightManager.Instance.AddTrafficLight(targetNodeId, ref targetNode);
            }

            bool hasTrafficLight = hasTrafficLightFlag || hasActiveTimedSimulation;
            bool checkTrafficLights = true;
            bool isTargetStartNode = prevSegment.m_startNode == targetNodeId;
            bool isLevelCrossing = (targetNodeFlagsLong & NetNode.FlagsLong.LevelCrossing) != NetNode.FlagsLong.None;

            if ((vehicleData.Info.m_vehicleType &
                 (VehicleInfo.VehicleType.Train | VehicleInfo.VehicleType.Metro |
                  VehicleInfo.VehicleType.Monorail)) == VehicleInfo.VehicleType.None) {
                // check if to check space
                Log._DebugIf(
                    logPriority,
                    () => $"CustomVehicleAI.MayChangeSegment: Vehicle {frontVehicleId} is not a train.");

                // stock priority signs
                if ((vehicleData.m_flags & Vehicle.Flags.Emergency2) == 0
                    && ((NetLane.Flags)prevLaneId.ToLane().m_flags &
                        (NetLane.Flags.YieldStart | NetLane.Flags.YieldEnd)) != NetLane.Flags.None
                    && (targetNode.m_flags & (NetNode.Flags.Junction | NetNode.Flags.TrafficLights |
                                              NetNode.Flags.OneWayIn)) == NetNode.Flags.Junction)
                {
                    if (vehicleData.Info.m_vehicleType == VehicleInfo.VehicleType.Tram ||
                        vehicleData.Info.m_vehicleType == VehicleInfo.VehicleType.Train ||
                        vehicleData.Info.m_vehicleType == VehicleInfo.VehicleType.Trolleybus)
                    {
                        if ((vehicleData.m_flags2 & Vehicle.Flags2.Yielding) == 0)
                        {
                            if (sqrVelocity < 0.01f) {
                                vehicleData.m_flags2 |= Vehicle.Flags2.Yielding;
                            }

                            return VehicleJunctionTransitState.Stop;
                        }

                        vehicleData.m_waitCounter = (byte)Mathf.Min(
                            vehicleData.m_waitCounter + 1,
                            4);

                        if (vehicleData.m_waitCounter < 4) {
                            return VehicleJunctionTransitState.Stop;
                        }

                        vehicleData.m_flags2 &= ~Vehicle.Flags2.Yielding;
                        vehicleData.m_waitCounter = 0;
                    } else if (sqrVelocity > 0.01f) {
                        return VehicleJunctionTransitState.Stop;
                    }
                }

                // entering blocked junctions
                if (MustCheckSpace(prevPos.m_segment, isTargetStartNode, ref targetNode, isRecklessDriver)) {
                    // check if there is enough space
                    var len = extVehicle.totalLength + 4f;

                    ref NetLane netLane = ref laneId.ToLane();

                    if (!netLane.CheckSpace(len)) {
                        var sufficientSpace = false;
                        if (nextPosition.m_segment != 0 && netLane.m_length < 30f)
                        {
                            ref NetNode nextTargetNetNode = ref nextTargetNodeId.ToNode();
                            NetNode.Flags nextTargetNodeFlags = nextTargetNetNode.m_flags;

                            if ((nextTargetNodeFlags &
                                 (NetNode.Flags.Junction | NetNode.Flags.OneWayOut |
                                  NetNode.Flags.OneWayIn)) != NetNode.Flags.Junction ||
                                nextTargetNetNode.CountSegments() == 2)
                            {
                                uint nextLaneId = PathManager.GetLaneID(nextPosition);
                                if (nextLaneId != 0u) {
                                    sufficientSpace = nextLaneId.ToLane().CheckSpace(len);
                                }
                            }
                        }

                        if (!sufficientSpace) {
                            Log._DebugIf(
                                logPriority,
                                () => $"Vehicle {frontVehicleId}: Setting JunctionTransitState to BLOCKED");
                            return VehicleJunctionTransitState.Blocked;
                        }
                    }
                }

                bool isJoinedJunction =
                    ((NetLane.Flags)prevLaneId.ToLane().m_flags &
                     NetLane.Flags.JoinedJunction) != NetLane.Flags.None;

                checkTrafficLights = !isJoinedJunction || isLevelCrossing;
            } else {
                Log._DebugIf(
                    logPriority,
                    () => $"CustomVehicleAI.MayChangeSegment: Vehicle {frontVehicleId} is " +
                    "a train/metro/monorail.");

                switch (vehicleData.Info.m_vehicleType) {
                    case VehicleInfo.VehicleType.Monorail:
                        // vanilla traffic light flags are not rendered on monorail tracks
                        checkTrafficLights = hasActiveTimedSimulation;
                        break;
                    case VehicleInfo.VehicleType.Train:
                        // vanilla traffic light flags are not rendered on train tracks, except for level crossings
                        checkTrafficLights = hasActiveTimedSimulation || isLevelCrossing;
                        break;
                }
            }

            IExtSegmentEndManager segEndMan = Constants.ManagerFactory.ExtSegmentEndManager;
            VehicleJunctionTransitState transitState = extVehicle.junctionTransitState;

            if (extVehicle.junctionTransitState == VehicleJunctionTransitState.Blocked) {
                Log._DebugIf(
                    logPriority,
                    () => $"Vehicle {frontVehicleId}: Setting JunctionTransitState from BLOCKED to APPROACH");

                transitState = VehicleJunctionTransitState.Approach;
            }

            ITrafficPriorityManager prioMan = TrafficPriorityManager.Instance;
            CustomSegmentLightsManager segLightsMan = CustomSegmentLightsManager.Instance;

            if ((vehicleData.m_flags & Vehicle.Flags.Emergency2) == 0 || isLevelCrossing) {
                if (hasTrafficLight && checkTrafficLights) {
                    Log._DebugIf(
                        logPriority,
                        () => $"VehicleBehaviorManager.MayChangeSegment({frontVehicleId}): Node " +
                        $"{targetNodeId} has a traffic light.");

                    bool stopCar = false;
                    uint simGroup = (uint)targetNodeId >> 7;

                    TrafficLightSimulationManager.Instance.GetTrafficLightState(
#if DEBUG
                        frontVehicleId,
                        ref vehicleData,
#endif
                        targetNodeId,
                        prevPos.m_segment,
                        prevPos.m_lane,
                        position.m_segment,
                        ref prevSegment,
                        currentFrameIndex - simGroup,
                        out RoadBaseAI.TrafficLightState vehicleLightState,
                        out RoadBaseAI.TrafficLightState pedestrianLightState,
                        out bool vehicles,
                        out bool pedestrians);
                    // TODO current frame index or reference frame index?

                    if (vehicleData.Info.m_vehicleType == VehicleInfo.VehicleType.Car &&
                        isRecklessDriver && !isLevelCrossing) {
                        vehicleLightState = RoadBaseAI.TrafficLightState.Green;
                    }

                    Log._DebugIf(
                        logPriority,
                        () => $"VehicleBehaviorManager.MayChangeSegment({frontVehicleId}): Vehicle " +
                              $"{frontVehicleId} has TL state {vehicleLightState} at node {targetNodeId} " +
                              $"(recklessDriver={isRecklessDriver})");

                    uint random = currentFrameIndex - simGroup & 255u;
                    if (!vehicles && random >= 196u) {
                        vehicles = true;
                        RoadBaseAI.SetTrafficLightState(
                            targetNodeId,
                            ref prevSegment,
                            currentFrameIndex - simGroup,
                            vehicleLightState,
                            pedestrianLightState,
                            vehicles,
                            pedestrians);
                    }

                    switch (vehicleLightState) {
                        case RoadBaseAI.TrafficLightState.RedToGreen: {
                            if (random < 60u) {
                                stopCar = true;
                            }

                            break;
                        }

                        case RoadBaseAI.TrafficLightState.Red: {
                            stopCar = true;
                            break;
                        }

                        case RoadBaseAI.TrafficLightState.GreenToRed: {
                            if (random >= 30u) {
                                stopCar = true;
                            }

                            break;
                        }
                    }

#if TURNONRED
                    // Check if turning in the preferred direction, and if turning while it's red is allowed
                    if (stopCar && sqrVelocity <= TrafficPriorityManager.MAX_SQR_STOP_VELOCITY &&
                        JunctionRestrictionsManager.Instance.IsTurnOnRedAllowed(
                            prevPos.m_segment,
                            isTargetStartNode)) {
                        SegmentGeometry currentSegGeo = SegmentGeometry.Get(prevPos.m_segment);
                        SegmentEndGeometry currentSegEndGeo = currentSegGeo.GetEnd(targetNodeId);
                        ArrowDirection targetDir =
                            currentSegEndGeo.GetDirection(position.m_segment);
                        bool lhd = Services.SimulationService.LeftHandDrive;
                        if (lhd && targetDir == ArrowDirection.Left ||
                            !lhd && targetDir == ArrowDirection.Right) {
#if DEBUG
                            if (debug)
                                Log._Debug(
                                    $"VehicleBehaviorManager.MayChangeSegment({frontVehicleId}): Vehicle may turn on red to target segment {position.m_segment}, lane {position.m_lane}");
#endif
                            stopCar = false;
                        }
                    }
#endif

                    // Turn-on-red: Check if turning in the preferred direction, and if turning while it's red is allowed
                    if (SavedGameOptions.Instance.turnOnRedEnabled
                        && stopCar
                        && (extVehicle.vehicleType & ExtVehicleType.RoadVehicle) != ExtVehicleType.None
                        && sqrVelocity <= GlobalConfig.Instance.PriorityRules.MaxYieldVelocity
                            * GlobalConfig.Instance.PriorityRules.MaxYieldVelocity
                        && !isRecklessDriver)
                    {
                        IJunctionRestrictionsManager junctionRestrictionsManager
                            = Constants.ManagerFactory.JunctionRestrictionsManager;
                        ITurnOnRedManager turnOnRedMan = Constants.ManagerFactory.TurnOnRedManager;
                        int torIndex = turnOnRedMan.GetIndex(prevPos.m_segment, isTargetStartNode);

                        if ((turnOnRedMan.TurnOnRedSegments[torIndex].leftSegmentId ==
                             position.m_segment
                             && junctionRestrictionsManager.IsTurnOnRedAllowed(
                                 Shortcuts.LHT,
                                 prevPos.m_segment,
                                 isTargetStartNode))
                            || (turnOnRedMan.TurnOnRedSegments[torIndex].rightSegmentId ==
                                position.m_segment
                                && junctionRestrictionsManager.IsTurnOnRedAllowed(
                                    !Shortcuts.LHT,
                                    prevPos.m_segment,
                                    isTargetStartNode)))
                        {
                            if (logPriority) {
                                Log._Debug(
                                    $"VehicleBehaviorManager.MayChangeSegment({frontVehicleId}): " +
                                    $"Vehicle may turn on red to target segment {position.m_segment}, " +
                                    $"lane {position.m_lane}");
                            }

                            stopCar = false;
                        }
                    }

                    // check priority rules at unprotected traffic lights
                    if (!stopCar && SavedGameOptions.Instance.prioritySignsEnabled &&
                        SavedGameOptions.Instance.trafficLightPriorityRules && segLightsMan.IsSegmentLight(
                            prevPos.m_segment,
                            isTargetStartNode))
                    {
                        bool hasPriority = prioMan.HasPriority(
                            frontVehicleId,
                            ref vehicleData,
                            ref prevPos,
                            ref segEndMan.ExtSegmentEnds[segEndMan.GetIndex(prevPos.m_segment, isTargetStartNode)],
                            targetNodeId,
                            isTargetStartNode,
                            ref position,
                            ref targetNode);

                        if (!hasPriority) {
                            // green light but other cars are incoming and they have priority: stop
                            Log._DebugIf(
                                logPriority,
                                () => $"VehicleBehaviorManager.MayChangeSegment({frontVehicleId}): " +
                                "Green traffic light (or turn on red allowed) but detected traffic with " +
                                "higher priority: stop.");

                            stopCar = true;
                        }
                    }

                    if (stopCar) {
                        Log._DebugIf(
                            logPriority,
                            () => $"VehicleBehaviorManager.MayChangeSegment({frontVehicleId}): Setting " +
                            "JunctionTransitState to STOP");

                        if (vehicleData.Info.m_vehicleType == VehicleInfo.VehicleType.Tram ||
                            vehicleData.Info.m_vehicleType == VehicleInfo.VehicleType.Train ||
                            vehicleData.Info.m_vehicleType == VehicleInfo.VehicleType.Trolleybus)
                        {
                            vehicleData.m_flags2 |= Vehicle.Flags2.Yielding;
                            vehicleData.m_waitCounter = 0;
                        }

                        vehicleData.m_blockCounter = 0;
                        return VehicleJunctionTransitState.Stop;
                    }

                    Log._DebugIf(
                        logPriority,
                        () => $"VehicleBehaviorManager.MayChangeSegment({frontVehicleId}): " +
                              $"Setting JunctionTransitState to LEAVE ({vehicleLightState})");

                    if (vehicleData.Info.m_vehicleType == VehicleInfo.VehicleType.Tram ||
                        vehicleData.Info.m_vehicleType == VehicleInfo.VehicleType.Train ||
                        vehicleData.Info.m_vehicleType == VehicleInfo.VehicleType.Trolleybus)
                    {
                        vehicleData.m_flags2 &= ~Vehicle.Flags2.Yielding;
                        vehicleData.m_waitCounter = 0;
                    }

                    return VehicleJunctionTransitState.Leave;
                }

                if (SavedGameOptions.Instance.prioritySignsEnabled && vehicleData.Info.m_vehicleType !=
                    VehicleInfo.VehicleType.Monorail)
                {
                    if (logPriority) {
                        Log._DebugFormat(
                            "VehicleBehaviorManager.MayChangeSegment({0}): Vehicle is arriving @ seg. " +
                            "{1} ({2}, {3}), node {4} which is not a traffic light.",
                            frontVehicleId,
                            prevPos.m_segment,
                            position.m_segment,
                            nextPosition.m_segment,
                            targetNodeId);
                    }

                    var sign = prioMan.GetPrioritySign(prevPos.m_segment, isTargetStartNode);

                    if (sign != PriorityType.None && sign != PriorityType.Main) {
                        if (logPriority) {
                            Log._DebugFormat(
                                "VehicleBehaviorManager.MayChangeSegment({0}): Vehicle is arriving " +
                                "@ seg. {1} ({2}, {3}), node {4} which is not a traffic light and is " +
                                "a priority segment.\nVehicleBehaviorManager.MayChangeSegment({5}): " +
                                "JunctionTransitState={6}",
                                frontVehicleId,
                                prevPos.m_segment,
                                position.m_segment,
                                nextPosition.m_segment,
                                targetNodeId,
                                frontVehicleId,
                                transitState);
                        }

                        if (transitState == VehicleJunctionTransitState.None) {
                            Log._DebugIf(
                                logPriority,
                                () => $"VehicleBehaviorManager.MayChangeSegment({frontVehicleId}): " +
                                      "Setting JunctionTransitState to APPROACH (prio)");

                            transitState = VehicleJunctionTransitState.Approach;
                        }

                        if (sign == PriorityType.Stop) {
                            if (transitState == VehicleJunctionTransitState.Approach) {
                                extVehicle.waitTime = 0;
                            }

                            if (sqrVelocity <= GlobalConfig.Instance.PriorityRules.MaxStopVelocity
                                * GlobalConfig.Instance.PriorityRules.MaxStopVelocity)
                            {
                                ++extVehicle.waitTime;

                                if (extVehicle.waitTime < 2) {
                                    vehicleData.m_blockCounter = 0;
                                    return VehicleJunctionTransitState.Stop;
                                }
                            } else {
                                Log._DebugIf(
                                    logPriority,
                                    () => $"VehicleBehaviorManager.MayChangeSegment({frontVehicleId}): " +
                                          "Vehicle has come to a full stop.");

                                vehicleData.m_blockCounter = 0;
                                return VehicleJunctionTransitState.Stop;
                            }
                        }

                        if (sqrVelocity <= GlobalConfig.Instance.PriorityRules.MaxYieldVelocity
                            * GlobalConfig.Instance.PriorityRules.MaxYieldVelocity) {
                            if (logPriority) {
                                Log._DebugFormat(
                                    "VehicleBehaviorManager.MayChangeSegment({0}): {1} sign. waittime={2}",
                                    frontVehicleId,
                                    sign,
                                    extVehicle.waitTime);
                            }

                            //skip checking of priority if simAccuracy on lowest settings
                            if (SavedGameOptions.Instance.simulationAccuracy <= SimulationAccuracy.VeryLow) {
                                return VehicleJunctionTransitState.Leave;
                            }

                            if (extVehicle.waitTime <
                                GlobalConfig.Instance.PriorityRules.MaxPriorityWaitTime) {
                                extVehicle.waitTime++;

                                Log._DebugIf(
                                    logPriority,
                                    () =>
                                        $"VehicleBehaviorManager.MayChangeSegment({frontVehicleId}): " +
                                        "Setting JunctionTransitState to STOP (wait)");

                                bool hasPriority = prioMan.HasPriority(
                                    frontVehicleId,
                                    ref vehicleData,
                                    ref prevPos,
                                    ref segEndMan.ExtSegmentEnds[
                                        segEndMan.GetIndex(
                                            prevPos.m_segment,
                                            isTargetStartNode)],
                                    targetNodeId,
                                    isTargetStartNode,
                                    ref position,
                                    ref targetNode);

                                Log._DebugIf(
                                    logPriority,
                                    () =>
                                        $"VehicleBehaviorManager.MayChangeSegment({frontVehicleId}): " +
                                        $"hasPriority: {hasPriority}");

                                if (!hasPriority) {
                                    vehicleData.m_blockCounter = 0;
                                    return VehicleJunctionTransitState.Stop;
                                }

                                Log._DebugIf(
                                    logPriority,
                                    () =>
                                        $"VehicleBehaviorManager.MayChangeSegment({frontVehicleId}): " +
                                        "Setting JunctionTransitState to LEAVE (no conflicting cars)");
                                return VehicleJunctionTransitState.Leave;
                            }

                            Log._DebugIf(
                                logPriority,
                                () =>
                                    $"VehicleBehaviorManager.MayChangeSegment({frontVehicleId}): " +
                                    "Setting JunctionTransitState to LEAVE (max wait timeout)");
                            return VehicleJunctionTransitState.Leave;
                        }

                        Log._DebugIf(
                            logPriority,
                            () => $"VehicleBehaviorManager.MayChangeSegment({frontVehicleId}): " +
                                  $"Vehicle has not yet reached yield speed (sqrVelocity={sqrVelocity})");

                        //slow down to target speed
                        maxSpeed = GlobalConfig.Instance.PriorityRules.MaxYieldVelocity;
                        // vehicle has not yet reached yield speed
                        return VehicleJunctionTransitState.Stop;
                    }

                    return VehicleJunctionTransitState.Leave;
                }

                return VehicleJunctionTransitState.Leave;
            }

            return VehicleJunctionTransitState.Leave;
        }

        /// <summary>
        /// Checks if a vehicle must check if the subsequent segment is empty while going from
        ///     segment <paramref name="segmentId"/>  through node <paramref name="startNode"/>.
        /// </summary>
        /// <param name="segmentId">source segment id</param>
        /// <param name="startNode">is transit node start node of source segment?</param>
        /// <param name="node">transit node</param>
        /// <param name="isRecklessDriver">reckless driver?</param>
        /// <returns></returns>
        protected bool MustCheckSpace(ushort segmentId,
                                      bool startNode,
                                      ref NetNode node,
                                      bool isRecklessDriver) {
            bool checkSpace;
            if (isRecklessDriver) {
                checkSpace = (node.m_flags & NetNode.Flags.LevelCrossing) != NetNode.Flags.None;
            } else {
                if (SavedGameOptions.Instance.junctionRestrictionsEnabled) {
                    checkSpace =
                        !JunctionRestrictionsManager.Instance.IsEnteringBlockedJunctionAllowed(
                            segmentId,
                            startNode);
                } else {
                    checkSpace =
                        (node.m_flags & (NetNode.Flags.Junction
                                         | NetNode.Flags.OneWayOut
                                         | NetNode.Flags.OneWayIn)) == NetNode.Flags.Junction
                        && node.CountSegments() != 2;
                }
            }

            return checkSpace;
        }

        public bool MayDespawn(ushort vehicleId, ref Vehicle vehicleData) {
            return !SavedGameOptions.Instance.disableDespawning
                   || ((vehicleData.m_flags2 & (Vehicle.Flags2.Blown
                                                | Vehicle.Flags2.Floating)) != 0)
                   || (vehicleData.m_flags & Vehicle.Flags.Parking) != 0
                   || GlobalConfig.Instance.Gameplay.AllowedDespawnVehicleTypes.IsFlagSet(ExtVehicleManager.Instance.ExtVehicles[vehicleId].vehicleType);
        }

        public float CalcMaxSpeed(ushort vehicleId,
                                  ref ExtVehicle extVehicle,
                                  VehicleInfo vehicleInfo,
                                  PathUnit.Position position,
                                  ref NetSegment segment,
                                  Vector3 pos,
                                  float maxSpeed,
                                  bool emergency)
        {
            if (Singleton<NetManager>.instance.m_treatWetAsSnow) {
                DistrictManager districtManager = Singleton<DistrictManager>.instance;
                byte district = districtManager.GetDistrict(pos);
                DistrictPolicies.CityPlanning cityPlanningPolicies =
                    districtManager.m_districts.m_buffer[district].m_cityPlanningPolicies;

                if ((cityPlanningPolicies & DistrictPolicies.CityPlanning.StuddedTires) !=
                    DistrictPolicies.CityPlanning.None)
                {
                    if (SavedGameOptions.Instance.strongerRoadConditionEffects) {
                        if (maxSpeed > ICY_ROADS_STUDDED_MIN_SPEED)
                        {
                            maxSpeed = ICY_ROADS_STUDDED_MIN_SPEED + ((255 - segment.m_wetness) *
                                       0.0039215686f * (maxSpeed - ICY_ROADS_STUDDED_MIN_SPEED));
                        }
                    } else {
                        maxSpeed *= 1f - (segment.m_wetness * (1f / 1700f)); // vanilla: -15% .. ±0%
                    }

                    districtManager.m_districts.m_buffer[district].m_cityPlanningPoliciesEffect
                        |= DistrictPolicies.CityPlanning.StuddedTires;
                } else {
                    if (SavedGameOptions.Instance.strongerRoadConditionEffects) {
                        if (maxSpeed > ICY_ROADS_MIN_SPEED) {
                            maxSpeed = ICY_ROADS_MIN_SPEED + ((255 - segment.m_wetness) *
                                       0.0039215686f * (maxSpeed - ICY_ROADS_MIN_SPEED));
                        }
                    } else {
                        maxSpeed *= 1f - (segment.m_wetness * (1f / 850f)); // vanilla: -30% .. ±0%
                    }
                }
            } else {
                if (SavedGameOptions.Instance.strongerRoadConditionEffects) {
                    float minSpeed = Math.Min(maxSpeed * WET_ROADS_FACTOR, WET_ROADS_MAX_SPEED); // custom: -25% .. 0
                    if (maxSpeed > minSpeed) {
                        maxSpeed = minSpeed + ((255 - segment.m_wetness) * 0.0039215686f *
                                   (maxSpeed - minSpeed));
                    }
                } else {
                    maxSpeed *= 1f - (segment.m_wetness * (1f / 1700f)); // vanilla: -15% .. ±0%
                }
            }

            if (SavedGameOptions.Instance.strongerRoadConditionEffects) {
                float minSpeed = Math.Min(maxSpeed * BROKEN_ROADS_FACTOR, BROKEN_ROADS_MAX_SPEED);
                if (maxSpeed > minSpeed) {
                    maxSpeed = minSpeed + (segment.m_condition * (1f / 255f) * (maxSpeed - minSpeed));
                }
            } else {
                maxSpeed *= 1f + (segment.m_condition * (1f / 1700f)); // vanilla: ±0% .. +15 %
            }

            maxSpeed = ApplyRealisticSpeeds(maxSpeed, vehicleId, ref extVehicle, vehicleInfo);
            maxSpeed = Math.Max(MIN_SPEED, maxSpeed); // at least 10 km/h

            return maxSpeed;
        }

        public float ApplyRealisticSpeeds(float speed,
                                          ushort vehicleId,
                                          ref ExtVehicle extVehicle,
                                          VehicleInfo vehicleInfo) {
            if (SavedGameOptions.Instance.individualDrivingStyle) {
                float vehicleRand =
                    0.01f * Constants.ManagerFactory.ExtVehicleManager.GetTimedVehicleRand(
                        vehicleId);
                if (vehicleInfo.m_isLargeVehicle) {
                    speed *= 0.75f + (vehicleRand * 0.25f); // a little variance, 0.75 .. 1
                } else if (extVehicle.recklessDriver) {
                    speed *= 1.3f + (vehicleRand * 1.7f); // woohooo, 1.3 .. 3
                } else {
                    speed *= 0.8f + (vehicleRand * 0.5f); // a little variance, 0.8 .. 1.3
                }
            } else if (extVehicle.recklessDriver) {
                speed *= 1.5f;
            }

            return speed;
        }

        public bool IsRecklessDriver(ushort vehicleId, ref Vehicle vehicleData) {
            if ((vehicleData.m_flags & Vehicle.Flags.Emergency2) != 0) {
                return true;
            }

            if (SavedGameOptions.Instance.evacBussesMayIgnoreRules &&
                vehicleData.Info.GetService() == ItemClass.Service.Disaster) {
                return true;
            }

            if (SavedGameOptions.Instance.recklessDrivers == RecklessDrivers.HolyCity) {
                return false;
            }

            if ((vehicleData.Info.m_vehicleType & RECKLESS_VEHICLE_TYPES) ==
                VehicleInfo.VehicleType.None) {
                return false;
            }

            return (uint)vehicleId % SavedGameOptions.Instance.getRecklessDriverModulo() == 0;
        }

        public int FindBestLane(ushort vehicleId,
                                ref Vehicle vehicleData,
                                ref ExtVehicle vehicleState,
                                uint currentLaneId,
                                PathUnit.Position currentPathPos,
                                NetInfo currentSegInfo,
                                PathUnit.Position next1PathPos,
                                NetInfo next1SegInfo,
                                PathUnit.Position next2PathPos,
                                NetInfo next2SegInfo,
                                PathUnit.Position next3PathPos,
                                NetInfo next3SegInfo,
                                PathUnit.Position next4PathPos)
        {
            try {
                // GlobalConfig conf = GlobalConfig.Instance;
#if DEBUG
                bool logLaneSelection = false;
                if (DebugSwitch.AlternativeLaneSelection.Get()) {
                    ref NetSegment netSegment = ref currentPathPos.m_segment.ToSegment();
                    ushort nodeId = currentPathPos.m_offset < 128
                        ? netSegment.m_startNode
                        : netSegment.m_endNode;
                    logLaneSelection =
                        (DebugSettings.VehicleId == 0 || DebugSettings.VehicleId == vehicleId)
                        && (DebugSettings.NodeId == 0 || DebugSettings.NodeId == nodeId);
                }
#else
                const bool logLaneSelection = false;
#endif

                if (logLaneSelection) {
                    Log._DebugFormat(
                        "VehicleBehaviorManager.FindBestLane({0}): currentLaneId={1}, currentPathPos=[seg={2}, " +
                        "lane={3}, off={4}] next1PathPos=[seg={5}, lane={6}, off={7}] next2PathPos=[seg={8}, " +
                        "lane={9}, off={10}] next3PathPos=[seg={11}, lane={12}, off={13}] " +
                        "next4PathPos=[seg={14}, lane={15}, off={16}]",
                        vehicleId,
                        currentLaneId,
                        currentPathPos.m_segment,
                        currentPathPos.m_lane,
                        currentPathPos.m_offset,
                        next1PathPos.m_segment,
                        next1PathPos.m_lane,
                        next1PathPos.m_offset,
                        next2PathPos.m_segment,
                        next2PathPos.m_lane,
                        next2PathPos.m_offset,
                        next3PathPos.m_segment,
                        next3PathPos.m_lane,
                        next3PathPos.m_offset,
                        next4PathPos.m_segment,
                        next4PathPos.m_lane,
                        next4PathPos.m_offset);
                }

                if (!vehicleState.dlsReady) {
                    Constants.ManagerFactory.ExtVehicleManager.UpdateDynamicLaneSelectionParameters(ref vehicleState);
                }

                if (vehicleState.lastAltLaneSelSegmentId == currentPathPos.m_segment) {
                    Log._DebugIf(
                        logLaneSelection,
                        () => $"VehicleBehaviorManager.FindBestLane({vehicleId}): Skipping " +
                        "alternative lane selection: Already calculated.");

                    return next1PathPos.m_lane;
                }

                vehicleState.lastAltLaneSelSegmentId = currentPathPos.m_segment;

                bool recklessDriver = vehicleState.recklessDriver;
                float maxReservedSpace = vehicleState.maxReservedSpace;

                // cur -> next1
                float vehicleLength = 1f + vehicleState.totalLength;
                bool startNode = currentPathPos.m_offset < 128;
                uint currentFwdRoutingIndex =
                    RoutingManager.Instance.GetLaneEndRoutingIndex(currentLaneId, startNode);

#if DEBUG
                if (currentFwdRoutingIndex >= RoutingManager.Instance.LaneEndForwardRoutings.Length) {
                    Log.Error(
                        $"Invalid array index: currentFwdRoutingIndex={currentFwdRoutingIndex}, " +
                        "RoutingManager.Instance.laneEndForwardRoutings.Length=" +
                        $"{RoutingManager.Instance.LaneEndForwardRoutings.Length} " +
                        $"(currentLaneId={currentLaneId}, startNode={startNode})");
                }
#endif

                if (!RoutingManager.Instance.LaneEndForwardRoutings[currentFwdRoutingIndex].routed) {
                    Log._DebugIf(
                        logLaneSelection,
                        () => $"VehicleBehaviorManager.FindBestLane({vehicleId}): No forward routing " +
                              "for next path position available.");

                    return next1PathPos.m_lane;
                }

                LaneTransitionData[] currentFwdTransitions
                    = RoutingManager.Instance.LaneEndForwardRoutings[currentFwdRoutingIndex].transitions;

                if (currentFwdTransitions == null) {
                    Log._DebugIf(
                        logLaneSelection,
                        () => $"VehicleBehaviorManager.FindBestLane({vehicleId}): No forward " +
                              $"transitions found for current lane {currentLaneId} at startNode {startNode}.");
                    return next1PathPos.m_lane;
                }

                VehicleInfo vehicleInfo = vehicleData.Info;
                float vehicleMaxSpeed = vehicleInfo.m_maxSpeed / 8f;
                float vehicleCurSpeed = vehicleData.GetLastFrameVelocity().magnitude / 8f;

                float bestStayMeanSpeed = 0f;
                float bestStaySpeedDiff = float.PositiveInfinity; // best speed difference on next continuous lane
                int bestStayTotalLaneDist = int.MaxValue;
                byte bestStayNext1LaneIndex = next1PathPos.m_lane;

                float bestOptMeanSpeed = 0f;
                float bestOptSpeedDiff = float.PositiveInfinity; // best speed difference on all next lanes
                int bestOptTotalLaneDist = int.MaxValue;
                byte bestOptNext1LaneIndex = next1PathPos.m_lane;

                bool foundSafeLaneChange = false;
                // bool foundClearBackLane = false;
                // bool foundClearFwdLane = false;

                // ushort reachableNext1LanesMask = 0;
                uint reachableNext2LanesMask = 0;
                uint reachableNext3LanesMask = 0;

                // int numReachableNext1Lanes = 0;
                int numReachableNext2Lanes = 0;
                int numReachableNext3Lanes = 0;

                Log._DebugIf(
                    logLaneSelection,
                    () => $"VehicleBehaviorManager.FindBestLane({vehicleId}): Starting lane-finding " +
                      $"algorithm now. vehicleMaxSpeed={vehicleMaxSpeed}, vehicleCurSpeed={vehicleCurSpeed} " +
                      $"vehicleLength={vehicleLength}");

                uint mask;

                for (int i = 0; i < currentFwdTransitions.Length; ++i) {
                    if (currentFwdTransitions[i].segmentId != next1PathPos.m_segment) {
                        continue;
                    }

                    if (!(currentFwdTransitions[i].type == LaneEndTransitionType.Default ||
                          currentFwdTransitions[i].type == LaneEndTransitionType.LaneConnection ||
                          (recklessDriver && currentFwdTransitions[i].type == LaneEndTransitionType.Relaxed))) {
                        continue;
                    }

                    if (currentFwdTransitions[i].distance > 1) {
                        Log._DebugIf(
                            logLaneSelection,
                            () => $"VehicleBehaviorManager.FindBestLane({vehicleId}): Skipping current " +
                            $"transition {currentFwdTransitions[i]} (distance too large)");

                        continue;
                    }

                    if (!VehicleRestrictionsManager.Instance.MayUseLane(
                            vehicleState.vehicleType,
                            next1PathPos.m_segment,
                            currentFwdTransitions[i].laneIndex,
                            next1SegInfo)) {
                        if (logLaneSelection) {
                            Log._Debug(
                                $"VehicleBehaviorManager.FindBestLane({vehicleId}): Skipping current " +
                                $"transition {currentFwdTransitions[i]} (vehicle restrictions)");
                        }

                        continue;
                    }

                    int minTotalLaneDist = int.MaxValue;

                    if (next2PathPos.m_segment != 0) {
                        // next1 -> next2
                        uint next1FwdRoutingIndex = RoutingManager.Instance.GetLaneEndRoutingIndex(
                            currentFwdTransitions[i].laneId,
                            !currentFwdTransitions[i].startNode);
#if DEBUG
                        if (next1FwdRoutingIndex >= RoutingManager.Instance.LaneEndForwardRoutings.Length)
                        {
                            Log.ErrorFormat(
                                "Invalid array index: next1FwdRoutingIndex={0}, " +
                                "RoutingManager.Instance.laneEndForwardRoutings.Length={1} " +
                                "(currentFwdTransitions[i].laneId={2}, !currentFwdTransitions[i]" +
                                ".startNode={3})",
                                next1FwdRoutingIndex,
                                RoutingManager.Instance.LaneEndForwardRoutings.Length,
                                currentFwdTransitions[i].laneId,
                                !currentFwdTransitions[i].startNode);
                        }
#endif

                        if (logLaneSelection) {
                            Log._DebugFormat(
                                "VehicleBehaviorManager.FindBestLane({0}): Exploring transitions for " +
                                "next1 lane id={1}, seg.={2}, index={3}, startNode={4}: {5}",
                                vehicleId,
                                currentFwdTransitions[i].laneId,
                                currentFwdTransitions[i].segmentId,
                                currentFwdTransitions[i].laneIndex,
                                !currentFwdTransitions[i].startNode,
                                RoutingManager.Instance.LaneEndForwardRoutings[next1FwdRoutingIndex]);
                        }

                        if (!RoutingManager.Instance.LaneEndForwardRoutings[next1FwdRoutingIndex].routed) {
                            continue;
                        }

                        LaneTransitionData[] next1FwdTransitions
                            = RoutingManager.Instance
                                            .LaneEndForwardRoutings[next1FwdRoutingIndex]
                                            .transitions;

                        if (next1FwdTransitions == null) {
                            continue;
                        }

                        bool foundNext1Next2 = false;

                        for (int j = 0; j < next1FwdTransitions.Length; ++j) {
                            if (next1FwdTransitions[j].segmentId != next2PathPos.m_segment) {
                                continue;
                            }

                            if (!(next1FwdTransitions[j].type == LaneEndTransitionType.Default
                                  || next1FwdTransitions[j].type == LaneEndTransitionType.LaneConnection
                                  || (recklessDriver && next1FwdTransitions[j].type == LaneEndTransitionType.Relaxed)))
                            {
                                continue;
                            }

                            if (next1FwdTransitions[j].distance > 1) {
                                if (logLaneSelection) {
                                    Log._Debug(
                                        $"VehicleBehaviorManager.FindBestLane({vehicleId}): Skipping " +
                                        $"next1 transition {next1FwdTransitions[j]} (distance too large)");
                                }
                                continue;
                            }

                            if (!VehicleRestrictionsManager.Instance.MayUseLane(
                                    vehicleState.vehicleType,
                                    next2PathPos.m_segment,
                                    next1FwdTransitions[j].laneIndex,
                                    next2SegInfo))
                            {
                                if (logLaneSelection) {
                                    Log._Debug(
                                        $"VehicleBehaviorManager.FindBestLane({vehicleId}): Skipping " +
                                        $"next1 transition {next1FwdTransitions[j]} (vehicle restrictions)");
                                }

                                continue;
                            }

                            if (next3PathPos.m_segment != 0) {
                                // next2 -> next3
                                uint next2FwdRoutingIndex =
                                    RoutingManager.Instance.GetLaneEndRoutingIndex(
                                        next1FwdTransitions[j].laneId,
                                        !next1FwdTransitions[j].startNode);
#if DEBUG
                                if (next2FwdRoutingIndex >= RoutingManager.Instance.LaneEndForwardRoutings.Length) {
                                    Log._DebugOnlyError(
                                        $"Invalid array index: next2FwdRoutingIndex={next2FwdRoutingIndex}, " +
                                        "RoutingManager.Instance.laneEndForwardRoutings.Length=" +
                                        $"{RoutingManager.Instance.LaneEndForwardRoutings.Length} " +
                                        $"(next1FwdTransitions[j].laneId={next1FwdTransitions[j].laneId}, " +
                                        $"!next1FwdTransitions[j].startNode={!next1FwdTransitions[j].startNode})");
                                }
#endif
                                if (logLaneSelection) {
                                    Log._DebugFormat(
                                        "VehicleBehaviorManager.FindBestLane({0}): Exploring transitions " +
                                        "for next2 lane id={1}, seg.={2}, index={3}, startNode={4}: {5}",
                                        vehicleId,
                                        next1FwdTransitions[j].laneId,
                                        next1FwdTransitions[j].segmentId,
                                        next1FwdTransitions[j].laneIndex,
                                        !next1FwdTransitions[j].startNode,
                                        RoutingManager.Instance.LaneEndForwardRoutings[next2FwdRoutingIndex]);
                                }

                                if (!RoutingManager
                                     .Instance.LaneEndForwardRoutings[next2FwdRoutingIndex]
                                     .routed) {
                                    continue;
                                }

                                LaneTransitionData[] next2FwdTransitions
                                    = RoutingManager.Instance
                                                    .LaneEndForwardRoutings[next2FwdRoutingIndex]
                                                    .transitions;

                                if (next2FwdTransitions == null) {
                                    continue;
                                }

                                bool foundNext2Next3 = false;

                                for (int k = 0; k < next2FwdTransitions.Length; ++k) {
                                    if (next2FwdTransitions[k].segmentId != next3PathPos.m_segment) {
                                        continue;
                                    }

                                    if (!(next2FwdTransitions[k].type == LaneEndTransitionType.Default
                                          || next2FwdTransitions[k].type == LaneEndTransitionType.LaneConnection
                                          || (recklessDriver && next2FwdTransitions[k].type
                                              == LaneEndTransitionType.Relaxed)))
                                    {
                                        continue;
                                    }

                                    if (next2FwdTransitions[k].distance > 1) {
                                        if (logLaneSelection) {
                                            Log._Debug(
                                                $"VehicleBehaviorManager.FindBestLane({vehicleId}): " +
                                                $"Skipping next2 transition {next2FwdTransitions[k]} " +
                                                "(distance too large)");
                                        }

                                        continue;
                                    }

                                    if (!VehicleRestrictionsManager.Instance.MayUseLane(
                                            vehicleState.vehicleType,
                                            next3PathPos.m_segment,
                                            next2FwdTransitions[k].laneIndex,
                                            next3SegInfo))
                                    {
                                        if (logLaneSelection) {
                                            Log._Debug(
                                                $"VehicleBehaviorManager.FindBestLane({vehicleId}): " +
                                                $"Skipping next2 transition {next2FwdTransitions[k]} " +
                                                "(vehicle restrictions)");
                                        }

                                        continue;
                                    }

                                    if (next4PathPos.m_segment != 0) {
                                        // next3 -> next4
                                        uint next3FwdRoutingIndex =
                                            RoutingManager.Instance.GetLaneEndRoutingIndex(
                                                next2FwdTransitions[k].laneId,
                                                !next2FwdTransitions[k].startNode);
#if DEBUG
                                        if (next3FwdRoutingIndex
                                            >= RoutingManager.Instance.LaneEndForwardRoutings.Length)
                                        {
                                            Log.ErrorFormat(
                                                "Invalid array index: next3FwdRoutingIndex={0}, " +
                                                "RoutingManager.Instance.laneEndForwardRoutings.Length={1} " +
                                                "(next2FwdTransitions[k].laneId={2}, " +
                                                "!next2FwdTransitions[k].startNode={3})",
                                                next3FwdRoutingIndex,
                                                RoutingManager.Instance.LaneEndForwardRoutings.Length,
                                                next2FwdTransitions[k].laneId,
                                                !next2FwdTransitions[k].startNode);
                                        }
#endif

                                        if (logLaneSelection) {
                                            Log._DebugFormat(
                                                "VehicleBehaviorManager.FindBestLane({0}): Exploring " +
                                                "transitions for next3 lane id={1}, seg.={2}, index={3}, " +
                                                "startNode={4}: {5}",
                                                vehicleId,
                                                next2FwdTransitions[k].laneId,
                                                next2FwdTransitions[k].segmentId,
                                                next2FwdTransitions[k].laneIndex,
                                                !next2FwdTransitions[k].startNode,
                                                RoutingManager.Instance.LaneEndForwardRoutings[next3FwdRoutingIndex]);
                                        }

                                        if (!RoutingManager
                                             .Instance.LaneEndForwardRoutings[next3FwdRoutingIndex]
                                             .routed) {
                                            continue;
                                        }

                                        LaneTransitionData[] next3FwdTransitions =
                                            RoutingManager.Instance
                                                          .LaneEndForwardRoutings[next3FwdRoutingIndex]
                                                          .transitions;

                                        if (next3FwdTransitions == null) {
                                            continue;
                                        }

                                        // check if original next4 lane is accessible via the next3 lane
                                        //
                                        //--------------------------------------
                                        bool foundNext3Next4 = false;

                                        for (int l = 0; l < next3FwdTransitions.Length; ++l) {
                                            if (next3FwdTransitions[l].segmentId != next4PathPos.m_segment) {
                                                continue;
                                            }

                                            if (!(next3FwdTransitions[l].type == LaneEndTransitionType.Default ||
                                                  next3FwdTransitions[l].type == LaneEndTransitionType.LaneConnection ||
                                                  (recklessDriver && next3FwdTransitions[l].type
                                                   == LaneEndTransitionType.Relaxed)))
                                            {
                                                continue;
                                            }

                                            if (next3FwdTransitions[l].distance > 1) {
                                                if (logLaneSelection) {
                                                    Log._Debug(
                                                        $"VehicleBehaviorManager.FindBestLane({vehicleId}): " +
                                                        $"Skipping next3 transition {next3FwdTransitions[l]} " +
                                                        "(distance too large)");
                                                }
                                                continue;
                                            }

                                            if (next3FwdTransitions[l].laneIndex == next4PathPos.m_lane) {
                                                // we found a valid routing from [current lane]
                                                // (currentPathPos) to [next1 lane] (next1Pos),
                                                // [next2 lane] (next2Pos), [next3 lane] (next3Pos),
                                                // and [next4 lane] (next4Pos)
                                                foundNext3Next4 = true;
                                                int totalLaneDist =
                                                    next1FwdTransitions[j].distance +
                                                    next2FwdTransitions[k].distance +
                                                    next3FwdTransitions[l].distance;

                                                if (totalLaneDist < minTotalLaneDist) {
                                                    minTotalLaneDist = totalLaneDist;
                                                }

                                                if (logLaneSelection) {
                                                    Log._DebugFormat(
                                                        "VehicleBehaviorManager.FindBestLane({0}): Found candidate transition with totalLaneDist={1}: {2} -> {3} -> {4} -> {5} -> {6}",
                                                        vehicleId,
                                                        totalLaneDist,
                                                        currentLaneId,
                                                        currentFwdTransitions[i],
                                                        next1FwdTransitions[j],
                                                        next2FwdTransitions[k],
                                                        next3FwdTransitions[l]);
                                                }
                                                break;
                                            }
                                        } // for l

                                        if (foundNext3Next4) {
                                            foundNext2Next3 = true;
                                        }
                                    } else {
                                        foundNext2Next3 = true;
                                    }

                                    if (foundNext2Next3) {
                                        mask = POW2MASKS[next2FwdTransitions[k].laneIndex];
                                        if ((reachableNext3LanesMask & mask) == 0) {
                                            ++numReachableNext3Lanes;
                                            reachableNext3LanesMask |= mask;
                                        }
                                    }
                                } // for k

                                if (foundNext2Next3) {
                                    foundNext1Next2 = true;
                                }
                            } else {
                                foundNext1Next2 = true;
                            }

                            if (foundNext1Next2) {
                                mask = POW2MASKS[next1FwdTransitions[j].laneIndex];
                                if ((reachableNext2LanesMask & mask) == 0) {
                                    ++numReachableNext2Lanes;
                                    reachableNext2LanesMask |= mask;
                                }
                            }
                        } // for j

                        if (next3PathPos.m_segment != 0 && !foundNext1Next2) {
                            // go to next candidate next1 lane
                            continue;
                        }
                    }

                    /*mask = POW2MASKS[currentFwdTransitions[i].laneIndex];
                    if ((reachableNext1LanesMask & mask) == 0) {
                            ++numReachableNext1Lanes;
                            reachableNext1LanesMask |= mask;
                    }*/

                    // This lane is a valid candidate.

                    //bool next1StartNode = next1PathPos.m_offset < 128;
                    //ushort next1TransitNode = 0;
                    //Services.NetService.ProcessSegment(next1PathPos.m_segment, delegate (ushort next1SegId, ref NetSegment next1Seg) {
                    //	next1TransitNode = next1StartNode ? next1Seg.m_startNode : next1Seg.m_endNode;
                    //	return true;
                    //});

                    //bool next1TransitNodeIsJunction = false;
                    //Services.NetService.ProcessNode(next1TransitNode, delegate (ushort nId, ref NetNode node) {
                    //	next1TransitNodeIsJunction = (node.m_flags & NetNode.Flags.Junction) != NetNode.Flags.None;
                    //	return true;
                    //});

                    /*
                     * Check if next1 lane is clear
                     */
                    if (logLaneSelection) {
                        Log._Debug(
                            $"VehicleBehaviorManager.FindBestLane({vehicleId}): Checking for traffic " +
                            $"on next1 lane id={currentFwdTransitions[i].laneId}.");
                    }

                    bool laneChange = currentFwdTransitions[i].distance != 0;
                    /*bool next1LaneClear = true;
                    if (laneChange) {
                            // check for traffic on next1 lane
                            float reservedSpace = 0;
                            Services.NetService.ProcessLane(currentFwdTransitions[i].laneId, delegate (uint next1LaneId, ref NetLane next1Lane) {
                                    reservedSpace = next1Lane.GetReservedSpace();
                                    return true;
                            });

                            if (currentFwdTransitions[i].laneIndex == next1PathPos.m_lane) {
                                    reservedSpace -= vehicleLength;
                            }

                            next1LaneClear = reservedSpace <= (recklessDriver ? conf.AltLaneSelectionMaxRecklessReservedSpace : conf.AltLaneSelectionMaxReservedSpace);
                    }

                    if (foundClearFwdLane && !next1LaneClear) {
                            continue;
                    }*/

                    /*
                     * Check traffic on the lanes in front of the candidate lane in order to
                     * prevent vehicles from backing up traffic
                     */
                    bool prevLanesClear = true;

                    if (laneChange) {
                        uint next1BackRoutingIndex = RoutingManager.Instance.GetLaneEndRoutingIndex(
                            currentFwdTransitions[i].laneId,
                            currentFwdTransitions[i].startNode);

#if DEBUG
                        if (next1BackRoutingIndex < 0 || next1BackRoutingIndex >=
                            RoutingManager.Instance.LaneEndForwardRoutings.Length) {
                            Log.ErrorFormat(
                                "Invalid array index: next1BackRoutingIndex={0}, " +
                                "RoutingManager.Instance.laneEndForwardRoutings.Length={1} " +
                                "(currentFwdTransitions[i].laneId={2}, currentFwdTransitions[i].startNode={3})",
                                next1BackRoutingIndex,
                                RoutingManager.Instance.LaneEndForwardRoutings.Length,
                                currentFwdTransitions[i].laneId,
                                currentFwdTransitions[i].startNode);
                        }
#endif
                        if (!RoutingManager.Instance.LaneEndBackwardRoutings[next1BackRoutingIndex].routed) {
                            continue;
                        }

                        LaneTransitionData[] next1BackTransitions
                            = RoutingManager.Instance
                                            .LaneEndBackwardRoutings[next1BackRoutingIndex]
                                            .transitions;

                        if (next1BackTransitions == null) {
                            continue;
                        }

                        for (int j = 0; j < next1BackTransitions.Length; ++j) {
                            if (next1BackTransitions[j].segmentId != currentPathPos.m_segment ||
                                next1BackTransitions[j].laneIndex == currentPathPos.m_lane) {
                                continue;
                            }

                            if (!(next1BackTransitions[j].type == LaneEndTransitionType.Default
                                  || next1BackTransitions[j].type == LaneEndTransitionType.LaneConnection
                                  || (recklessDriver && next1BackTransitions[j].type == LaneEndTransitionType.Relaxed)))
                            {
                                continue;
                            }

                            if (next1BackTransitions[j].distance > 1) {
                                if (logLaneSelection) {
                                    Log._Debug(
                                        $"VehicleBehaviorManager.FindBestLane({vehicleId}): Skipping " +
                                        $"next1 backward transition {next1BackTransitions[j]} " +
                                        "(distance too large)");
                                }

                                continue;
                            }

                            if (logLaneSelection) {
                                Log._DebugFormat(
                                    "VehicleBehaviorManager.FindBestLane({0}): Checking for upcoming " +
                                    "traffic in front of next1 lane id={1}. Checking back transition {2}",
                                    vehicleId,
                                    currentFwdTransitions[i].laneId,
                                    next1BackTransitions[j]);
                            }

                            prevLanesClear = next1BackTransitions[j].laneId.ToLane().GetReservedSpace() <= maxReservedSpace;
                            if (!prevLanesClear) {
                                if (logLaneSelection) {
                                    Log._Debug(
                                        $"VehicleBehaviorManager.FindBestLane({vehicleId}): Back lane " +
                                        $"{next1BackTransitions[j].laneId} is not clear!");
                                }
                                break;
                            }

                            if (logLaneSelection) {
                                Log._Debug(
                                    $"VehicleBehaviorManager.FindBestLane({vehicleId}): Back lane " +
                                    $"{next1BackTransitions[j].laneId} is clear!");
                            }
                        }
                    }

                    Log._DebugIf(
                        logLaneSelection,
                        () => $"VehicleBehaviorManager.FindBestLane({vehicleId}): Checking for coming " +
                              $"up traffic in front of next1 lane. prevLanesClear={prevLanesClear}");

                    if ( // foundClearBackLane
                        foundSafeLaneChange && !prevLanesClear) {
                        continue;
                    }

                    // calculate lane metric
#if DEBUG
                    if (currentFwdTransitions[i].laneIndex >= next1SegInfo.m_lanes.Length) {
                        Log.Error(
                            "Invalid array index: currentFwdTransitions[i].laneIndex=" +
                            $"{currentFwdTransitions[i].laneIndex}, " +
                            $"next1SegInfo.m_lanes.Length={next1SegInfo.m_lanes.Length}");
                    }
#endif
                    NetInfo.Lane next1LaneInfo =
                        next1SegInfo.m_lanes[currentFwdTransitions[i].laneIndex];
                    float next1MaxSpeed = SpeedLimitManager.Instance.GetGameSpeedLimit(
                        currentFwdTransitions[i].segmentId,
                        currentFwdTransitions[i].laneIndex,
                        currentFwdTransitions[i].laneId,
                        next1LaneInfo);
                    float targetSpeed = Math.Min(
                        vehicleMaxSpeed,
                        ApplyRealisticSpeeds(
                            next1MaxSpeed,
                            vehicleId,
                            ref vehicleState,
                            vehicleInfo));

                    ushort meanSpeed = TrafficMeasurementManager.Instance.CalcLaneRelativeMeanSpeed(
                        currentFwdTransitions[i].segmentId,
                        currentFwdTransitions[i].laneIndex,
                        currentFwdTransitions[i].laneId,
                        next1LaneInfo);

                    float relMeanSpeedInPercent =
                        meanSpeed / (TrafficMeasurementManager.REF_REL_SPEED /
                                     TrafficMeasurementManager.REF_REL_SPEED_PERCENT_DENOMINATOR);

                    if (vehicleState.laneSpeedRandInterval > 0) {
                        float randSpeed =
                            Singleton<SimulationManager>.instance.m_randomizer.Int32(
                                (uint)vehicleState.laneSpeedRandInterval + 1u) -
                            (vehicleState.laneSpeedRandInterval / 2f);

                        relMeanSpeedInPercent += randSpeed;
                    }

                    float relMeanSpeed = relMeanSpeedInPercent /
                                         TrafficMeasurementManager.REF_REL_SPEED_PERCENT_DENOMINATOR;
                    float next1MeanSpeed = relMeanSpeed * next1MaxSpeed;

                    //if (
                    //#if DEBUG
                    //    conf.Debug.Switches[19] &&
                    //#endif
                    //    next1LaneInfo.m_similarLaneCount > 1) {
                    //    float relLaneInnerIndex =
                    //        ((float)RoutingManager
                    //                .Instance.CalcOuterSimilarLaneIndex(next1LaneInfo) /
                    //         (float)next1LaneInfo.m_similarLaneCount);
                    //    float rightObligationFactor =
                    //        conf.AltLaneSelectionMostOuterLaneSpeedFactor +
                    //        (conf.AltLaneSelectionMostInnerLaneSpeedFactor -
                    //         conf.AltLaneSelectionMostOuterLaneSpeedFactor) * relLaneInnerIndex;
                    //#if DEBUG
                    //    if (debug) {
                    //        Log._Debug(
                    //            $"VehicleBehaviorManager.FindBestLane({vehicleId}): Applying obligation
                    // factor to next1 lane {currentFwdTransitions[i].laneId}: relLaneInnerIndex={relLaneInnerIndex},
                    // rightObligationFactor={rightObligationFactor}, next1MaxSpeed={next1MaxSpeed},
                    // relMeanSpeedInPercent={relMeanSpeedInPercent}, randSpeed={randSpeed},
                    // next1MeanSpeed={next1MeanSpeed} => new next1MeanSpeed={Mathf.Max(rightObligationFactor *
                    // next1MaxSpeed, next1MeanSpeed)}");
                    //    }
                    //#endif
                    //    next1MeanSpeed = Mathf.Min(
                    //        rightObligationFactor * next1MaxSpeed,
                    //        next1MeanSpeed);
                    //}

                    // > 0: lane is faster than vehicle would go. < 0: vehicle could go faster
                    // than this lane allows
                    float speedDiff = next1MeanSpeed - targetSpeed;

                    if (logLaneSelection) {
                        Log._DebugFormat(
                            "VehicleBehaviorManager.FindBestLane({0}): Calculated metric for next1 lane {1}: " +
                            "next1MaxSpeed={2} next1MeanSpeed={3} targetSpeed={4} speedDiff={5} " +
                            "bestSpeedDiff={6} bestStaySpeedDiff={7}",
                            vehicleId,
                            currentFwdTransitions[i].laneId,
                            next1MaxSpeed,
                            next1MeanSpeed,
                            targetSpeed,
                            speedDiff,
                            bestOptSpeedDiff,
                            bestStaySpeedDiff);
                    }

                    if (!laneChange) {
                        if (float.IsInfinity(bestStaySpeedDiff) ||
                             (bestStaySpeedDiff < 0 && speedDiff > bestStaySpeedDiff) ||
                             (bestStaySpeedDiff > 0 && speedDiff < bestStaySpeedDiff && speedDiff >= 0))
                        {
                            bestStaySpeedDiff = speedDiff;
                            bestStayNext1LaneIndex = currentFwdTransitions[i].laneIndex;
                            bestStayMeanSpeed = next1MeanSpeed;
                            bestStayTotalLaneDist = minTotalLaneDist;
                        }
                    } else {
                        // bool foundFirstClearFwdLane = laneChange && !foundClearFwdLane && next1LaneClear;
                        // bool foundFirstClearBackLane = laneChange && !foundClearBackLane && prevLanesClear;
                        bool foundFirstSafeLaneChange = !foundSafeLaneChange && /*next1LaneClear &&*/ prevLanesClear;
                        if (/*(foundFirstClearFwdLane && !foundClearBackLane) ||
							(foundFirstClearBackLane && !foundClearFwdLane) ||*/
                            foundFirstSafeLaneChange ||
                            float.IsInfinity(bestOptSpeedDiff) ||
                            (bestOptSpeedDiff < 0 && speedDiff > bestOptSpeedDiff) ||
                            (bestOptSpeedDiff > 0 && speedDiff < bestOptSpeedDiff && speedDiff >= 0))
                        {
                            bestOptSpeedDiff = speedDiff;
                            bestOptNext1LaneIndex = currentFwdTransitions[i].laneIndex;
                            bestOptMeanSpeed = next1MeanSpeed;
                            bestOptTotalLaneDist = minTotalLaneDist;
                        }

                        // if (foundFirstClearBackLane) {
                        //         foundClearBackLane = true;
                        // }
                        //
                        // if (foundFirstClearFwdLane) {
                        //         foundClearFwdLane = true;
                        // }
                        if (foundFirstSafeLaneChange) {
                            foundSafeLaneChange = true;
                        }
                    }
                } // for i

                if (logLaneSelection) {
                    Log._DebugFormat(
                        "VehicleBehaviorManager.FindBestLane({0}): best lane index: {1}, best stay " +
                        "lane index: {2}, path lane index: {3})\nbest speed diff: {4}, best stay " +
                        "speed diff: {5}\nfoundClearBackLane=XXfoundClearBackLaneXX, " +
                        "foundClearFwdLane=XXfoundClearFwdLaneXX, foundSafeLaneChange={6}\n" +
                        "bestMeanSpeed={7}, bestStayMeanSpeed={8}",
                        vehicleId,
                        bestOptNext1LaneIndex,
                        bestStayNext1LaneIndex,
                        next1PathPos.m_lane,
                        bestOptSpeedDiff,
                        bestStaySpeedDiff,
                        foundSafeLaneChange,
                        bestOptMeanSpeed,
                        bestStayMeanSpeed);
                }

                if (float.IsInfinity(bestStaySpeedDiff)) {
                    // no continuous lane found
                    Log._DebugIf(
                        logLaneSelection,
                        () => $"VehicleBehaviorManager.FindBestLane({vehicleId}): ===> no continuous " +
                        $"lane found -- selecting bestOptNext1LaneIndex={bestOptNext1LaneIndex}");

                    return bestOptNext1LaneIndex;
                }

                if (float.IsInfinity(bestOptSpeedDiff)) {
                    // no lane change found
                    Log._DebugIf(
                        logLaneSelection,
                        () => $"VehicleBehaviorManager.FindBestLane({vehicleId}): ===> no lane " +
                        $"change found -- selecting bestStayNext1LaneIndex={bestStayNext1LaneIndex}");

                    return bestStayNext1LaneIndex;
                }

                // decide if vehicle should stay or change

                // vanishing lane change opportunity detection
                int vehSel = vehicleId % 12;

                Log._DebugIf(
                    logLaneSelection,
                    () => $"VehicleBehaviorManager.FindBestLane({vehicleId}): vehMod4={vehSel} " +
                    $"numReachableNext2Lanes={numReachableNext2Lanes} " +
                    $"numReachableNext3Lanes={numReachableNext3Lanes}");

                // 50% of all vehicles will change lanes 3 segments in front
                // OR 33% of all vehicles will change lanes 2 segments in front, 16.67% will change
                // at the last opportunity
                if ((numReachableNext3Lanes == 1 && vehSel <= 5) ||
                    (numReachableNext2Lanes == 1 && vehSel <= 9))
                {
                    // vehicle must reach a certain lane since lane changing opportunities will vanish
                    Log._DebugIf(
                        logLaneSelection,
                        () => $"VehicleBehaviorManager.FindBestLane({vehicleId}): vanishing lane change " +
                        $"opportunities detected: numReachableNext2Lanes={numReachableNext2Lanes} " +
                        $"numReachableNext3Lanes={numReachableNext3Lanes}, vehSel={vehSel}, " +
                        $"bestOptTotalLaneDist={bestOptTotalLaneDist}, bestStayTotalLaneDist" +
                        $"={bestStayTotalLaneDist}");

                    if (bestOptTotalLaneDist < bestStayTotalLaneDist) {
                        Log._DebugIf(
                            logLaneSelection,
                            () => $"VehicleBehaviorManager.FindBestLane({vehicleId}): ===> vanishing " +
                            $"lane change opportunities -- selecting bestOptTotalLaneDist={bestOptTotalLaneDist}");

                        return bestOptNext1LaneIndex;
                    }

                    Log._DebugIf(
                        logLaneSelection,
                        () => $"VehicleBehaviorManager.FindBestLane({vehicleId}): ===> vanishing lane " +
                        $"change opportunities -- selecting bestStayTotalLaneDist={bestStayTotalLaneDist}");

                    return bestStayNext1LaneIndex;
                }

                bool isBssDiffZero = Math.Abs(bestStaySpeedDiff) < FloatUtil.VERY_SMALL_FLOAT;
                if (isBssDiffZero || bestOptMeanSpeed < 0.1f) {
                    //---------------------------------------
                    // edge cases:
                    //   (1) continuous lane is super optimal
                    //   (2) best mean speed is near zero
                    //---------------------------------------
                    if (logLaneSelection) {
                        Log._DebugFormat(
                            "VehicleBehaviorManager.FindBestLane({0}): ===> edge case: continuous lane " +
                            "is optimal ({1}) / best mean speed is near zero ({2}) -- selecting " +
                            "bestStayNext1LaneIndex={3}",
                            vehicleId,
                            isBssDiffZero,
                            bestOptMeanSpeed < 0.1f,
                            bestStayNext1LaneIndex);
                    }

                    return bestStayNext1LaneIndex;
                }

                if (bestStayTotalLaneDist != bestOptTotalLaneDist &&
                    Math.Max(bestStayTotalLaneDist, bestOptTotalLaneDist) >
                    vehicleState.maxOptLaneChanges)
                {
                    // best route contains more lane changes than allowed: choose lane with the
                    // least number of future lane changes
                    Log._DebugIf(
                        logLaneSelection,
                        () => $"VehicleBehaviorManager.FindBestLane({vehicleId}): maximum best total " +
                        $"lane distance = {Math.Max(bestStayTotalLaneDist, bestOptTotalLaneDist)} > " +
                        "AltLaneSelectionMaxOptLaneChanges");

                    if (bestOptTotalLaneDist < bestStayTotalLaneDist) {
                        Log._DebugIf(
                            logLaneSelection,
                            () => $"VehicleBehaviorManager.FindBestLane({vehicleId}): ===> selecting " +
                            "lane change option for minimizing number of future lane changes -- selecting " +
                            $"bestOptNext1LaneIndex={bestOptNext1LaneIndex}");

                        return bestOptNext1LaneIndex;
                    }

                    Log._DebugIf(
                        logLaneSelection,
                        () => $"VehicleBehaviorManager.FindBestLane({vehicleId}): ===> selecting stay " +
                        "option for minimizing number of future lane changes -- selecting " +
                        $"bestStayNext1LaneIndex={bestStayNext1LaneIndex}");

                    return bestStayNext1LaneIndex;
                }

                if (bestStaySpeedDiff < 0
                    && bestOptSpeedDiff > bestStaySpeedDiff)
                {
                    // found a lane change that improves vehicle speed
                    // float improvement = 100f * ((bestOptSpeedDiff - bestStaySpeedDiff)
                    //     / ((bestStayMeanSpeed + bestOptMeanSpeed) / 2f));
                    float speedDiff = Mathf.Abs(bestOptMeanSpeed - vehicleCurSpeed);
                    float optImprovementSpeed = bestOptSpeedDiff - bestStaySpeedDiff;

                    if (logLaneSelection) {
                        Log._DebugFormat(
                            "VehicleBehaviorManager.FindBestLane({0}): a lane change for speed " +
                            "improvement is possible. optImprovementInKmH={1} speedDiff={2} " +
                            "(bestOptMeanSpeed={3}, vehicleCurVelocity={4}, foundSafeLaneChange={5})",
                            vehicleId,
                            new SpeedValue(optImprovementSpeed).ToKmphPrecise(),
                            speedDiff,
                            bestOptMeanSpeed,
                            vehicleCurSpeed,
                            foundSafeLaneChange);
                    }

                    if (optImprovementSpeed >= vehicleState.minSafeSpeedImprovement &&
                        (foundSafeLaneChange || (speedDiff <= vehicleState.maxUnsafeSpeedDiff)))
                    {
                        // speed improvement is significant
                        Log._DebugIf(
                            logLaneSelection,
                            () => $"VehicleBehaviorManager.FindBestLane({vehicleId}): ===> found a " +
                            "faster lane to change to and speed improvement is significant -- selecting " +
                            $"bestOptNext1LaneIndex={bestOptNext1LaneIndex} (foundSafeLaneChange" +
                            $"={foundSafeLaneChange}, speedDiff={speedDiff})");

                        return bestOptNext1LaneIndex;
                    }

                    // insufficient improvement
                    Log._DebugIf(
                        logLaneSelection,
                        () => $"VehicleBehaviorManager.FindBestLane({vehicleId}): ===> found a faster " +
                        "lane to change to but speed improvement is NOT significant OR lane change " +
                        $"is unsafe -- selecting bestStayNext1LaneIndex={bestStayNext1LaneIndex} " +
                        $"(foundSafeLaneChange={foundSafeLaneChange})");

                    return bestStayNext1LaneIndex;
                }

                if (!recklessDriver && foundSafeLaneChange && bestStaySpeedDiff > 0 &&
                    bestOptSpeedDiff < bestStaySpeedDiff && bestOptSpeedDiff >= 0)
                {
                    // found a lane change that allows faster vehicles to overtake
                    float optimization =
                        100f * ((bestStaySpeedDiff - bestOptSpeedDiff) /
                                ((bestStayMeanSpeed + bestOptMeanSpeed) / 2f));
                    Log._DebugIf(
                        logLaneSelection,
                        () => $"VehicleBehaviorManager.FindBestLane({vehicleId}): found a lane " +
                        $"change that optimizes overall traffic. optimization={optimization}%");

                    if (optimization >= vehicleState.minSafeTrafficImprovement) {
                        // traffic optimization is significant
                        Log._DebugIf(
                            logLaneSelection,
                            () => $"VehicleBehaviorManager.FindBestLane({vehicleId}): ===> found a " +
                            "lane that optimizes overall traffic and traffic optimization is significant " +
                            $"-- selecting bestOptNext1LaneIndex={bestOptNext1LaneIndex}");

                        return bestOptNext1LaneIndex;
                    }

                    // insufficient optimization
                    Log._DebugIf(
                        logLaneSelection,
                        () => $"VehicleBehaviorManager.FindBestLane({vehicleId}): ===> found a lane " +
                        "that optimizes overall traffic but optimization is NOT significant -- selecting " +
                        $"bestStayNext1LaneIndex={bestStayNext1LaneIndex}");

                    return bestOptNext1LaneIndex;
                }

                // suboptimal safe lane change
                Log._DebugIf(
                    logLaneSelection,
                    () => $"VehicleBehaviorManager.FindBestLane({vehicleId}): ===> suboptimal safe " +
                    $"lane change detected -- selecting bestStayNext1LaneIndex={bestStayNext1LaneIndex}");

                return bestStayNext1LaneIndex;
            } catch (Exception e) {
                Log.Error($"VehicleBehaviorManager.FindBestLane({vehicleId}): Exception occurred: {e}");
            }

            return next1PathPos.m_lane;
        }

        public int FindBestEmergencyLane(ushort vehicleId,
                                         ref Vehicle vehicleData,
                                         ref ExtVehicle vehicleState,
                                         uint currentLaneId,
                                         PathUnit.Position currentPathPos,
                                         NetInfo currentSegInfo,
                                         PathUnit.Position nextPathPos,
                                         NetInfo nextSegInfo)
        {
            try {
                // GlobalConfig conf = GlobalConfig.Instance;
#if DEBUG
                bool logLaneSelection = false;

                if (DebugSwitch.AlternativeLaneSelection.Get()) {
                    ref NetSegment netSegment = ref currentPathPos.m_segment.ToSegment();
                    ushort nodeId = currentPathPos.m_offset < 128
                        ? netSegment.m_startNode
                        : netSegment.m_endNode;
                    logLaneSelection =
                        (DebugSettings.VehicleId == 0 || DebugSettings.VehicleId == vehicleId)
                        && (DebugSettings.NodeId == 0 || DebugSettings.NodeId == nodeId);
                }
#else
                const bool logLaneSelection = false;
#endif

                if (logLaneSelection) {
                    Log._DebugFormat(
                    "VehicleBehaviorManager.FindBestEmergencyLane({0}): currentLaneId={1}, " +
                    "currentPathPos=[seg={2}, lane={3}, off={4}] nextPathPos=[seg={5}, lane={6}, off={7}]",
                    vehicleId,
                    currentLaneId,
                    currentPathPos.m_segment,
                    currentPathPos.m_lane,
                    currentPathPos.m_offset,
                    nextPathPos.m_segment,
                    nextPathPos.m_lane,
                    nextPathPos.m_offset);
                }

                // cur -> next
                float curPosition = 0f;

                if (currentPathPos.m_lane < currentSegInfo.m_lanes.Length) {
                    curPosition = currentSegInfo.m_lanes[currentPathPos.m_lane].m_position;
                }

                float vehicleLength = 1f + vehicleState.totalLength;
                bool startNode = currentPathPos.m_offset < 128;
                uint currentFwdRoutingIndex =
                    RoutingManager.Instance.GetLaneEndRoutingIndex(currentLaneId, startNode);

#if DEBUG
                if (currentFwdRoutingIndex >= RoutingManager.Instance.LaneEndForwardRoutings.Length) {
                    Log.ErrorFormat(
                        "VehicleBehaviorManager.FindBestEmergencyLane({0}): Invalid array index: " +
                        "currentFwdRoutingIndex={1}, RoutingManager.Instance.laneEndForwardRoutings.Length={2} " +
                        "(currentLaneId={3}, startNode={4})",
                        vehicleId,
                        currentFwdRoutingIndex,
                        RoutingManager.Instance.LaneEndForwardRoutings.Length,
                        currentLaneId,
                        startNode);
                }
#endif

                if (!RoutingManager.Instance.LaneEndForwardRoutings[currentFwdRoutingIndex].routed) {
                    Log._DebugIf(
                        logLaneSelection,
                        () => $"VehicleBehaviorManager.FindBestEmergencyLane({vehicleId}): " +
                        "No forward routing for next path position available.");

                    return nextPathPos.m_lane;
                }

                LaneTransitionData[] currentFwdTransitions
                    = RoutingManager.Instance.LaneEndForwardRoutings[currentFwdRoutingIndex].transitions;

                if (currentFwdTransitions == null) {
                    Log._DebugIf(
                        logLaneSelection,
                        () => $"VehicleBehaviorManager.FindBestEmergencyLane({vehicleId}): No forward " +
                        $"transitions found for current lane {currentLaneId} at startNode {startNode}.");

                    return nextPathPos.m_lane;
                }

                Log._DebugIf(
                    logLaneSelection,
                    () => $"VehicleBehaviorManager.FindBestEmergencyLane({vehicleId}): Starting " +
                    $"lane-finding algorithm now. vehicleLength={vehicleLength}");

                float minCost = float.MaxValue;
                byte bestNextLaneIndex = nextPathPos.m_lane;

                for (int i = 0; i < currentFwdTransitions.Length; ++i) {
                    if (currentFwdTransitions[i].segmentId != nextPathPos.m_segment) {
                        continue;
                    }

                    if (!(currentFwdTransitions[i].type == LaneEndTransitionType.Default ||
                          currentFwdTransitions[i].type == LaneEndTransitionType.LaneConnection ||
                          currentFwdTransitions[i].type == LaneEndTransitionType.Relaxed))
                    {
                        continue;
                    }

                    if (!VehicleRestrictionsManager.Instance.MayUseLane(
                            vehicleState.vehicleType,
                            nextPathPos.m_segment,
                            currentFwdTransitions[i].laneIndex,
                            nextSegInfo))
                    {
                        Log._DebugIf(
                            logLaneSelection,
                            () => $"VehicleBehaviorManager.FindBestEmergencyLane({vehicleId}): " +
                            $"Skipping current transition {currentFwdTransitions[i]} (vehicle restrictions)");

                        continue;
                    }

                    var nextLaneInfoPos = nextSegInfo.m_lanes[currentFwdTransitions[i].laneIndex].m_position;

                    //---------------------------
                    // Check reserved space on next lane
                    //---------------------------
                    Log._DebugIf(
                        logLaneSelection,
                        () => $"VehicleBehaviorManager.FindBestEmergencyLane({vehicleId}): Checking " +
                        $"for traffic on next lane id={currentFwdTransitions[i].laneId}.");

                    float cost = currentFwdTransitions[i].laneId.ToLane().GetReservedSpace();

                    if (currentFwdTransitions[i].laneIndex == nextPathPos.m_lane) {
                        cost -= vehicleLength;
                    }

                    cost += Mathf.Abs(curPosition - nextLaneInfoPos) * 0.1f;

                    if (cost < minCost) {
                        minCost = cost;
                        bestNextLaneIndex = currentFwdTransitions[i].laneIndex;

                        Log._DebugIf(
                            logLaneSelection,
                            () => $"VehicleBehaviorManager.FindBestEmergencyLane({vehicleId}): " +
                            $"Found better lane: bestNextLaneIndex={bestNextLaneIndex}, minCost={minCost}");
                    }
                } // for each forward transition

                Log._DebugIf(
                    logLaneSelection,
                    () => $"VehicleBehaviorManager.FindBestEmergencyLane({vehicleId}): Best lane identified: " +
                    $"bestNextLaneIndex={bestNextLaneIndex}, minCost={minCost}");

                return bestNextLaneIndex;
            } catch (Exception e) {
                Log.Error($"VehicleBehaviorManager.FindBestEmergencyLane({vehicleId}): Exception occurred: {e}");
            }

            return nextPathPos.m_lane;
        }

        public bool MayFindBestLane(ushort vehicleId, ref Vehicle vehicleData, ref ExtVehicle vehicleState) {
            GlobalConfig conf = GlobalConfig.Instance;
#if DEBUG
            bool logLaneSelection = DebugSwitch.AlternativeLaneSelection.Get()
                 && (DebugSettings.VehicleId == 0 || DebugSettings.VehicleId == vehicleId);

            if (logLaneSelection) {
                Log._Debug($"VehicleBehaviorManager.MayFindBestLane({vehicleId}) called.");
            }
#else
            const bool logLaneSelection = false;
#endif

            if (!SavedGameOptions.Instance.advancedAI) {
                Log._DebugIf(
                    logLaneSelection,
                    () => $"VehicleBehaviorManager.MayFindBestLane({vehicleId}): Skipping lane checking. " +
                    "Advanced Vehicle AI is disabled.");

                return false;
            }

            if (vehicleState.heavyVehicle) {
                Log._DebugIf(
                    logLaneSelection,
                    () => $"VehicleBehaviorManager.MayFindBestLane({vehicleId}): Skipping lane checking. " +
                    "Vehicle is heavy.");

                return false;
            }

            if ((vehicleState.vehicleType & (ExtVehicleType.RoadVehicle & ~ExtVehicleType.Bus)) == ExtVehicleType.None) {
                if (logLaneSelection) {
                    Log._Debug($"VehicleBehaviorManager.MayFindBestLane({vehicleId}): Skipping lane " +
                               $"checking. vehicleType={vehicleState.vehicleType}");
                }

                return false;
            }

            uint vehicleRand = Constants.ManagerFactory.ExtVehicleManager.GetStaticVehicleRand(vehicleId);

            if (vehicleRand < 100 - SavedGameOptions.Instance.altLaneSelectionRatio) {
                Log._DebugIf(
                    logLaneSelection,
                    () => $"VehicleBehaviorManager.MayFindBestLane({vehicleId}): Skipping lane checking " +
                    "(randomization).");

                return false;
            }

            return true;
        }
    }
}