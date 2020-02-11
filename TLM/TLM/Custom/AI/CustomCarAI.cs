// #define DEBUGV

namespace TrafficManager.Custom.AI {
    using ColossalFramework.Math;
    using ColossalFramework;
    using CSUtil.Commons.Benchmark;
    using CSUtil.Commons;
    using JetBrains.Annotations;
    using System.Diagnostics.CodeAnalysis;
    using System.Runtime.CompilerServices;
    using System;
    using TrafficManager.API.Manager;
    using TrafficManager.API.Traffic.Data;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.Custom.PathFinding;
    using TrafficManager.Manager.Impl;
    using TrafficManager.Manager;
    using TrafficManager.RedirectionFramework.Attributes;
    using TrafficManager.State.ConfigData;
    using TrafficManager.State;
    using UnityEngine;

    // TODO inherit from VehicleAI (in order to keep the correct references to `base`)
    [TargetType(typeof(CarAI))]
    public class CustomCarAI : CarAI {
        /// <summary>
        /// Lightweight simulation step method.
        /// This method is occasionally being called for different cars.
        /// </summary>
        /// <param name="vehicleId">Vehicle</param>
        /// <param name="vehicleData">The struct representing the vehicle</param>
        /// <param name="physicsLodRefPos">Reference position for geometry LOD</param>
        [RedirectMethod]
        public void CustomSimulationStep(ushort vehicleId,
                                         ref Vehicle vehicleData,
                                         Vector3 physicsLodRefPos) {
#if DEBUG
            bool vehDebug = DebugSettings.VehicleId == 0
                           || DebugSettings.VehicleId == vehicleId;
            bool logParkingAi = DebugSwitch.BasicParkingAILog.Get() && vehDebug;
#else
            var logParkingAi = false;
#endif

            if ((vehicleData.m_flags & Vehicle.Flags.WaitingPath) != 0) {
                PathManager pathManager = Singleton<PathManager>.instance;
                byte pathFindFlags = pathManager.m_pathUnits.m_buffer[vehicleData.m_path].m_pathFindFlags;

                // NON-STOCK CODE START
                ExtPathState mainPathState = ExtPathState.Calculating;
                if ((pathFindFlags & PathUnit.FLAG_FAILED) != 0 || vehicleData.m_path == 0) {
                    mainPathState = ExtPathState.Failed;
                } else if ((pathFindFlags & PathUnit.FLAG_READY) != 0) {
                    mainPathState = ExtPathState.Ready;
                }

#if DEBUG
                uint logVehiclePath = vehicleData.m_path;
                Log._DebugIf(
                    logParkingAi,
                    () => $"CustomCarAI.CustomSimulationStep({vehicleId}): " +
                    $"Path: {logVehiclePath}, mainPathState={mainPathState}");
#endif

                IExtVehicleManager extVehicleManager = Constants.ManagerFactory.ExtVehicleManager;
                ExtSoftPathState finalPathState = ExtCitizenInstance.ConvertPathStateToSoftPathState(mainPathState);
                if (Options.parkingAI
                    && extVehicleManager.ExtVehicles[vehicleId].vehicleType == ExtVehicleType.PassengerCar)
                {
                    ushort driverInstanceId = extVehicleManager.GetDriverInstanceId(vehicleId, ref vehicleData);
                    finalPathState = AdvancedParkingManager.Instance.UpdateCarPathState(
                        vehicleId,
                        ref vehicleData,
                        ref Singleton<CitizenManager>.instance.m_instances.m_buffer[driverInstanceId],
                        ref ExtCitizenInstanceManager.Instance.ExtInstances[driverInstanceId],
                        mainPathState);

#if DEBUG
                    if (logParkingAi) {
                        Log._Debug($"CustomCarAI.CustomSimulationStep({vehicleId}): " +
                                   $"Applied Parking AI logic. Path: {vehicleData.m_path}, " +
                                   $"mainPathState={mainPathState}, finalPathState={finalPathState}");
                    }
#endif
                }

                switch (finalPathState) {
                    case ExtSoftPathState.Ready: {
#if DEBUG
                        if (logParkingAi) {
                            Log._Debug(
                                $"CustomCarAI.CustomSimulationStep({vehicleId}): Path-finding " +
                                $"succeeded for vehicle {vehicleId} (finalPathState={finalPathState}). " +
                                $"Path: {vehicleData.m_path} -- calling CarAI.PathfindSuccess");
                        }
#endif

                        vehicleData.m_pathPositionIndex = 255;
                        vehicleData.m_flags &= ~Vehicle.Flags.WaitingPath;
                        vehicleData.m_flags &= ~Vehicle.Flags.Arriving;
                        PathfindSuccess(vehicleId, ref vehicleData);
                        TrySpawn(vehicleId, ref vehicleData);
                        break;
                    }

                    case ExtSoftPathState.Ignore: {
#if DEBUG
                        if (logParkingAi) {
                            Log._Debug(
                                $"CustomCarAI.CustomSimulationStep({vehicleId}): Path-finding " +
                                $"result shall be ignored for vehicle {vehicleId} " +
                                $"(finalPathState={finalPathState}). Path: {vehicleData.m_path} -- ignoring");
                        }
#endif
                        return;
                    }

                    case ExtSoftPathState.Calculating:
                    default: {
#if DEBUG
                        if (logParkingAi) {
                            Log._Debug(
                                $"CustomCarAI.CustomSimulationStep({vehicleId}): Path-finding " +
                                $"result undetermined for vehicle {vehicleId} (finalPathState={finalPathState}). " +
                                $"Path: {vehicleData.m_path} -- continue");
                        }
#endif
                        break;
                    }

                    case ExtSoftPathState.FailedHard: {
#if DEBUG
                        if (logParkingAi) {
                            Log._Debug(
                                $"CustomCarAI.CustomSimulationStep({vehicleId}): HARD path-finding " +
                                $"failure for vehicle {vehicleId} (finalPathState={finalPathState}). " +
                                $"Path: {vehicleData.m_path} -- calling CarAI.PathfindFailure");
                        }
#endif
                        vehicleData.m_flags &= ~Vehicle.Flags.WaitingPath;
                        Singleton<PathManager>.instance.ReleasePath(vehicleData.m_path);
                        vehicleData.m_path = 0u;
                        PathfindFailure(vehicleId, ref vehicleData);
                        return;
                    }

                    case ExtSoftPathState.FailedSoft: {
#if DEBUG
                        if (logParkingAi) {
                            Log._Debug(
                                $"CustomCarAI.CustomSimulationStep({vehicleId}): SOFT path-finding " +
                                $"failure for vehicle {vehicleId} (finalPathState={finalPathState}). " +
                                $"Path: {vehicleData.m_path} -- calling CarAI.InvalidPath");
                        }
#endif

                        // path mode has been updated, repeat path-finding
                        vehicleData.m_flags &= ~Vehicle.Flags.WaitingPath;
                        InvalidPath(vehicleId, ref vehicleData, vehicleId, ref vehicleData);
                        break;
                    }
                }

                // NON-STOCK CODE END
            } else {
                if ((vehicleData.m_flags & Vehicle.Flags.WaitingSpace) != 0) {
                    TrySpawn(vehicleId, ref vehicleData);
                }
            }

            // NON-STOCK CODE START
            IExtVehicleManager extVehicleMan = Constants.ManagerFactory.ExtVehicleManager;
            extVehicleMan.UpdateVehiclePosition(vehicleId, ref vehicleData);

            if (Options.advancedAI
                && (vehicleData.m_flags & Vehicle.Flags.Spawned) != 0)
            {
                extVehicleMan.LogTraffic(vehicleId, ref vehicleData);
            }

            // NON-STOCK CODE END
            Vector3 lastFramePosition = vehicleData.GetLastFramePosition();
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

            SimulationStep(vehicleId, ref vehicleData, vehicleId, ref vehicleData, lodPhysics);
            if (vehicleData.m_leadingVehicle == 0 && vehicleData.m_trailingVehicle != 0) {
                VehicleManager vehManager = Singleton<VehicleManager>.instance;
                ushort trailerId = vehicleData.m_trailingVehicle;
                int numIters = 0;
                while (trailerId != 0) {
                    ushort trailingVehicle = vehManager.m_vehicles.m_buffer[trailerId].m_trailingVehicle;
                    VehicleInfo info = vehManager.m_vehicles.m_buffer[trailerId].Info;

                    info.m_vehicleAI.SimulationStep(
                        trailerId,
                        ref vehManager.m_vehicles.m_buffer[trailerId],
                        vehicleId,
                        ref vehicleData,
                        lodPhysics);

                    trailerId = trailingVehicle;
                    if (++numIters > 16384) {
                        CODebugBase<LogChannel>.Error(
                            LogChannel.Core,
                            $"Invalid list detected!\n{Environment.StackTrace}");
                        break;
                    }
                }
            }

            int privateServiceIndex = ItemClass.GetPrivateServiceIndex(m_info.m_class.m_service);
            int maxBlockCounter = (privateServiceIndex == -1) ? 150 : 100;

            if ((vehicleData.m_flags & (Vehicle.Flags.Spawned
                                        | Vehicle.Flags.WaitingPath
                                        | Vehicle.Flags.WaitingSpace)) == 0
                && vehicleData.m_cargoParent == 0) {
                Singleton<VehicleManager>.instance.ReleaseVehicle(vehicleId);
            } else if (vehicleData.m_blockCounter >= maxBlockCounter) {
                // NON-STOCK CODE START
                if (VehicleBehaviorManager.Instance.MayDespawn(ref vehicleData)) {
                    // NON-STOCK CODE END
                    Singleton<VehicleManager>.instance.ReleaseVehicle(vehicleId);
                } // NON-STOCK CODE
            }
        }

