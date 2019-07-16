#define DEBUGVx

namespace TrafficManager.Custom.AI {
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Runtime.CompilerServices;
    using API.Traffic.Data;
    using API.Traffic.Enums;
    using ColossalFramework;
    using ColossalFramework.Math;
    using CSUtil.Commons;
    using CSUtil.Commons.Benchmark;
    using JetBrains.Annotations;
    using Manager.Impl;
    using PathFinding;
    using RedirectionFramework.Attributes;
    using State;
    using State.ConfigData;
    using Traffic.Data;
    using UnityEngine;

    [TargetType(typeof(CarAI))]
    // TODO inherit from VehicleAI (in order to keep the correct references to `base`)
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
            var vehDebug = GlobalConfig.Instance.Debug.VehicleId == 0
                           || GlobalConfig.Instance.Debug.VehicleId == vehicleId;
            var parkingAiLog = DebugSwitch.BasicParkingAILog.Get() && vehDebug;
            var extendedParkingAiLog = DebugSwitch.ExtendedParkingAILog.Get() && vehDebug;
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
                if (parkingAiLog) {
                    Log._Debug($"CustomCarAI.CustomSimulationStep({vehicleId}): " +
                               $"Path: {vehicleData.m_path}, mainPathState={mainPathState}");
                }
#endif
                var extVehicleManager = Constants.ManagerFactory.ExtVehicleManager;
                var finalPathState = ExtCitizenInstance.ConvertPathStateToSoftPathState(mainPathState);
                if (Options.parkingAI
                    && extVehicleManager.ExtVehicles[vehicleId].vehicleType == ExtVehicleType.PassengerCar)
                {
                    var driverInstanceId = extVehicleManager.GetDriverInstanceId(vehicleId, ref vehicleData);
                    finalPathState = AdvancedParkingManager.Instance.UpdateCarPathState(
                        vehicleId,
                        ref vehicleData,
                        ref Singleton<CitizenManager>.instance.m_instances.m_buffer[driverInstanceId],
                        ref ExtCitizenInstanceManager.Instance.ExtInstances[driverInstanceId],
                        mainPathState);

#if DEBUG
                    if (parkingAiLog) {
                        Log._Debug($"CustomCarAI.CustomSimulationStep({vehicleId}): " +
                                   $"Applied Parking AI logic. Path: {vehicleData.m_path}, " +
                                   $"mainPathState={mainPathState}, finalPathState={finalPathState}");
                    }
#endif
                }

