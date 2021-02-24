// #define DEBUGV

namespace TrafficManager.Custom.AI {
    using ColossalFramework.Math;
    using ColossalFramework;
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
    }
}