        [RedirectMethod]
        [UsedImplicitly]
        public bool CustomTrySpawn(ushort vehicleId, ref Vehicle vehicleData) {
            if ((vehicleData.m_flags & Vehicle.Flags.Spawned) != 0) {
                return true;
            }

            if (CheckOverlap(vehicleData.m_segment, 0, 1000f)) {
                vehicleData.m_flags |= Vehicle.Flags.WaitingSpace;
                return false;
            }

            vehicleData.Spawn(vehicleId);
            vehicleData.m_flags &= ~Vehicle.Flags.WaitingSpace;
            return true;
        }

        [RedirectMethod]
        public void CustomCalculateSegmentPosition(ushort vehicleId,
                                                   ref Vehicle vehicleData,
                                                   PathUnit.Position nextNextPosition,
                                                   PathUnit.Position nextPosition,
                                                   uint nextLaneId,
                                                   byte nextOffset,
                                                   PathUnit.Position curPosition,
                                                   uint curLaneId,
                                                   byte curOffset,
                                                   int index,
                                                   out Vector3 pos,
                                                   out Vector3 dir,
                                                   out float maxSpeed) {
            NetManager netManager = Singleton<NetManager>.instance;
            ushort nextSourceNodeId;
            ushort nextTargetNodeId;
            NetSegment[] segmentsBuffer = netManager.m_segments.m_buffer;

            if (nextOffset < nextPosition.m_offset) {
                nextSourceNodeId = segmentsBuffer[nextPosition.m_segment].m_startNode;
                nextTargetNodeId = segmentsBuffer[nextPosition.m_segment].m_endNode;
            } else {
                nextSourceNodeId = segmentsBuffer[nextPosition.m_segment].m_endNode;
                nextTargetNodeId = segmentsBuffer[nextPosition.m_segment].m_startNode;
            }

            ushort curTargetNodeId;
            curTargetNodeId = curOffset == 0
                                  ? segmentsBuffer[curPosition.m_segment].m_startNode :
                                  segmentsBuffer[curPosition.m_segment].m_endNode;

#if DEBUG
            bool logCalculation = DebugSwitch.CalculateSegmentPosition.Get()
                        && (DebugSettings.NodeId <= 0
                            || curTargetNodeId == DebugSettings.NodeId)
                        && (GlobalConfig.Instance.Debug.ApiExtVehicleType == ExtVehicleType.None
                            || GlobalConfig.Instance.Debug.ApiExtVehicleType == ExtVehicleType.RoadVehicle)
                        && (DebugSettings.VehicleId == 0
                            || DebugSettings.VehicleId == vehicleId);

            if (logCalculation) {
                Log._Debug($"CustomCarAI.CustomCalculateSegmentPosition({vehicleId}) called.\n" +
                           $"\tcurPosition.m_segment={curPosition.m_segment}, " +
                           $"curPosition.m_offset={curPosition.m_offset}\n" +
                           $"\tnextPosition.m_segment={nextPosition.m_segment}, " +
                           $"nextPosition.m_offset={nextPosition.m_offset}\n" +
                           $"\tnextNextPosition.m_segment={nextNextPosition.m_segment}, " +
                           $"nextNextPosition.m_offset={nextNextPosition.m_offset}\n" +
                           $"\tcurLaneId={curLaneId}, curOffset={curOffset}\n" +
                           $"\tnextLaneId={nextLaneId}, nextOffset={nextOffset}\n" +
                           $"\tnextSourceNodeId={nextSourceNodeId}, nextTargetNodeId={nextTargetNodeId}\n" +
                           $"\tcurTargetNodeId={curTargetNodeId}, curTargetNodeId={curTargetNodeId}\n" +
                           $"\tindex={index}");
            }
#endif

            Vehicle.Frame lastFrameData = vehicleData.GetLastFrameData();
            Vector3 lastFrameVehiclePos = lastFrameData.m_position;
            float sqrVelocity = lastFrameData.m_velocity.sqrMagnitude;
            NetInfo prevSegmentInfo = segmentsBuffer[nextPosition.m_segment].Info;
            netManager.m_lanes.m_buffer[nextLaneId].CalculatePositionAndDirection(
                Constants.ByteToFloat(nextOffset), out pos, out dir);

            float braking = m_info.m_braking;
            if ((vehicleData.m_flags & Vehicle.Flags.Emergency2) != 0) {
                braking *= 2f;
            }

            // car position on the Bezier curve of the lane
            Vector3 refVehiclePosOnBezier = netManager.m_lanes.m_buffer[curLaneId].CalculatePosition(
                Constants.ByteToFloat(curOffset));

            // ushort currentSegmentId = netManager.m_lanes.m_buffer[prevLaneID].m_segment;
            // this seems to be like the required braking force in order to stop the vehicle within its half length.
            float crazyValue = (0.5f * sqrVelocity / braking) + (m_info.m_generatedInfo.m_size.z * 0.5f);
            float d = Vector3.Distance(lastFrameVehiclePos, refVehiclePosOnBezier);
            bool withinBrakingDistance = d >= crazyValue - 1f;

            if (nextSourceNodeId == curTargetNodeId
                && withinBrakingDistance) {
                // NON-STOCK CODE START (stock code replaced)
                if (!VehicleBehaviorManager.Instance.MayChangeSegment(
                        vehicleId,
                        ref vehicleData,
                        sqrVelocity,
                        ref curPosition,
                        ref segmentsBuffer[curPosition.m_segment],
                        curTargetNodeId,
                        curLaneId,
                        ref nextPosition,
                        nextSourceNodeId,
                        ref netManager.m_nodes.m_buffer[nextSourceNodeId],
                        nextLaneId,
                        ref nextNextPosition,
                        nextTargetNodeId,
                        out maxSpeed)) {
                    // NON-STOCK CODE
                    return;
                }

                ExtVehicleManager.Instance.UpdateVehiclePosition(
                    vehicleId, ref vehicleData/*, lastFrameData.m_velocity.magnitude*/);

                // NON-STOCK CODE END
            }

            if (prevSegmentInfo.m_lanes != null
                && prevSegmentInfo.m_lanes.Length > nextPosition.m_lane) {
                // NON-STOCK CODE START
                float laneSpeedLimit = !Options.customSpeedLimitsEnabled
                                           ? prevSegmentInfo.m_lanes[nextPosition.m_lane].m_speedLimit
                                           : Constants.ManagerFactory.SpeedLimitManager.GetLockFreeGameSpeedLimit(
                                               nextPosition.m_segment,
                                               nextPosition.m_lane,
                                               nextLaneId,
                                               prevSegmentInfo.m_lanes[nextPosition.m_lane]);

                // NON-STOCK CODE END
                maxSpeed = CalculateTargetSpeed(
                    vehicleId,
                    ref vehicleData,
                    laneSpeedLimit,
                    netManager.m_lanes.m_buffer[nextLaneId].m_curve);
            } else {
                maxSpeed = CalculateTargetSpeed(vehicleId, ref vehicleData, 1f, 0f);
            }

            // NON-STOCK CODE START (stock code replaced)
            maxSpeed = Constants.ManagerFactory.VehicleBehaviorManager.CalcMaxSpeed(
                vehicleId,
                ref Constants.ManagerFactory.ExtVehicleManager.ExtVehicles[vehicleId],
                m_info,
                nextPosition,
                ref segmentsBuffer[nextPosition.m_segment],
                pos,
                maxSpeed,
                false);

            // NON-STOCK CODE END
        }