                switch (finalPathState) {
                    case ExtSoftPathState.Ready: {
#if DEBUG
                        if (parkingAiLog) {
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
                        if (parkingAiLog) {
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
                        if (parkingAiLog) {
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
                        if (parkingAiLog) {
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
                        if (parkingAiLog) {
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
            var extVehicleMan = Constants.ManagerFactory.ExtVehicleManager;
            extVehicleMan.UpdateVehiclePosition(vehicleId, ref vehicleData);

            if (!Options.isStockLaneChangerUsed()
                && (vehicleData.m_flags & Vehicle.Flags.Spawned) != 0) {
                extVehicleMan.LogTraffic(vehicleId, ref vehicleData);
            }
            // NON-STOCK CODE END

            var lastFramePosition = vehicleData.GetLastFramePosition();
            int lodPhysics;
            if (Vector3.SqrMagnitude(physicsLodRefPos - lastFramePosition) >= 1100f * 1100f) {
                lodPhysics = 2;
            } else if (Vector3.SqrMagnitude(Singleton<SimulationManager>.instance.m_simulationView.m_position - lastFramePosition) >= 500f * 500f) {
                lodPhysics = 1;
            } else {
                lodPhysics = 0;
            }

            SimulationStep(vehicleId, ref vehicleData, vehicleId, ref vehicleData, lodPhysics);
            if (vehicleData.m_leadingVehicle == 0 && vehicleData.m_trailingVehicle != 0) {
                var vehManager = Singleton<VehicleManager>.instance;
                var trailerId = vehicleData.m_trailingVehicle;
                var numIters = 0;
                while (trailerId != 0) {
                    var trailingVehicle = vehManager.m_vehicles.m_buffer[trailerId].m_trailingVehicle;
                    var info = vehManager.m_vehicles.m_buffer[trailerId].Info;

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

            var privateServiceIndex = ItemClass.GetPrivateServiceIndex(m_info.m_class.m_service);
            var maxBlockCounter = (privateServiceIndex == -1) ? 150 : 100;
            const Vehicle.Flags MASK = Vehicle.Flags.Spawned
                                       | Vehicle.Flags.WaitingPath
                                       | Vehicle.Flags.WaitingSpace;

            if ((vehicleData.m_flags & MASK) == 0
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
            var netManager = Singleton<NetManager>.instance;
            ushort nextSourceNodeId;
            ushort nextTargetNodeId;
            var segmentsBuffer = netManager.m_segments.m_buffer;

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
            var logCalculation = DebugSwitch.CalculateSegmentPosition.Get()
                        && (GlobalConfig.Instance.Debug.NodeId <= 0
                            || curTargetNodeId == GlobalConfig.Instance.Debug.NodeId)
                        && (GlobalConfig.Instance.Debug.ApiExtVehicleType == ExtVehicleType.None
                            || GlobalConfig.Instance.Debug.ApiExtVehicleType == ExtVehicleType.RoadVehicle)
                        && (GlobalConfig.Instance.Debug.VehicleId == 0
                            || GlobalConfig.Instance.Debug.VehicleId == vehicleId);

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
            var prevSegmentInfo = segmentsBuffer[nextPosition.m_segment].Info;
            netManager.m_lanes.m_buffer[nextLaneId].CalculatePositionAndDirection(
                Constants.ByteToFloat(nextOffset), out pos, out dir);

            float braking = m_info.m_braking;
            if ((vehicleData.m_flags & Vehicle.Flags.Emergency2) != 0) {
                braking *= 2f;
            }

            // car position on the Bezier curve of the lane
            var refVehiclePosOnBezier = netManager.m_lanes.m_buffer[curLaneId].CalculatePosition(
                Constants.ByteToFloat(curOffset));

            // ushort currentSegmentId = netManager.m_lanes.m_buffer[prevLaneID].m_segment;
            // this seems to be like the required braking force in order to stop the vehicle within its half length.
            var crazyValue = (0.5f * sqrVelocity / braking) + (m_info.m_generatedInfo.m_size.z * 0.5f);
            var d = Vector3.Distance(lastFrameVehiclePos, refVehiclePosOnBezier);
            var withinBrakingDistance = d >= crazyValue - 1f;

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
                        nextTargetNodeId)) {
                    // NON-STOCK CODE
                    maxSpeed = 0;
                    return;
                }

                ExtVehicleManager.Instance.UpdateVehiclePosition(
                    vehicleId, ref vehicleData/*, lastFrameData.m_velocity.magnitude*/);
                // NON-STOCK CODE END
            }

            if (prevSegmentInfo.m_lanes != null
                && prevSegmentInfo.m_lanes.Length > nextPosition.m_lane) {
                // NON-STOCK CODE START

                var laneSpeedLimit = !Options.customSpeedLimitsEnabled
                                           ? prevSegmentInfo.m_lanes[nextPosition.m_lane].m_speedLimit
                                           : Constants.ManagerFactory.SpeedLimitManager.GetLockFreeGameSpeedLimit(
                                               nextPosition.m_segment,
                                               nextPosition.m_lane,
                                               nextLaneId,
                                               prevSegmentInfo.m_lanes[nextPosition.m_lane]);

                // NON-STOCK CODE END
                maxSpeed = CalculateTargetSpeed(
                    vehicleId, ref vehicleData, laneSpeedLimit,
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
            var netManager = Singleton<NetManager>.instance;
            var segmentInfo = netManager.m_segments.m_buffer[position.m_segment].Info;
            netManager.m_lanes.m_buffer[laneId].CalculatePositionAndDirection(
                Constants.ByteToFloat(offset),
                out pos,
                out dir);

            if (segmentInfo.m_lanes != null
                && segmentInfo.m_lanes.Length > position.m_lane) {
                // NON-STOCK CODE START
                var laneSpeedLimit = !Options.customSpeedLimitsEnabled
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
        public bool CustomStartPathFind(ushort vehicleID,
                                        ref Vehicle vehicleData,
                                        Vector3 startPos,
                                        Vector3 endPos,
                                        bool startBothWays,
                                        bool endBothWays,
                                        bool undergroundTarget) {
#if DEBUG
            var vehDebug = GlobalConfig.Instance.Debug.VehicleId == 0
                           || GlobalConfig.Instance.Debug.VehicleId == vehicleID;
            var logParkingAi = DebugSwitch.BasicParkingAILog.Get() && vehDebug;
            var extendedLogParkingAi = DebugSwitch.ExtendedParkingAILog.Get() && vehDebug;

            if (logParkingAi) {
                Log.Warning($"CustomCarAI.CustomStartPathFind({vehicleID}): called for vehicle " +
                            $"{vehicleID}, startPos={startPos}, endPos={endPos}, " +
                            $"startBothWays={startBothWays}, endBothWays={endBothWays}, " +
                            $"undergroundTarget={undergroundTarget}");
            }
#endif

            ExtVehicleType vehicleType;
#if BENCHMARK
            using (var bm = new Benchmark(null, "OnStartPathFind")) {
#endif
            vehicleType = ExtVehicleManager.Instance.OnStartPathFind(vehicleID, ref vehicleData, null);
            if (vehicleType == ExtVehicleType.None) {
#if DEBUG
                Log.Warning($"CustomCarAI.CustomStartPathFind({vehicleID}): Vehicle {vehicleID} does not have a valid vehicle type!");
#endif
                vehicleType = ExtVehicleType.RoadVehicle;
            }
#if BENCHMARK
            }
#endif
            var info = m_info;
            var allowUnderground = (vehicleData.m_flags & (Vehicle.Flags.Underground
                                                           | Vehicle.Flags.Transition)) != 0;

        if (PathManager.FindPathPosition(
                startPos,
                ItemClass.Service.Road,
        NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle,
                info.m_vehicleType,
                allowUnderground,
                false,
                32f,
                out var startPosA,
                out var startPosB,
                out var startDistSqrA,
                out _)
            && PathManager.FindPathPosition(
                endPos,
                ItemClass.Service.Road,
                NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle,
                info.m_vehicleType,
                undergroundTarget,
                false,
                32f,
                out var endPosA,
                out var endPosB,
                out var endDistSqrA,
                out _)) {
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
                args.vehicleId = vehicleID;
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
                args.ignoreBlocked = IgnoreBlocked(vehicleID, ref vehicleData);
                args.ignoreFlooded = false;
                args.ignoreCosts = false;
                args.randomParking = false;
                args.stablePath = false;
                args.skipQueue = (vehicleData.m_flags & Vehicle.Flags.Spawned) != 0;

                if (CustomPathManager._instance.CustomCreatePath(
                    out var path,
                    ref Singleton<SimulationManager>.instance.m_randomizer,
                    args)) {
#if DEBUG
                    if (logParkingAi) {
                        Log._Debug($"CustomCarAI.CustomStartPathFind({vehicleID}): " +
                                   $"Path-finding starts for vehicle {vehicleID}, path={path}, " +
                                   $"extVehicleType={vehicleType}, startPosA.segment={startPosA.m_segment}, " +
                                   $"startPosA.lane={startPosA.m_lane}, info.m_vehicleType={info.m_vehicleType}, " +
                                   $"endPosA.segment={endPosA.m_segment}, endPosA.lane={endPosA.m_lane}");
                    }
#endif

                    // NON-STOCK CODE END
                    if (vehicleData.m_path != 0u) {
                        Singleton<PathManager>.instance.ReleasePath(vehicleData.m_path);
                    }

                    vehicleData.m_path = path;
                    vehicleData.m_flags |= Vehicle.Flags.WaitingPath;
                    return true;
                }
            }

            return false;
        }

        [RedirectMethod]
        [UsedImplicitly]
        public static ushort CustomCheckOtherVehicle(ushort vehicleID,
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
            if (otherID == vehicleID
                || vehicleData.m_leadingVehicle == otherID
                || vehicleData.m_trailingVehicle == otherID) {
                return otherData.m_nextGridVehicle;
            }

            var info = otherData.Info;
            if (info.m_vehicleType == VehicleInfo.VehicleType.Bicycle) {
                return otherData.m_nextGridVehicle;
            }

            const Vehicle.Flags U = Vehicle.Flags.Underground;
            if (((vehicleData.m_flags | otherData.m_flags) & Vehicle.Flags.Transition) == 0
                && (vehicleData.m_flags & U) != (otherData.m_flags & U)) {
                return otherData.m_nextGridVehicle;
            }

#if DEBUG
            var logLogic = DebugSwitch.ResourceLoading.Get() &&
                         (GlobalConfig.Instance.Debug.ApiExtVehicleType == ExtVehicleType.None
                          || GlobalConfig.Instance.Debug.ApiExtVehicleType == ExtVehicleType.RoadVehicle)
                         && (GlobalConfig.Instance.Debug.VehicleId == 0
                             || GlobalConfig.Instance.Debug.VehicleId == vehicleID);

            if (logLogic) {
                Log._Debug($"CustomCarAI.CustomCheckOtherVehicle({vehicleID}, {otherID}) called.");
            }
#endif

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

            var otherFrameData = otherData.GetLastFrameData();
            if (lodPhysics < 2) {
                float u = default;
                float v = default;
                var segSqrDist = vehicleData.m_segment.DistanceSqr(otherData.m_segment, out u, out v);

                if (segSqrDist < 4f) {
                    var vehPos = vehicleData.m_segment.Position(0.5f);
                    var otherPos = otherData.m_segment.Position(0.5f);
                    var vehBounds = vehicleData.m_segment.b - vehicleData.m_segment.a;
                    if (Vector3.Dot(vehBounds, vehPos - otherPos) < 0f) {
                        collisionPush -= vehBounds.normalized * (0.1f - (segSqrDist * 0.025f));
                    } else {
                        collisionPush += vehBounds.normalized * (0.1f - (segSqrDist * 0.025f));
                    }

                    blocked = true;
                }
            }

            var vehVelocity = frameData.m_velocity.magnitude + 0.01f;
            var otherVehVelocity = otherFrameData.m_velocity.magnitude;
            var otherBreakingDist = (otherVehVelocity * (0.5f + 0.5f * otherVehVelocity / info.m_braking))
                                    + Mathf.Min(1f, otherVehVelocity);
            otherVehVelocity += 0.01f;

            var prevLength = 0f;
            var prevTargetPos = vehicleData.m_segment.b;
            var prevBounds = vehicleData.m_segment.b - vehicleData.m_segment.a;
            var startI = (vehicleData.Info.m_vehicleType == VehicleInfo.VehicleType.Tram) ? 1 : 0;

            for (var i = startI; i < 4; i++) {
                Vector3 targetPos = vehicleData.GetTargetPos(i);
                var targetPosDiff = targetPos - prevTargetPos;
                if (!(Vector3.Dot(prevBounds, targetPosDiff) > 0f)) {
                    continue;
                }

                var targetPosDiffLen = targetPosDiff.magnitude;
                var curSegment = new Segment3(prevTargetPos, targetPos);
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
                    var otherVehFrontPos = otherData.m_segment.a;
                    otherVehFrontPos.y *= 0.5f;

                    if (curSegment.DistanceSqr(otherVehFrontPos, out var u) < 4f) {
                        var otherCosAngleToTargetPosDiff =
                            Vector3.Dot(otherFrameData.m_velocity, targetPosDiff) / targetPosDiffLen;
                        var uDist = prevLength + (targetPosDiffLen * u);

                        if (uDist >= 0.01f) {
                            uDist -= otherCosAngleToTargetPosDiff + 3f;
                            var speed = Mathf.Max(
                                0f,
                                CalculateMaxSpeed(uDist,
                                                  otherCosAngleToTargetPosDiff,
                                                  maxBraking));
                            if (speed < 0.01f) {
                                blocked = true;
                            }

                            var normOtherDir =
                                Vector3.Normalize((Vector3)otherData.m_targetPos0 - otherData.m_segment.a);

                            var blockFactor = 1.2f - (1f / (vehicleData.m_blockCounter * 0.02f + 0.5f));

                            if (Vector3.Dot(targetPosDiff, normOtherDir) > blockFactor * targetPosDiffLen) {
                                maxSpeed = Mathf.Min(maxSpeed, speed);
                            }
                        }

                        break;
                    }

                    if (lodPhysics < 2) {
                        var totalDist = 0f;
                        var otherBreakDist = otherBreakingDist;
                        var otherFrontPos = otherData.m_segment.b;
                        var otherBounds = otherData.m_segment.b - otherData.m_segment.a;
                        var startOtherTargetPosIndex = (info.m_vehicleType == VehicleInfo.VehicleType.Tram) ? 1 : 0;
                        var exitTargetPosLoop = false;
                        var otherTargetPosIndex = startOtherTargetPosIndex;

                        while (otherTargetPosIndex < 4 && otherBreakDist > 0.1f) {
                            Vector3 otherTargetPos;
                            if (otherData.m_leadingVehicle == 0) {
                                otherTargetPos = otherData.GetTargetPos(otherTargetPosIndex);
                            } else {
                                if (otherTargetPosIndex != startOtherTargetPosIndex) {
                                    break;
                                }

                                var vehiclesBuffer = Singleton<VehicleManager>.instance.m_vehicles.m_buffer;
                                otherTargetPos = vehiclesBuffer[otherData.m_leadingVehicle].m_segment.b;
                            }

                            var minBreakPos = Vector3.ClampMagnitude(otherTargetPos - otherFrontPos,
                                                                     otherBreakDist);

                            if (Vector3.Dot(otherBounds, minBreakPos) > 0f) {
                                otherTargetPos = otherFrontPos + minBreakPos;

                                var breakPosDist = minBreakPos.magnitude;
                                otherBreakDist -= breakPosDist;

                                var otherVehNextSegment = new Segment3(otherFrontPos, otherTargetPos);
                                otherVehNextSegment.a.y *= 0.5f;
                                otherVehNextSegment.b.y *= 0.5f;

                                if (breakPosDist > 0.01f) {
                                    var otherVehNextSegmentDistToCurSegment
                                        = otherID >= vehicleID
                                              ? curSegment.DistanceSqr(
                                                  otherVehNextSegment,
                                                  out var otherVehNextSegU,
                                                  out var otherVehNextSegV)
                                              : otherVehNextSegment.DistanceSqr(
                                                  curSegment,
                                                  out otherVehNextSegV,
                                                  out otherVehNextSegU);

                                    if (otherVehNextSegmentDistToCurSegment < 4f) {
                                        var uDist = prevLength + (targetPosDiffLen * otherVehNextSegU);
                                        var vDist = totalDist + (breakPosDist * otherVehNextSegV) + 0.1f;

                                        if (uDist >= 0.01f && uDist * otherVehVelocity > vDist * vehVelocity) {
                                            var otherCosAngleToTargetPosDiff =
                                                Vector3.Dot(
                                                    otherFrameData.m_velocity,
                                                    targetPosDiff) / targetPosDiffLen;
                                            if (uDist >= 0.01f) {
                                                uDist -= otherCosAngleToTargetPosDiff
                                                         + 1f
                                                         + otherData.Info.m_generatedInfo.m_size.z;
                                                var speed = Mathf.Max(
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
            Log.Error("CustomCarAI.CheckOverlap called");
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
            Log.Error("CustomCarAI.CheckCitizen called");
            return 0;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [RedirectReverse]
        [UsedImplicitly]
        private static float CalculateMaxSpeed(float targetDistance, float targetSpeed, float maxBraking) {
            Log.Error("CustomCarAI.CalculateMaxSpeed called");
            return 0f;
        }
    }
}