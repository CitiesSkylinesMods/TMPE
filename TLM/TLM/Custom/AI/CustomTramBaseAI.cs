namespace TrafficManager.Custom.AI {
    using System;
    using System.Runtime.CompilerServices;
    using API.Traffic.Data;
    using API.Traffic.Enums;
    using ColossalFramework;
    using ColossalFramework.Math;
    using CSUtil.Commons;
    using JetBrains.Annotations;
    using Manager.Impl;
    using PathFinding;
    using RedirectionFramework.Attributes;
    using State;
    using State.ConfigData;
    using UnityEngine;

    // TODO inherit from VehicleAI (in order to keep the correct references to `base`)
    [TargetType(typeof(TramBaseAI))]
    public class CustomTramBaseAI : TramBaseAI {
        [RedirectMethod]
        public void CustomSimulationStep(ushort vehicleId, ref Vehicle vehicleData, Vector3 physicsLodRefPos) {
            var extVehicleMan = Constants.ManagerFactory.ExtVehicleManager;

            if ((vehicleData.m_flags & Vehicle.Flags.WaitingPath) != 0) {
                var pathFindFlags = Singleton<PathManager>
                                    .instance.m_pathUnits.m_buffer[vehicleData.m_path].m_pathFindFlags;

                if ((pathFindFlags & PathUnit.FLAG_READY) != 0) {
                    try {
                        PathfindSuccess(vehicleId, ref vehicleData);
                        PathFindReady(vehicleId, ref vehicleData);
                    } catch (Exception e) {
                        Log.Warning($"TramBaseAI.PathFindSuccess/PathFindReady({vehicleId}) " +
                                    $"threw an exception: {e}");
                        vehicleData.m_flags &= ~Vehicle.Flags.WaitingPath;
                        Singleton<PathManager>.instance.ReleasePath(vehicleData.m_path);
                        vehicleData.m_path = 0u;
                        PathfindFailure(vehicleId, ref vehicleData);
                        return;
                    }
                } else if ((pathFindFlags & PathUnit.FLAG_FAILED) != 0 || vehicleData.m_path == 0) {
                    vehicleData.m_flags &= ~Vehicle.Flags.WaitingPath;
                    Singleton<PathManager>.instance.ReleasePath(vehicleData.m_path);
                    vehicleData.m_path = 0u;
                    PathfindFailure(vehicleId, ref vehicleData);
                    return;
                }
            } else {
                if ((vehicleData.m_flags & Vehicle.Flags.WaitingSpace) != 0) {
                    TrySpawn(vehicleId, ref vehicleData);
                }
            }

            // NON-STOCK CODE START
            extVehicleMan.UpdateVehiclePosition(vehicleId, ref vehicleData);

            if (!Options.isStockLaneChangerUsed() && (vehicleData.m_flags & Vehicle.Flags.Spawned) != 0) {
                // Advanced AI traffic measurement
                extVehicleMan.LogTraffic(vehicleId, ref vehicleData);
            }

            // NON-STOCK CODE END
            var instance = Singleton<VehicleManager>.instance;
            var info = instance.m_vehicles.m_buffer[vehicleId].Info;
            info.m_vehicleAI.SimulationStep(
                vehicleId,
                ref instance.m_vehicles.m_buffer[vehicleId],
                vehicleId,
                ref vehicleData,
                0);

            if ((vehicleData.m_flags & (Vehicle.Flags.Created
                                        | Vehicle.Flags.Deleted)) != Vehicle.Flags.Created) {
                return;
            }

            var trailingVehicle = instance.m_vehicles.m_buffer[vehicleId].m_trailingVehicle;
            var num = 0;

            while (trailingVehicle != 0) {
                info = instance.m_vehicles.m_buffer[trailingVehicle].Info;
                info.m_vehicleAI.SimulationStep(
                    trailingVehicle,
                    ref instance.m_vehicles.m_buffer[trailingVehicle],
                    vehicleId,
                    ref vehicleData,
                    0);

                if ((vehicleData.m_flags & (Vehicle.Flags.Created
                                            | Vehicle.Flags.Deleted)) != Vehicle.Flags.Created) {
                    return;
                }

                trailingVehicle = instance.m_vehicles.m_buffer[trailingVehicle].m_trailingVehicle;

                if (++num > 16384) {
                    CODebugBase<LogChannel>.Error(LogChannel.Core,
                                                  $"Invalid list detected!\n{Environment.StackTrace}");
                    break;
                }
            }

            if ((vehicleData.m_flags & (Vehicle.Flags.Spawned
                                        | Vehicle.Flags.WaitingPath
                                        | Vehicle.Flags.WaitingSpace
                                        | Vehicle.Flags.WaitingCargo)) == 0) {
                Singleton<VehicleManager>.instance.ReleaseVehicle(vehicleId);
            } else if (vehicleData.m_blockCounter == 255) {
                // NON-STOCK CODE START
                if (VehicleBehaviorManager.Instance.MayDespawn(ref vehicleData)) {
                    // NON-STOCK CODE END
                    Singleton<VehicleManager>.instance.ReleaseVehicle(vehicleId);
                } // NON-STOCK CODE
            }
        }

        [RedirectMethod]
        [UsedImplicitly]
        public bool CustomStartPathFind(ushort vehicleId,
                                        ref Vehicle vehicleData,
                                        Vector3 startPos,
                                        Vector3 endPos,
                                        bool startBothWays,
                                        bool endBothWays) {
            ExtVehicleManager.Instance.OnStartPathFind(vehicleId, ref vehicleData, null); // NON-STOCK CODE

            var info = m_info;
            bool allowUnderground;
            bool allowUnderground2;
            if (info.m_vehicleType == VehicleInfo.VehicleType.Metro) {
                allowUnderground = true;
                allowUnderground2 = true;
            } else {
                allowUnderground = (vehicleData.m_flags & (Vehicle.Flags.Underground
                                                           | Vehicle.Flags.Transition)) != 0;
                allowUnderground2 = false;
            }

            if (PathManager.FindPathPosition(
                    startPos,
                    ItemClass.Service.Road,
                    NetInfo.LaneType.Vehicle,
                    info.m_vehicleType,
                    allowUnderground,
                    false,
                    32f,
                    out var startPosA,
                    out var startPosB,
                    out var startSqrDistA,
                    out var startSqrDistB)
                && PathManager.FindPathPosition(
                    endPos,
                    ItemClass.Service.Road,
                    NetInfo.LaneType.Vehicle,
                    info.m_vehicleType,
                    allowUnderground2,
                    false,
                    32f,
                    out var endPosA,
                    out var endPosB,
                    out var endSqrDistA,
                    out var endSqrDistB)) {
                if (!startBothWays || startSqrDistB > startSqrDistA * 1.2f) {
                    startPosB = default;
                }

                if (!endBothWays || endSqrDistB > endSqrDistA * 1.2f) {
                    endPosB = default;
                }

                // NON-STOCK CODE START
                PathCreationArgs args;
                args.extPathType = ExtPathType.None;
                args.extVehicleType = ExtVehicleType.Tram;
                args.vehicleId = vehicleId;
                args.spawned = (vehicleData.m_flags & Vehicle.Flags.Spawned) != 0;
                args.buildIndex = Singleton<SimulationManager>.instance.m_currentBuildIndex;
                args.startPosA = startPosA;
                args.startPosB = startPosB;
                args.endPosA = endPosA;
                args.endPosB = endPosB;
                args.vehiclePosition = default;
                args.laneTypes = NetInfo.LaneType.Vehicle;
                args.vehicleTypes = info.m_vehicleType;
                args.maxLength = 20000f;
                args.isHeavyVehicle = false;
                args.hasCombustionEngine = false;
                args.ignoreBlocked = IgnoreBlocked(vehicleId, ref vehicleData);
                args.ignoreFlooded = false;
                args.ignoreCosts = false;
                args.randomParking = false;
                args.stablePath = true;
                args.skipQueue = true;

                if (CustomPathManager._instance.CustomCreatePath(
                    out var path,
                    ref Singleton<SimulationManager>.instance.m_randomizer,
                    args)) {
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
        public void CustomCalculateSegmentPosition(ushort vehicleId,
                                                   ref Vehicle vehicleData,
                                                   PathUnit.Position nextPosition,
                                                   PathUnit.Position prevPosition,
                                                   uint prevLaneId,
                                                   byte prevOffset,
                                                   PathUnit.Position refPosition,
                                                   uint refLaneId,
                                                   byte refOffset,
                                                   int index,
                                                   out Vector3 pos,
                                                   out Vector3 dir,
                                                   out float maxSpeed) {
            var netManager = Singleton<NetManager>.instance;
            ushort prevSourceNodeId;
            ushort prevTargetNodeId;
            if (prevOffset < prevPosition.m_offset) {
                prevSourceNodeId = netManager.m_segments.m_buffer[prevPosition.m_segment].m_startNode;
                prevTargetNodeId = netManager.m_segments.m_buffer[prevPosition.m_segment].m_endNode;
            } else {
                prevSourceNodeId = netManager.m_segments.m_buffer[prevPosition.m_segment].m_endNode;
                prevTargetNodeId = netManager.m_segments.m_buffer[prevPosition.m_segment].m_startNode;
            }

            var refTargetNodeId = refOffset == 0
                                      ? netManager.m_segments.m_buffer[refPosition.m_segment].m_startNode
                                      : netManager.m_segments.m_buffer[refPosition.m_segment].m_endNode;

#if DEBUG
            var logLogic = DebugSwitch.CalculateSegmentPosition.Get()
                           && (DebugSettings.NodeId <= 0
                               || refTargetNodeId == DebugSettings.NodeId)
                           && (GlobalConfig.Instance.Debug.ApiExtVehicleType == ExtVehicleType.None
                               || GlobalConfig.Instance.Debug.ApiExtVehicleType == ExtVehicleType.Tram)
                           && (DebugSettings.VehicleId == 0
                               || DebugSettings.VehicleId == vehicleId);

            if (logLogic) {
                Log._Debug($"CustomTramBaseAI.CustomCalculateSegmentPosition({vehicleId}) called.\n" +
                           $"\trefPosition.m_segment={refPosition.m_segment}, " +
                           $"refPosition.m_offset={refPosition.m_offset}\n" +
                           $"\tprevPosition.m_segment={prevPosition.m_segment}, " +
                           $"prevPosition.m_offset={prevPosition.m_offset}\n" +
                           $"\tnextPosition.m_segment={nextPosition.m_segment}, " +
                           $"nextPosition.m_offset={nextPosition.m_offset}\n" +
                           $"\trefLaneId={refLaneId}, refOffset={refOffset}\n" +
                           $"\tprevLaneId={prevLaneId}, prevOffset={prevOffset}\n" +
                           $"\tprevSourceNodeId={prevSourceNodeId}, prevTargetNodeId={prevTargetNodeId}\n" +
                           $"\trefTargetNodeId={refTargetNodeId}, refTargetNodeId={refTargetNodeId}\n" +
                           $"\tindex={index}");
            }
#endif
            var lastFrameData = vehicleData.GetLastFrameData();
            var sqrVelocity = lastFrameData.m_velocity.sqrMagnitude;

            netManager.m_lanes.m_buffer[prevLaneId].CalculatePositionAndDirection(
                Constants.ByteToFloat(prevOffset),
                out pos,
                out dir);
            var b = netManager.m_lanes.m_buffer[refLaneId].CalculatePosition(
                Constants.ByteToFloat(refOffset));
            var a = lastFrameData.m_position;
            var a2 = lastFrameData.m_position;
            var b2 = lastFrameData.m_rotation * new Vector3(0f, 0f, m_info.m_generatedInfo.m_wheelBase * 0.5f);
            a += b2;
            a2 -= b2;
            var crazyValue = 0.5f * sqrVelocity / m_info.m_braking;
            var a3 = Vector3.Distance(a, b);
            var b3 = Vector3.Distance(a2, b);

            if (Mathf.Min(a3, b3) >= crazyValue - 1f) {
                // dead stock code
                /*Segment3 segment;
                segment.a = pos;
                if (prevOffset < prevPosition.m_offset) {
                        segment.b = pos + dir.normalized * this.m_info.m_generatedInfo.m_size.z;
                } else {
                        segment.b = pos - dir.normalized * this.m_info.m_generatedInfo.m_size.z;
                }*/
                if (prevSourceNodeId == refTargetNodeId) {
                    if (!VehicleBehaviorManager.Instance.MayChangeSegment(
                            vehicleId,
                            ref vehicleData,
                            sqrVelocity,
                            ref refPosition,
                            ref netManager.m_segments.m_buffer[refPosition.m_segment],
                            refTargetNodeId,
                            refLaneId,
                            ref prevPosition,
                            prevSourceNodeId,
                            ref netManager.m_nodes.m_buffer[prevSourceNodeId],
                            prevLaneId,
                            ref nextPosition,
                            prevTargetNodeId)) {
                        maxSpeed = 0;
                        return;
                    }

                    ExtVehicleManager.Instance.UpdateVehiclePosition(
                            vehicleId,
                            ref vehicleData
                            /*, lastFrameData.m_velocity.magnitude*/
                        );
                }
            }

            var info = netManager.m_segments.m_buffer[prevPosition.m_segment].Info;
            if (info.m_lanes != null && info.m_lanes.Length > prevPosition.m_lane) {
                var speedLimit = Options.customSpeedLimitsEnabled
                                     ? SpeedLimitManager.Instance.GetLockFreeGameSpeedLimit(
                                         prevPosition.m_segment,
                                         prevPosition.m_lane,
                                         prevLaneId,
                                         info.m_lanes[prevPosition.m_lane])
                                     : info.m_lanes[prevPosition.m_lane].m_speedLimit; // NON-STOCK CODE
                maxSpeed = CalculateTargetSpeed(
                    vehicleId,
                    ref vehicleData,
                    speedLimit,
                    netManager.m_lanes.m_buffer[prevLaneId].m_curve);
            } else {
                maxSpeed = CalculateTargetSpeed(vehicleId, ref vehicleData, 1f, 0f);
            }
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
            var instance = Singleton<NetManager>.instance;
            instance.m_lanes.m_buffer[laneId].CalculatePositionAndDirection(
                Constants.ByteToFloat(offset),
                out pos,
                out dir);
            var info = instance.m_segments.m_buffer[position.m_segment].Info;

            if (info.m_lanes != null
                && info.m_lanes.Length > position.m_lane)
            {
                var speedLimit = Options.customSpeedLimitsEnabled
                                     ? SpeedLimitManager.Instance.GetLockFreeGameSpeedLimit(
                                         position.m_segment,
                                         position.m_lane,
                                         laneId,
                                         info.m_lanes[position.m_lane])
                                     : info.m_lanes[position.m_lane].m_speedLimit; // NON-STOCK CODE
                maxSpeed = CalculateTargetSpeed(
                    vehicleId,
                    ref vehicleData,
                    speedLimit,
                    instance.m_lanes.m_buffer[laneId].m_curve);
            } else {
                maxSpeed = CalculateTargetSpeed(vehicleId, ref vehicleData, 1f, 0f);
            }
        }

        [RedirectMethod]
        public void CustomSimulationStep(ushort vehicleId,
                                         ref Vehicle vehicleData,
                                         ref Vehicle.Frame frameData,
                                         ushort leaderId,
                                         ref Vehicle leaderData,
                                         int lodPhysics) {
#if DEBUG
            var logLogic = DebugSwitch.TramBaseAISimulationStep.Get()
                        && DebugSettings.NodeId == vehicleId;
#endif
            var leadingVehicle = vehicleData.m_leadingVehicle;
            var currentFrameIndex = Singleton<SimulationManager>.instance.m_currentFrameIndex;

            var leaderInfo = leaderId != vehicleId ? leaderData.Info : m_info;
            var tramBaseAi = leaderInfo.m_vehicleAI as TramBaseAI;

            if (leadingVehicle != 0) {
                frameData.m_position += frameData.m_velocity * 0.4f;
            } else {
                frameData.m_position += frameData.m_velocity * 0.5f;
            }

            frameData.m_swayPosition += frameData.m_swayVelocity * 0.5f;
            var wheelBaseRot = frameData.m_rotation
                               * new Vector3(0f, 0f, m_info.m_generatedInfo.m_wheelBase * 0.5f);
            var posAfterWheelRot = frameData.m_position + wheelBaseRot;
            var posBeforeWheelRot = frameData.m_position - wheelBaseRot;

            var acceleration = m_info.m_acceleration;
            var braking = m_info.m_braking;
            var curSpeed = frameData.m_velocity.magnitude;

            var afterRotToTargetPos1Diff = (Vector3)vehicleData.m_targetPos1 - posAfterWheelRot;
            var afterRotToTargetPos1DiffSqrMag = afterRotToTargetPos1Diff.sqrMagnitude;

            var curInvRot = Quaternion.Inverse(frameData.m_rotation);
            var curveTangent = curInvRot * frameData.m_velocity;

#if DEBUG
            if (logLogic) {
                Log._Debug($"CustomTramBaseAI.SimulationStep({vehicleId}): " +
                           "================================================");
                Log._Debug($"CustomTramBaseAI.SimulationStep({vehicleId}): " +
                           $"leadingVehicle={leadingVehicle} frameData.m_position={frameData.m_position} " +
                           $"frameData.m_swayPosition={frameData.m_swayPosition} " +
                           $"wheelBaseRot={wheelBaseRot} posAfterWheelRot={posAfterWheelRot} " +
                           $"posBeforeWheelRot={posBeforeWheelRot} acceleration={acceleration} " +
                           $"braking={braking} curSpeed={curSpeed} " +
                           $"afterRotToTargetPos1Diff={afterRotToTargetPos1Diff} " +
                           $"afterRotToTargetPos1DiffSqrMag={afterRotToTargetPos1DiffSqrMag} " +
                           $"curInvRot={curInvRot} curveTangent={curveTangent} " +
                           $"this.m_info.m_generatedInfo.m_wheelBase={m_info.m_generatedInfo.m_wheelBase}");
            }
#endif

            var forward = Vector3.forward;
            var targetMotion = Vector3.zero;
            var targetSpeed = 0f;
            var motionFactor = 0.5f;
            var turnAngle = 0f;
            if (leadingVehicle != 0) {
                var vehMan = Singleton<VehicleManager>.instance;
                var leadingVehLastFrameData = vehMan.m_vehicles.m_buffer[leadingVehicle].GetLastFrameData();
                var leadingVehInfo = vehMan.m_vehicles.m_buffer[leadingVehicle].Info;

                float attachOffset;
                if ((vehicleData.m_flags & Vehicle.Flags.Inverted) != 0) {
                    attachOffset = m_info.m_attachOffsetBack - (m_info.m_generatedInfo.m_size.z * 0.5f);
                } else {
                    attachOffset = m_info.m_attachOffsetFront - (m_info.m_generatedInfo.m_size.z * 0.5f);
                }

                float leadingAttachOffset;
                if ((vehMan.m_vehicles.m_buffer[leadingVehicle].m_flags & Vehicle.Flags.Inverted) != 0) {
                    leadingAttachOffset = leadingVehInfo.m_attachOffsetFront
                                          - (leadingVehInfo.m_generatedInfo.m_size.z * 0.5f);
                } else {
                    leadingAttachOffset = leadingVehInfo.m_attachOffsetBack
                                          - (leadingVehInfo.m_generatedInfo.m_size.z * 0.5f);
                }

                var curPosMinusRotAttachOffset = frameData.m_position
                                                 - (frameData.m_rotation * new Vector3(0f, 0f, attachOffset));
                var leadingPosPlusRotAttachOffset = leadingVehLastFrameData.m_position
                                                    + (leadingVehLastFrameData.m_rotation
                                                       * new Vector3(0f, 0f, leadingAttachOffset));

                wheelBaseRot = leadingVehLastFrameData.m_rotation
                               * new Vector3(0f, 0f, leadingVehInfo.m_generatedInfo.m_wheelBase * 0.5f);
                var leadingPosBeforeWheelRot = leadingVehLastFrameData.m_position - wheelBaseRot;

                if (Vector3.Dot(
                        vehicleData.m_targetPos1 - vehicleData.m_targetPos0,
                        (Vector3)vehicleData.m_targetPos0 - posBeforeWheelRot) < 0f
                    && vehicleData.m_path != 0u
                    && (leaderData.m_flags & Vehicle.Flags.WaitingPath) == 0)
                {
                    var someIndex = -1;
                    UpdatePathTargetPositions(
                        tramBaseAi,
                        vehicleId,
                        ref vehicleData,
                        vehicleData.m_targetPos0,
                        posBeforeWheelRot,
                        0,
                        ref leaderData,
                        ref someIndex,
                        0,
                        0,
                        Vector3.SqrMagnitude(posBeforeWheelRot
                                             - (Vector3)vehicleData.m_targetPos0) + 1f,
                        1f);
                    afterRotToTargetPos1DiffSqrMag = 0f;
                }

                var attachRotDist = Mathf.Max(
                    Vector3.Distance(curPosMinusRotAttachOffset, leadingPosPlusRotAttachOffset),
                    2f);

                const float ONE = 1f;
                var attachRotSqrDist = attachRotDist * attachRotDist;
                const float ONE_SQR = ONE * ONE;
                var i = 0;
                if (afterRotToTargetPos1DiffSqrMag < attachRotSqrDist) {
                    if (vehicleData.m_path != 0u
                        && (leaderData.m_flags & Vehicle.Flags.WaitingPath) == 0) {
                        UpdatePathTargetPositions(
                            tramBaseAi,
                            vehicleId,
                            ref vehicleData,
                            posBeforeWheelRot,
                            posAfterWheelRot,
                            0,
                            ref leaderData,
                            ref i,
                            1,
                            2,
                            attachRotSqrDist,
                            ONE_SQR);
                    }

                    while (i < 4) {
                        vehicleData.SetTargetPos(i, vehicleData.GetTargetPos(i - 1));
                        i++;
                    }

                    afterRotToTargetPos1Diff = (Vector3)vehicleData.m_targetPos1 - posAfterWheelRot;
                    afterRotToTargetPos1DiffSqrMag = afterRotToTargetPos1Diff.sqrMagnitude;
                }

                afterRotToTargetPos1Diff = curInvRot * afterRotToTargetPos1Diff;

                var negTotalAttachLen =
                    -(((m_info.m_generatedInfo.m_wheelBase + leadingVehInfo.m_generatedInfo.m_wheelBase) * 0.5f) +
                      attachOffset + leadingAttachOffset);
                var hasPath = false;

                if (vehicleData.m_path != 0u
                    && (leaderData.m_flags & Vehicle.Flags.WaitingPath) == 0)
                {
                    if (Line3.Intersect(
                        posAfterWheelRot,
                        vehicleData.m_targetPos1,
                        leadingPosBeforeWheelRot,
                        negTotalAttachLen,
                        out var u1,
                        out var u2))
                    {
                        targetMotion = afterRotToTargetPos1Diff
                                       * Mathf.Clamp(Mathf.Min(u1, u2) / 0.6f, 0f, 2f);
                    } else {
                        Line3.DistanceSqr(posAfterWheelRot,
                                          vehicleData.m_targetPos1,
                                          leadingPosBeforeWheelRot, out u1);
                        targetMotion = afterRotToTargetPos1Diff
                                       * Mathf.Clamp(u1 / 0.6f, 0f, 2f);
                    }

                    hasPath = true;
                }

                if (hasPath) {
                    if (Vector3.Dot(leadingPosBeforeWheelRot - posAfterWheelRot,
                                    posAfterWheelRot - posBeforeWheelRot) < 0f) {
                        motionFactor = 0f;
                    }
                } else {
                    var leadingPosBeforeToAfterWheelRotDist = Vector3.Distance(
                        leadingPosBeforeWheelRot, posAfterWheelRot);
                    motionFactor = 0f;
                    targetMotion = curInvRot * ((leadingPosBeforeWheelRot - posAfterWheelRot) *
                                                (Mathf.Max(
                                                     0f,
                                                     leadingPosBeforeToAfterWheelRotDist - negTotalAttachLen) /
                                                 Mathf.Max(1f, leadingPosBeforeToAfterWheelRotDist * 0.6f)));
                }
            } else {
                var estimatedFrameDist =
                    ((curSpeed + acceleration) * (0.5f + 0.5f * (curSpeed + acceleration) / braking)) +
                    ((m_info.m_generatedInfo.m_size.z - m_info.m_generatedInfo.m_wheelBase) * 0.5f);
                var maxSpeedAdd = Mathf.Max(curSpeed + acceleration, 2f);
                var meanSpeedAdd = Mathf.Max((estimatedFrameDist - maxSpeedAdd) / 2f, 2f);
                var maxSpeedAddSqr = maxSpeedAdd * maxSpeedAdd;
                var meanSpeedAddSqr = meanSpeedAdd * meanSpeedAdd;

                if (Vector3.Dot(
                        vehicleData.m_targetPos1 - vehicleData.m_targetPos0,
                        (Vector3)vehicleData.m_targetPos0 - posBeforeWheelRot) < 0f
                    && vehicleData.m_path != 0u
                    && (leaderData.m_flags & (Vehicle.Flags.WaitingPath | Vehicle.Flags.Stopped)) == 0)
                {
                    var someIndex = -1;
                    UpdatePathTargetPositions(
                        tramBaseAi,
                        vehicleId,
                        ref vehicleData,
                        vehicleData.m_targetPos0,
                        posBeforeWheelRot,
                        leaderId,
                        ref leaderData,
                        ref someIndex,
                        0,
                        0,
                        Vector3.SqrMagnitude(
                            posBeforeWheelRot - (Vector3)vehicleData.m_targetPos0) + 1f,
                        1f);
                    afterRotToTargetPos1DiffSqrMag = 0f;
#if DEBUG
                    if (logLogic) {
                        Log._Debug($"CustomTramBaseAI.SimulationStep({vehicleId}): dot < 0");
                    }
#endif
                }
#if DEBUG
                if (logLogic) {
                    Log._Debug($"CustomTramBaseAI.SimulationStep({vehicleId}): " +
                               $"Leading vehicle is 0. vehicleData.m_targetPos0={vehicleData.m_targetPos0} " +
                               $"vehicleData.m_targetPos1={vehicleData.m_targetPos1} " +
                               $"posBeforeWheelRot={posBeforeWheelRot} posBeforeWheelRot={posAfterWheelRot} " +
                               $"estimatedFrameDist={estimatedFrameDist} maxSpeedAdd={maxSpeedAdd} " +
                               $"meanSpeedAdd={meanSpeedAdd} maxSpeedAddSqr={maxSpeedAddSqr} " +
                               $"meanSpeedAddSqr={meanSpeedAddSqr} " +
                               $"afterRotToTargetPos1DiffSqrMag={afterRotToTargetPos1DiffSqrMag}");
                }
#endif
                var posIndex = 0;
                var hasValidPathTargetPos = false;

                if ((afterRotToTargetPos1DiffSqrMag < maxSpeedAddSqr
                     || vehicleData.m_targetPos3.w < 0.01f)
                    && (leaderData.m_flags & (Vehicle.Flags.WaitingPath | Vehicle.Flags.Stopped)) == 0)
                {
                    if (vehicleData.m_path != 0u) {
                        UpdatePathTargetPositions(
                            tramBaseAi,
                            vehicleId,
                            ref vehicleData,
                            posBeforeWheelRot,
                            posAfterWheelRot,
                            leaderId,
                            ref leaderData,
                            ref posIndex,
                            1,
                            4,
                            maxSpeedAddSqr,
                            meanSpeedAddSqr);
                    }

                    if (posIndex < 4) {
                        hasValidPathTargetPos = true;
                        while (posIndex < 4) {
                            vehicleData.SetTargetPos(posIndex, vehicleData.GetTargetPos(posIndex - 1));
                            posIndex++;
                        }
                    }

                    afterRotToTargetPos1Diff = (Vector3)vehicleData.m_targetPos1 - posAfterWheelRot;
                    afterRotToTargetPos1DiffSqrMag = afterRotToTargetPos1Diff.sqrMagnitude;
                }

#if DEBUG
                if (logLogic) {
                    Log._Debug($"CustomTramBaseAI.SimulationStep({vehicleId}): posIndex={posIndex} " +
                               $"hasValidPathTargetPos={hasValidPathTargetPos}");
                }
#endif
                if (leaderData.m_path != 0u
                    && (leaderData.m_flags & Vehicle.Flags.WaitingPath) == 0)
                {
                    var netMan = Singleton<NetManager>.instance;
                    var leaderPathPosIndex = leaderData.m_pathPositionIndex;
                    var leaderLastPathOffset = leaderData.m_lastPathOffset;
                    if (leaderPathPosIndex == 255) {
                        leaderPathPosIndex = 0;
                    }

                    var leaderLen = 1f + leaderData.CalculateTotalLength(leaderId, out var noise);

#if DEBUG
                    if (logLogic) {
                        Log._Debug($"CustomTramBaseAI.SimulationStep({vehicleId}): " +
                                   $"leaderPathPosIndex={leaderPathPosIndex} " +
                                   $"leaderLastPathOffset={leaderLastPathOffset} " +
                                   $"leaderPathPosIndex={leaderPathPosIndex} leaderLen={leaderLen}");
                    }
#endif
                    // reserve space / add traffic
                    var pathMan = Singleton<PathManager>.instance;
                    if (pathMan.m_pathUnits.m_buffer[leaderData.m_path].GetPosition(
                        leaderPathPosIndex >> 1,
                        out var pathPos))
                    {
                        netMan.m_segments.m_buffer[pathPos.m_segment].AddTraffic(
                            Mathf.RoundToInt(leaderLen * 2.5f),
                            noise);
                        var reservedSpaceOnCurrentLane = false;

                        if ((leaderPathPosIndex & 1) == 0 || leaderLastPathOffset == 0) {
                            var laneId = PathManager.GetLaneID(pathPos);
                            if (laneId != 0u) {
                                var curPathOffsetPos = netMan
                                                       .m_lanes.m_buffer[laneId]
                                                       .CalculatePosition(pathPos.m_offset * 0.003921569f);
                                var speedAdd = 0.5f * curSpeed * curSpeed / m_info.m_braking;
                                var afterWheelRotCurPathOffsetDist = Vector3.Distance(
                                    posAfterWheelRot,
                                    curPathOffsetPos);
                                var beforeWheelRotCurPathOffsetDist = Vector3.Distance(
                                    posBeforeWheelRot,
                                    curPathOffsetPos);
                                if (Mathf.Min(afterWheelRotCurPathOffsetDist,
                                              beforeWheelRotCurPathOffsetDist) >= speedAdd - 1f) {
                                    netMan.m_lanes.m_buffer[laneId].ReserveSpace(leaderLen);
                                    reservedSpaceOnCurrentLane = true;
                                }
                            }
                        }

                        if (!reservedSpaceOnCurrentLane
                            && pathMan.m_pathUnits.m_buffer[leaderData.m_path]
                                      .GetNextPosition(leaderPathPosIndex >> 1, out pathPos)) {
                            var nextLaneId = PathManager.GetLaneID(pathPos);
                            if (nextLaneId != 0u) {
                                netMan.m_lanes.m_buffer[nextLaneId].ReserveSpace(leaderLen);
                            }
                        }
                    }

                    if ((currentFrameIndex >> 4 & 15u) == (ulong)(leaderId & 15)) {
                        // check if vehicle can proceed to next path position
                        var canProceeed = false;
                        var curLeaderPathId = leaderData.m_path;
                        var curLeaderPathPosIndex = leaderPathPosIndex >> 1;
                        var k = 0;
                        while (k < 5) {
                            if (PathUnit.GetNextPosition(
                                ref curLeaderPathId,
                                ref curLeaderPathPosIndex,
                                out pathPos,
                                out var invalidPos)) {
                                var laneId = PathManager.GetLaneID(pathPos);
                                if (laneId != 0u && !netMan.m_lanes.m_buffer[laneId].CheckSpace(leaderLen)) {
                                    k++;
                                    continue;
                                }
                            }

                            if (invalidPos) {
                                InvalidPath(vehicleId, ref vehicleData, leaderId, ref leaderData);
                            }

                            canProceeed = true;
                            break;
                        }

                        if (!canProceeed) {
                            leaderData.m_flags |= Vehicle.Flags.Congestion;
                        }
                    }
                }

                float maxSpeed;
                if ((leaderData.m_flags & Vehicle.Flags.Stopped) != 0) {
                    maxSpeed = 0f;
#if DEBUG
                    if (logLogic) {
                        Log._Debug($"CustomTramBaseAI.SimulationStep({vehicleId}): " +
                                   $"Vehicle is stopped. maxSpeed={maxSpeed}");
                    }
#endif
                } else {
                    maxSpeed = Mathf.Min(vehicleData.m_targetPos1.w, GetMaxSpeed(leaderId, ref leaderData));
#if DEBUG
                    if (logLogic) {
                        Log._Debug($"CustomTramBaseAI.SimulationStep({vehicleId}): " +
                                   $"Vehicle is not stopped. maxSpeed={maxSpeed}");
                    }
#endif
                }

#if DEBUG
                if (logLogic) {
                    Log._Debug($"CustomTramBaseAI.SimulationStep({vehicleId}): " +
                               $"Start of second part. curSpeed={curSpeed} curInvRot={curInvRot}");
                }
#endif

                afterRotToTargetPos1Diff = curInvRot * afterRotToTargetPos1Diff;
#if DEBUG
                if (logLogic) {
                    Log._Debug($"CustomTramBaseAI.SimulationStep({vehicleId}): " +
                               $"afterRotToTargetPos1Diff={afterRotToTargetPos1Diff} " +
                               $"(old afterRotToTargetPos1DiffSqrMag={afterRotToTargetPos1DiffSqrMag})");
                }
#endif
                var zero = Vector3.zero;
                var blocked = false;
                var forwardLen = 0f;
                if (afterRotToTargetPos1DiffSqrMag > 1f) { // TODO why is this not recalculated?
                    forward = VectorUtils.NormalizeXZ(afterRotToTargetPos1Diff, out forwardLen);
#if DEBUG
                    if (logLogic) {
                        Log._Debug($"CustomTramBaseAI.SimulationStep({vehicleId}): " +
                                   $"afterRotToTargetPos1DiffSqrMag > 1f. forward={forward} " +
                                   $"forwardLen={forwardLen}");
                    }
#endif
                    if (forwardLen > 1f) {
                        var fwd = afterRotToTargetPos1Diff;
                        maxSpeedAdd = Mathf.Max(curSpeed, 2f);
                        maxSpeedAddSqr = maxSpeedAdd * maxSpeedAdd;
#if DEBUG
                        if (logLogic) {
                            Log._Debug($"CustomTramBaseAI.SimulationStep({vehicleId}): " +
                                       $"forwardLen > 1f. fwd={fwd} maxSpeedAdd={maxSpeedAdd} maxSpeedAddSqr={maxSpeedAddSqr}");
                        }
#endif
                        if (afterRotToTargetPos1DiffSqrMag > maxSpeedAddSqr) {
                            var fwdLimiter = maxSpeedAdd / Mathf.Sqrt(afterRotToTargetPos1DiffSqrMag);
                            fwd.x *= fwdLimiter;
                            fwd.y *= fwdLimiter;

#if DEBUG
                            if (logLogic) {
                                Log._Debug($"CustomTramBaseAI.SimulationStep({vehicleId}): " +
                                           $"afterRotToTargetPos1DiffSqrMag > maxSpeedAddSqr. " +
                                           $"afterRotToTargetPos1DiffSqrMag={afterRotToTargetPos1DiffSqrMag} " +
                                           $"maxSpeedAddSqr={maxSpeedAddSqr} fwdLimiter={fwdLimiter} fwd={fwd}");
                            }
#endif
                        }

                        if (fwd.z < -1f) { // !!!
#if DEBUG
                            if (logLogic) {
                                Log._Debug($"CustomTramBaseAI.SimulationStep({vehicleId}): " +
                                           $"fwd.z < -1f. fwd={fwd}");
                            }
#endif

                            if (vehicleData.m_path != 0u && (leaderData.m_flags & Vehicle.Flags.WaitingPath) == 0) {
                                Vector3 targetPos0TargetPos1Diff = vehicleData.m_targetPos1 - vehicleData.m_targetPos0;
                                if ((curInvRot * targetPos0TargetPos1Diff).z < -0.01f) {
                                    // !!!
#if DEBUG
                                    if (logLogic) {
                                        Log._Debug($"CustomTramBaseAI.SimulationStep({vehicleId}): " +
                                                   $"(curInvRot * targetPos0TargetPos1Diff).z < -0.01f. " +
                                                   $"curInvRot={curInvRot} " +
                                                   $"targetPos0TargetPos1Diff={targetPos0TargetPos1Diff}");
                                    }
#endif
                                    if (afterRotToTargetPos1Diff.z < Mathf.Abs(afterRotToTargetPos1Diff.x) * -10f) {
                                        // !!!
#if DEBUG
                                        if (logLogic) {
                                            Log._Debug($"CustomTramBaseAI.SimulationStep({vehicleId}): " +
                                                       $"afterRotToTargetPos1Diff.z < Mathf.Abs" +
                                                       $"(afterRotToTargetPos1Diff.x) * -10f. fwd={fwd} " +
                                                       $"targetPos0TargetPos1Diff={targetPos0TargetPos1Diff} " +
                                                       $"afterRotToTargetPos1Diff={afterRotToTargetPos1Diff}");
                                        }
#endif

                                        // Fix: Trams get stuck
                                        /*fwd.z = 0f;
                                        afterRotToTargetPos1Diff = Vector3.zero;*/
                                        maxSpeed = 0.5f; // NON-STOCK CODE

#if DEBUG
                                        if (logLogic) {
                                            Log._Debug($"CustomTramBaseAI.SimulationStep({vehicleId}): (1) " +
                                                       $"set maxSpeed={maxSpeed}");
                                        }
#endif
                                    } else {
                                        posAfterWheelRot =
                                            posBeforeWheelRot +
                                            Vector3.Normalize(vehicleData.m_targetPos1 - vehicleData.m_targetPos0) *
                                            m_info.m_generatedInfo.m_wheelBase;
                                        posIndex = -1;

                                        UpdatePathTargetPositions(
                                            tramBaseAi,
                                            vehicleId,
                                            ref vehicleData,
                                            vehicleData.m_targetPos0,
                                            vehicleData.m_targetPos1,
                                            leaderId,
                                            ref leaderData,
                                            ref posIndex,
                                            0,
                                            0,
                                            Vector3.SqrMagnitude(
                                                vehicleData.m_targetPos1 - vehicleData.m_targetPos0) + 1f,
                                            1f);

#if DEBUG
                                        if (logLogic) {
                                            Log._Debug($"CustomTramBaseAI.SimulationStep({vehicleId}): " +
                                                       $"afterRotToTargetPos1Diff.z >= Mathf.Abs" +
                                                       $"(afterRotToTargetPos1Diff.x) * -10f. Invoked " +
                                                       $"UpdatePathTargetPositions. " +
                                                       $"posAfterWheelRot={posAfterWheelRot} " +
                                                       $"posBeforeWheelRot={posBeforeWheelRot} this.m_info" +
                                                       $".m_generatedInfo.m_wheelBase=" +
                                                       $"{m_info.m_generatedInfo.m_wheelBase}");
                                        }
#endif
                                    }
                                } else {
                                    posIndex = -1;
                                    UpdatePathTargetPositions(
                                        tramBaseAi,
                                        vehicleId,
                                        ref vehicleData,
                                        vehicleData.m_targetPos0,
                                        posBeforeWheelRot,
                                        leaderId,
                                        ref leaderData,
                                        ref posIndex,
                                        0,
                                        0,
                                        Vector3.SqrMagnitude(
                                            posBeforeWheelRot - (Vector3)vehicleData.m_targetPos0) + 1f,
                                        1f);

                                    vehicleData.m_targetPos1 = posAfterWheelRot;
                                    fwd.z = 0f;
                                    afterRotToTargetPos1Diff = Vector3.zero;
                                    maxSpeed = 0f;
#if DEBUG
                                    if (logLogic) {
                                        Log._Debug($"CustomTramBaseAI.SimulationStep({vehicleId}): " +
                                                   $"Vehicle is waiting for a path. posIndex={posIndex} " +
                                                   $"vehicleData.m_targetPos1={vehicleData.m_targetPos1} " +
                                                   $"fwd={fwd} afterRotToTargetPos1Diff={afterRotToTargetPos1Diff} " +
                                                   $"maxSpeed={maxSpeed}");
                                    }
#endif
                                }
                            }

                            motionFactor = 0f;
#if DEBUG
                            if (logLogic) {
                                Log._Debug($"CustomTramBaseAI.SimulationStep({vehicleId}): " +
                                           $"Reset motion factor. motionFactor={motionFactor}");
                            }
#endif
                        }

                        forward = VectorUtils.NormalizeXZ(fwd, out forwardLen);
                        var curve = Mathf.PI / 2f * (1f - forward.z);

                        if (forwardLen > 1f) {
                            curve /= forwardLen;
                        }

                        var targetDist = forwardLen;
#if DEBUG
                        if (logLogic) {
                            Log._Debug($"CustomTramBaseAI.SimulationStep({vehicleId}): " +
                                       $"targetDist={targetDist} fwd={fwd} curve={curve} maxSpeed={maxSpeed}");
                        }
#endif

                        if (vehicleData.m_targetPos1.w < 0.1f) {
                            maxSpeed = CalculateTargetSpeed(vehicleId, ref vehicleData, 1000f, curve);
                            maxSpeed = Mathf.Min(
                                maxSpeed,
                                CalculateMaxSpeed(targetDist, vehicleData.m_targetPos1.w,
                                                  braking * 0.9f));
                        } else {
                            maxSpeed = Mathf.Min(
                                maxSpeed,
                                CalculateTargetSpeed(vehicleId, ref vehicleData, 1000f, curve));
                        }
#if DEBUG
                        if (logLogic) {
                            Log._Debug($"CustomTramBaseAI.SimulationStep({vehicleId}): [1] maxSpeed={maxSpeed}");
                        }
#endif
                        maxSpeed = Mathf.Min(
                            maxSpeed,
                            CalculateMaxSpeed(targetDist, vehicleData.m_targetPos2.w,
                                              braking * 0.9f));
#if DEBUG
                        if (logLogic) {
                            Log._Debug($"CustomTramBaseAI.SimulationStep({vehicleId}): [2] maxSpeed={maxSpeed}");
                        }
#endif
                        targetDist += VectorUtils.LengthXZ(vehicleData.m_targetPos2 - vehicleData.m_targetPos1);
                        maxSpeed = Mathf.Min(
                            maxSpeed,
                            CalculateMaxSpeed(targetDist, vehicleData.m_targetPos3.w,
                                              braking * 0.9f));
#if DEBUG
                        if (logLogic) {
                            Log._Debug($"CustomTramBaseAI.SimulationStep({vehicleId}): [3] maxSpeed={maxSpeed}");
                        }
#endif
                        targetDist += VectorUtils.LengthXZ(vehicleData.m_targetPos3 - vehicleData.m_targetPos2);
                        if (vehicleData.m_targetPos3.w < 0.01f) {
                            var lengthExtra = m_info.m_generatedInfo.m_wheelBase - m_info.m_generatedInfo.m_size.z;
                            targetDist = Mathf.Max(0f, targetDist + (lengthExtra * 0.5f));
                        }

                        maxSpeed = Mathf.Min(maxSpeed, CalculateMaxSpeed(targetDist, 0f,
                                                                         braking * 0.9f));
#if DEBUG
                        if (logLogic) {
                            Log._Debug($"CustomTramBaseAI.SimulationStep({vehicleId}): [4] maxSpeed={maxSpeed}");
                        }
#endif
                        CarAI.CheckOtherVehicles(
                            vehicleId,
                            ref vehicleData,
                            ref frameData,
                            ref maxSpeed,
                            ref blocked,
                            ref zero,
                            estimatedFrameDist,
                            braking * 0.9f,
                            lodPhysics);
#if DEBUG
                        if (logLogic) {
                            Log._Debug($"CustomTramBaseAI.SimulationStep({vehicleId}): " +
                                       $"CheckOtherVehicles finished. blocked={blocked}");
                        }
#endif
                        if (maxSpeed < curSpeed) {
                            var brake = Mathf.Max(acceleration, Mathf.Min(braking, curSpeed));
                            targetSpeed = Mathf.Max(maxSpeed, curSpeed - brake);
#if DEBUG
                            if (logLogic) {
                                Log._Debug($"CustomTramBaseAI.SimulationStep({vehicleId}): " +
                                           $"maxSpeed < curSpeed. maxSpeed={maxSpeed} curSpeed={curSpeed} " +
                                           $"brake={brake} targetSpeed={targetSpeed}");
                            }
#endif
                        } else {
                            var accel = Mathf.Max(acceleration, Mathf.Min(braking, -curSpeed));
                            targetSpeed = Mathf.Min(maxSpeed, curSpeed + accel);
#if DEBUG
                            if (logLogic) {
                                Log._Debug($"CustomTramBaseAI.SimulationStep({vehicleId}): " +
                                           $"maxSpeed >= curSpeed. maxSpeed={maxSpeed} curSpeed={curSpeed} " +
                                           $"accel={accel} targetSpeed={targetSpeed}");
                            }
#endif
                        }
                    }
                } else if (curSpeed < 0.1f
                           && hasValidPathTargetPos
                           && leaderInfo.m_vehicleAI.ArriveAtDestination(leaderId, ref leaderData))
                {
                    leaderData.Unspawn(leaderId);
                    return;
                }

                if ((leaderData.m_flags & Vehicle.Flags.Stopped) == 0 && maxSpeed < 0.1f) {
#if DEBUG
                    if (logLogic) {
                        Log._Debug($"CustomTramBaseAI.SimulationStep({vehicleId}): " +
                                   $"Vehicle is not stopped but maxSpeed < 0.1. maxSpeed={maxSpeed}");
                    }
#endif
                    blocked = true;
                }

                if (blocked) {
                    leaderData.m_blockCounter = (byte)Mathf.Min(leaderData.m_blockCounter + 1, 255);
                } else {
                    leaderData.m_blockCounter = 0;
                }

                if (forwardLen > 1f) {
                    turnAngle = Mathf.Asin(forward.x) * Mathf.Sign(targetSpeed);
                    targetMotion = forward * targetSpeed;
                } else {
                    var vel = Vector3.ClampMagnitude(afterRotToTargetPos1Diff * 0.5f - curveTangent, braking);
                    targetMotion = curveTangent + vel;
                }
            }

            var mayBlink = (currentFrameIndex + leaderId & 16u) != 0u;
            var springs = targetMotion - curveTangent;
            var targetAfterWheelRotMotion = frameData.m_rotation * targetMotion;
            var targetBeforeWheelRotMotion = Vector3.Normalize((Vector3)vehicleData.m_targetPos0 - posBeforeWheelRot) *
                                             (targetMotion.magnitude * motionFactor);
            targetBeforeWheelRotMotion -= targetAfterWheelRotMotion *
                                          (Vector3.Dot(targetAfterWheelRotMotion, targetBeforeWheelRotMotion) /
                                           Mathf.Max(1f, targetAfterWheelRotMotion.sqrMagnitude));
            posAfterWheelRot += targetAfterWheelRotMotion;
            posBeforeWheelRot += targetBeforeWheelRotMotion;
            frameData.m_rotation = Quaternion.LookRotation(posAfterWheelRot - posBeforeWheelRot);
            var targetPos = posAfterWheelRot - (frameData.m_rotation * new Vector3(
                                                    0f,
                                                    0f,
                                                    m_info.m_generatedInfo.m_wheelBase * 0.5f));
            frameData.m_velocity = targetPos - frameData.m_position;

            if (leadingVehicle != 0) {
                frameData.m_position += frameData.m_velocity * 0.6f;
            } else {
                frameData.m_position += frameData.m_velocity * 0.5f;
            }

            frameData.m_swayVelocity = (frameData.m_swayVelocity * (1f - m_info.m_dampers)) -
                                        (springs * (1f - m_info.m_springs)) -
                                        (frameData.m_swayPosition * m_info.m_springs);
            frameData.m_swayPosition += frameData.m_swayVelocity * 0.5f;
            frameData.m_steerAngle = 0f;
            frameData.m_travelDistance += targetMotion.z;
            if (leadingVehicle != 0) {
                frameData.m_lightIntensity = Singleton<VehicleManager>
                                             .instance.m_vehicles.m_buffer[leaderId].GetLastFrameData()
                                             .m_lightIntensity;
            } else {
                frameData.m_lightIntensity.x = 5f;
                frameData.m_lightIntensity.y = springs.z >= -0.1f ? 0.5f : 5f;
                frameData.m_lightIntensity.z = turnAngle >= -0.1f || !mayBlink ? 0f : 5f;
                frameData.m_lightIntensity.w = turnAngle <= 0.1f || !mayBlink ? 0f : 5f;
            }

            frameData.m_underground = (vehicleData.m_flags & Vehicle.Flags.Underground) != 0;
            frameData.m_transition = (vehicleData.m_flags & Vehicle.Flags.Transition) != 0;

            // base.SimulationStep(vehicleID, ref vehicleData, ref frameData, leaderID, ref leaderData, lodPhysics);
        }

        [RedirectReverse]
        [MethodImpl(MethodImplOptions.NoInlining)]
        [UsedImplicitly]
        public static void UpdatePathTargetPositions(TramBaseAI tramBaseAi,
                                                     ushort vehicleId,
                                                     ref Vehicle vehicleData,
                                                     Vector3 refPos1,
                                                     Vector3 refPos2,
                                                     ushort leaderId,
                                                     ref Vehicle leaderData,
                                                     ref int index,
                                                     int max1,
                                                     int max2,
                                                     float minSqrDistanceA,
                                                     float minSqrDistanceB) {
            Log.Error($"CustomTramBaseAI.InvokeUpdatePathTargetPositions called! tramBaseAI={tramBaseAi}");
        }

        [RedirectReverse]
        [MethodImpl(MethodImplOptions.NoInlining)]
        [UsedImplicitly]
        private static float GetMaxSpeed(ushort leaderId, ref Vehicle leaderData) {
            Log.Error("CustomTrainAI.GetMaxSpeed called");
            return 0f;
        }

        [RedirectReverse]
        [MethodImpl(MethodImplOptions.NoInlining)]
        [UsedImplicitly]
        private static float CalculateMaxSpeed(float targetDist, float targetSpeed, float maxBraking) {
            Log.Error("CustomTrainAI.CalculateMaxSpeed called");
            return 0f;
        }

        [RedirectReverse]
        [MethodImpl(MethodImplOptions.NoInlining)]
        [UsedImplicitly]
        private static void InitializePath(ushort vehicleId, ref Vehicle vehicleData) {
            Log.Error("CustomTrainAI.InitializePath called");
        }
    }
}