        [RedirectMethod]
        public void CustomCalculateSegmentPosition(ushort vehicleId,
                                                   ref Vehicle vehicleData,
                                                   PathUnit.Position position,
                                                   uint laneId,
                                                   byte offset,
                                                   out Vector3 pos,
                                                   out Vector3 dir,
                                                   out float maxSpeed) {
            NetManager netManager = Singleton<NetManager>.instance;
            NetInfo segmentInfo = netManager.m_segments.m_buffer[position.m_segment].Info;
            netManager.m_lanes.m_buffer[laneId].CalculatePositionAndDirection(
                Constants.ByteToFloat(offset),
                out pos,
                out dir);

            if (segmentInfo.m_lanes != null
                && segmentInfo.m_lanes.Length > position.m_lane) {
                // NON-STOCK CODE START
                float laneSpeedLimit = !Options.customSpeedLimitsEnabled
                                         ? segmentInfo.m_lanes[position.m_lane].m_speedLimit
                                         : Constants.ManagerFactory.SpeedLimitManager.GetLockFreeGameSpeedLimit(
                                             position.m_segment,
                                             position.m_lane,
                                             laneId,
                                             segmentInfo.m_lanes[position.m_lane]);

                // NON-STOCK CODE END
                maxSpeed = CalculateTargetSpeed(
                    vehicleId,
                    ref vehicleData,
                    laneSpeedLimit,
                    netManager.m_lanes.m_buffer[laneId].m_curve);
            } else {
                maxSpeed = CalculateTargetSpeed(vehicleId, ref vehicleData, 1f, 0f);
            }

            // NON-STOCK CODE START
            maxSpeed = VehicleBehaviorManager.Instance.CalcMaxSpeed(
                vehicleId,
                ref Constants.ManagerFactory.ExtVehicleManager.ExtVehicles[vehicleId],
                m_info,
                position,
                ref netManager.m_segments.m_buffer[position.m_segment],
                pos,
                maxSpeed,
                false);

            // NON-STOCK CODE END
        }

        [RedirectMethod]
        [UsedImplicitly]
        public bool CustomStartPathFind(ushort vehicleId,
                                        ref Vehicle vehicleData,
                                        Vector3 startPos,
                                        Vector3 endPos,
                                        bool startBothWays,
                                        bool endBothWays,
                                        bool undergroundTarget) {
#if DEBUG
            bool vehDebug = DebugSettings.VehicleId == 0
                           || DebugSettings.VehicleId == vehicleId;
            bool logParkingAi = DebugSwitch.BasicParkingAILog.Get() && vehDebug;
#else
            var logParkingAi = false;
#endif
            Log._DebugOnlyWarningIf(
                logParkingAi,
                () => $"CustomCarAI.CustomStartPathFind({vehicleId}): called for vehicle " +
                $"{vehicleId}, startPos={startPos}, endPos={endPos}, " +
                $"startBothWays={startBothWays}, endBothWays={endBothWays}, " +
                $"undergroundTarget={undergroundTarget}");

            ExtVehicleType vehicleType;

            using (var bm = Benchmark.MaybeCreateBenchmark(null, "OnStartPathFind")) {
                vehicleType =
                    ExtVehicleManager.Instance.OnStartPathFind(vehicleId, ref vehicleData, null);
                if (vehicleType == ExtVehicleType.None) {
                    Log._DebugOnlyWarning(
                        $"CustomCarAI.CustomStartPathFind({vehicleId}): Vehicle {vehicleId} " +
                        "does not have a valid vehicle type!");
                    vehicleType = ExtVehicleType.RoadVehicle;
                }
            }

            VehicleInfo info = m_info;
            bool allowUnderground = (vehicleData.m_flags & (Vehicle.Flags.Underground
                                                           | Vehicle.Flags.Transition)) != 0;

            if (!PathManager.FindPathPosition(
                    startPos,
                    ItemClass.Service.Road,
                    NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle,
                    info.m_vehicleType,
                    allowUnderground,
                    false,
                    32f,
                    out PathUnit.Position startPosA,
                    out PathUnit.Position startPosB,
                    out float startDistSqrA,
                    out _) || !PathManager.FindPathPosition(
                    endPos,
                    ItemClass.Service.Road,
                    NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle,
                    info.m_vehicleType,
                    undergroundTarget,
                    false,
                    32f,
                    out PathUnit.Position endPosA,
                    out PathUnit.Position endPosB,
                    out float endDistSqrA,
                    out _)) {
                return false;
            }

            if (!startBothWays || startDistSqrA < 10f) {
                startPosB = default;
            }

            if (!endBothWays || endDistSqrA < 10f) {
                endPosB = default;
            }

            // NON-STOCK CODE START
            PathCreationArgs args;
            args.extPathType = ExtPathType.None;
            args.extVehicleType = vehicleType;
            args.vehicleId = vehicleId;
            args.spawned = (vehicleData.m_flags & Vehicle.Flags.Spawned) != 0;
            args.buildIndex = Singleton<SimulationManager>.instance.m_currentBuildIndex;
            args.startPosA = startPosA;
            args.startPosB = startPosB;
            args.endPosA = endPosA;
            args.endPosB = endPosB;
            args.vehiclePosition = default;
            args.laneTypes = NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle;
            args.vehicleTypes = info.m_vehicleType;
            args.maxLength = 20000f;
            args.isHeavyVehicle = IsHeavyVehicle();
            args.hasCombustionEngine = CombustionEngine();
            args.ignoreBlocked = IgnoreBlocked(vehicleId, ref vehicleData);
            args.ignoreFlooded = false;
            args.ignoreCosts = false;
            args.randomParking = false;
            args.stablePath = false;
            args.skipQueue = (vehicleData.m_flags & Vehicle.Flags.Spawned) != 0;

            if (!CustomPathManager._instance.CustomCreatePath(
                    out uint path,
                    ref Singleton<SimulationManager>.instance.m_randomizer,
                    args)) {
                return false;
            }

            Log._DebugIf(
                logParkingAi,
                () => $"CustomCarAI.CustomStartPathFind({vehicleId}): " +
                $"Path-finding starts for vehicle {vehicleId}, path={path}, " +
                $"extVehicleType={vehicleType}, startPosA.segment={startPosA.m_segment}, " +
                $"startPosA.lane={startPosA.m_lane}, info.m_vehicleType={info.m_vehicleType}, " +
                $"endPosA.segment={endPosA.m_segment}, endPosA.lane={endPosA.m_lane}");

            // NON-STOCK CODE END
            if (vehicleData.m_path != 0u) {
                Singleton<PathManager>.instance.ReleasePath(vehicleData.m_path);
            }

            vehicleData.m_path = path;
            vehicleData.m_flags |= Vehicle.Flags.WaitingPath;
            return true;
        }

        [RedirectMethod]
        [UsedImplicitly]
        public static ushort CustomCheckOtherVehicle(ushort vehicleId,
                                                     ref Vehicle vehicleData,
                                                     ref Vehicle.Frame frameData,
                                                     ref float maxSpeed,
                                                     ref bool blocked,
                                                     ref Vector3 collisionPush,
                                                     float maxBraking,
                                                     ushort otherID,
                                                     ref Vehicle otherData,
                                                     Vector3 min,
                                                     Vector3 max,
                                                     int lodPhysics) {
            if (otherID == vehicleId
                || vehicleData.m_leadingVehicle == otherID
                || vehicleData.m_trailingVehicle == otherID) {
                return otherData.m_nextGridVehicle;
            }

            VehicleInfo info = otherData.Info;
            if (info.m_vehicleType == VehicleInfo.VehicleType.Bicycle) {
                return otherData.m_nextGridVehicle;
            }

            if (((vehicleData.m_flags | otherData.m_flags) & Vehicle.Flags.Transition) == 0
                && (vehicleData.m_flags & Vehicle.Flags.Underground) !=
                (otherData.m_flags & Vehicle.Flags.Underground)) {
                return otherData.m_nextGridVehicle;
            }

#if DEBUG
            bool logLogic = DebugSwitch.ResourceLoading.Get() &&
                         (GlobalConfig.Instance.Debug.ApiExtVehicleType == ExtVehicleType.None
                          || GlobalConfig.Instance.Debug.ApiExtVehicleType == ExtVehicleType.RoadVehicle)
                         && (DebugSettings.VehicleId == 0
                             || DebugSettings.VehicleId == vehicleId);
#else
            const bool logLogic = false;
#endif
            Log._DebugIf(
                logLogic,
                () => $"CustomCarAI.CustomCheckOtherVehicle({vehicleId}, {otherID}) called.");

            Vector3 otherSegMin;
            Vector3 otherSegMax;
            if (lodPhysics >= 2) {
                otherSegMin = otherData.m_segment.Min();
                otherSegMax = otherData.m_segment.Max();
            } else {
                otherSegMin = Vector3.Min(otherData.m_segment.Min(), otherData.m_targetPos3);
                otherSegMax = Vector3.Max(otherData.m_segment.Max(), otherData.m_targetPos3);
            }

            if (min.x >= otherSegMax.x + 2f
                && min.y >= otherSegMax.y + 2f
                && min.z >= otherSegMax.z + 2f
                && otherSegMin.x >= max.x + 2f
                && otherSegMin.y >= max.y + 2f
                && otherSegMin.z >= max.z + 2f) {
                return otherData.m_nextGridVehicle;
            }

            Vehicle.Frame otherFrameData = otherData.GetLastFrameData();
            if (lodPhysics < 2) {
                float segSqrDist = vehicleData.m_segment.DistanceSqr(otherData.m_segment, out _, out _);

                if (segSqrDist < 4f) {
                    Vector3 vehPos = vehicleData.m_segment.Position(0.5f);
                    Vector3 otherPos = otherData.m_segment.Position(0.5f);
                    Vector3 vehBounds = vehicleData.m_segment.b - vehicleData.m_segment.a;
                    if (Vector3.Dot(vehBounds, vehPos - otherPos) < 0f) {
                        collisionPush -= vehBounds.normalized * (0.1f - (segSqrDist * 0.025f));
                    } else {
                        collisionPush += vehBounds.normalized * (0.1f - (segSqrDist * 0.025f));
                    }

                    blocked = true;
                }
            }

            float vehVelocity = frameData.m_velocity.magnitude + 0.01f;
            float otherVehVelocity = otherFrameData.m_velocity.magnitude;
            float otherBreakingDist =
                (otherVehVelocity * (0.5f + (0.5f * otherVehVelocity / info.m_braking)))
                + Mathf.Min(1f, otherVehVelocity);
            otherVehVelocity += 0.01f;

            float prevLength = 0f;
            Vector3 prevTargetPos = vehicleData.m_segment.b;
            Vector3 prevBounds = vehicleData.m_segment.b - vehicleData.m_segment.a;
            int startI = (vehicleData.Info.m_vehicleType == VehicleInfo.VehicleType.Tram) ? 1 : 0;

            for (int i = startI; i < 4; i++) {
                Vector3 targetPos = vehicleData.GetTargetPos(i);
                Vector3 targetPosDiff = targetPos - prevTargetPos;
                if (Vector3.Dot(prevBounds, targetPosDiff) <= 0f) {
                    continue;
                }

                float targetPosDiffLen = targetPosDiff.magnitude;
                Segment3 curSegment = new Segment3(prevTargetPos, targetPos);
                min = curSegment.Min();
                max = curSegment.Max();
                curSegment.a.y *= 0.5f;
                curSegment.b.y *= 0.5f;

                if (targetPosDiffLen > 0.01f
                    && min.x < otherSegMax.x + 2f
                    && min.y < otherSegMax.y + 2f
                    && min.z < otherSegMax.z + 2f
                    && otherSegMin.x < max.x + 2f
                    && otherSegMin.y < max.y + 2f
                    && otherSegMin.z < max.z + 2f)
                {
                    Vector3 otherVehFrontPos = otherData.m_segment.a;
                    otherVehFrontPos.y *= 0.5f;

                    if (curSegment.DistanceSqr(otherVehFrontPos, out float u) < 4f) {
                        float otherCosAngleToTargetPosDiff =
                            Vector3.Dot(otherFrameData.m_velocity, targetPosDiff) / targetPosDiffLen;
                        float uDist = prevLength + (targetPosDiffLen * u);

                        if (uDist >= 0.01f) {
                            uDist -= otherCosAngleToTargetPosDiff + 3f;
                            float speed = Mathf.Max(
                                0f,
                                CalculateMaxSpeed(uDist,
                                                  otherCosAngleToTargetPosDiff,
                                                  maxBraking));
                            if (speed < 0.01f) {
                                blocked = true;
                            }

                            Vector3 normOtherDir =
                                Vector3.Normalize((Vector3)otherData.m_targetPos0 - otherData.m_segment.a);

                            float blockFactor = 1.2f - (1f / ((vehicleData.m_blockCounter * 0.02f) + 0.5f));

                            if (Vector3.Dot(targetPosDiff, normOtherDir) > blockFactor * targetPosDiffLen) {
                                maxSpeed = Mathf.Min(maxSpeed, speed);
                            }
                        }

                        break;
                    }

                    if (lodPhysics < 2) {
                        float totalDist = 0f;
                        float otherBreakDist = otherBreakingDist;
                        Vector3 otherFrontPos = otherData.m_segment.b;
                        Vector3 otherBounds = otherData.m_segment.b - otherData.m_segment.a;
                        int startOtherTargetPosIndex = (info.m_vehicleType == VehicleInfo.VehicleType.Tram) ? 1 : 0;
                        bool exitTargetPosLoop = false;
                        int otherTargetPosIndex = startOtherTargetPosIndex;

                        while (otherTargetPosIndex < 4 && otherBreakDist > 0.1f) {
                            Vector3 otherTargetPos;
                            if (otherData.m_leadingVehicle == 0) {
                                otherTargetPos = otherData.GetTargetPos(otherTargetPosIndex);
                            } else {
                                if (otherTargetPosIndex != startOtherTargetPosIndex) {
                                    break;
                                }

                                Vehicle[] vehiclesBuffer = Singleton<VehicleManager>.instance.m_vehicles.m_buffer;
                                otherTargetPos = vehiclesBuffer[otherData.m_leadingVehicle].m_segment.b;
                            }

                            Vector3 minBreakPos = Vector3.ClampMagnitude(otherTargetPos - otherFrontPos,
                                                                     otherBreakDist);

                            if (Vector3.Dot(otherBounds, minBreakPos) > 0f) {
                                otherTargetPos = otherFrontPos + minBreakPos;

                                float breakPosDist = minBreakPos.magnitude;
                                otherBreakDist -= breakPosDist;

                                Segment3 otherVehNextSegment = new Segment3(otherFrontPos, otherTargetPos);
                                otherVehNextSegment.a.y *= 0.5f;
                                otherVehNextSegment.b.y *= 0.5f;

                                if (breakPosDist > 0.01f) {
                                    float otherVehNextSegmentDistToCurSegment
                                        = otherID >= vehicleId
                                              ? curSegment.DistanceSqr(
                                                  otherVehNextSegment,
                                                  out float otherVehNextSegU,
                                                  out float otherVehNextSegV)
                                              : otherVehNextSegment.DistanceSqr(
                                                  curSegment,
                                                  out otherVehNextSegV,
                                                  out otherVehNextSegU);

                                    if (otherVehNextSegmentDistToCurSegment < 4f) {
                                        float uDist = prevLength + (targetPosDiffLen * otherVehNextSegU);
                                        float vDist = totalDist + (breakPosDist * otherVehNextSegV) + 0.1f;

                                        if (uDist >= 0.01f && uDist * otherVehVelocity > vDist * vehVelocity) {
                                            float otherCosAngleToTargetPosDiff =
                                                Vector3.Dot(
                                                    otherFrameData.m_velocity,
                                                    targetPosDiff) / targetPosDiffLen;
                                            if (uDist >= 0.01f) {
                                                uDist -= otherCosAngleToTargetPosDiff
                                                         + 1f
                                                         + otherData.Info.m_generatedInfo.m_size.z;
                                                float speed = Mathf.Max(
                                                    0f,
                                                    CalculateMaxSpeed(uDist,
                                                                      otherCosAngleToTargetPosDiff,
                                                                      maxBraking));

                                                if (speed < 0.01f) {
                                                    blocked = true;
                                                }

                                                maxSpeed = Mathf.Min(maxSpeed, speed);
                                            }
                                        }

                                        exitTargetPosLoop = true;
                                        break;
                                    }
                                }

                                otherBounds = minBreakPos;
                                totalDist += breakPosDist;
                                otherFrontPos = otherTargetPos;
                            }

                            otherTargetPosIndex++;
                        }

                        if (exitTargetPosLoop) {
                            break;
                        }
                    }
                }

                prevBounds = targetPosDiff;
                prevLength += targetPosDiffLen;
                prevTargetPos = targetPos;
            }

            return otherData.m_nextGridVehicle;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [RedirectReverse]
        [UsedImplicitly]
        private static bool CheckOverlap(Segment3 segment, ushort ignoreVehicle, float maxVelocity) {
            Log._DebugOnlyError("CustomCarAI.CheckOverlap called");
            return false;
        }

        /*[MethodImpl(MethodImplOptions.NoInlining)]
        [RedirectReverse]
        private static ushort CheckOtherVehicle(ushort vehicleID, ref Vehicle vehicleData, ref Vehicle.Frame frameData, ref float maxSpeed, ref bool blocked, ref Vector3 collisionPush, float maxBraking, ushort otherID, ref Vehicle otherData, Vector3 min, Vector3 max, int lodPhysics) {
                Log.Error("CustomCarAI.CheckOtherVehicle called");
                return 0;
        }*/

        [MethodImpl(MethodImplOptions.NoInlining)]
        [RedirectReverse]
        [UsedImplicitly]
        private static ushort CheckCitizen(ushort vehicleId,
                                           ref Vehicle vehicleData,
                                           Segment3 segment,
                                           float lastLen,
                                           float nextLen,
                                           ref float maxSpeed,
                                           ref bool blocked,
                                           float maxBraking,
                                           ushort otherId,
                                           ref CitizenInstance otherData,
                                           Vector3 min,
                                           Vector3 max) {
            Log._DebugOnlyError("CustomCarAI.CheckCitizen called");
            return 0;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [RedirectReverse]
        [UsedImplicitly]
        private static float CalculateMaxSpeed(float targetDistance, float targetSpeed, float maxBraking) {
            Log._DebugOnlyError("CustomCarAI.CalculateMaxSpeed called");
            return 0f;
        }
    }
}
