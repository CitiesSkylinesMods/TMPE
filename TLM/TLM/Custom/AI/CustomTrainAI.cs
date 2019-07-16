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
    [TargetType(typeof(TrainAI))]
    public class CustomTrainAI : TrainAI {
        [RedirectMethod]
        public void CustomSimulationStep(ushort vehicleId, ref Vehicle vehicleData, Vector3 physicsLodRefPos) {
            var extVehicleMan = Constants.ManagerFactory.ExtVehicleManager;

            if ((vehicleData.m_flags & Vehicle.Flags.WaitingPath) != 0) {
                var pathFindFlags = Singleton<PathManager>
                                    .instance.m_pathUnits.m_buffer[vehicleData.m_path].m_pathFindFlags;

                if ((pathFindFlags & PathUnit.FLAG_READY) != 0) {
                    try {
                        PathFindReady(vehicleId, ref vehicleData);
                    } catch (Exception e) {
                        Log.Warning($"TrainAI.PathFindReady({vehicleId}) for vehicle " +
                                    $"{vehicleData.Info?.m_class?.name} threw an exception: {e}");
                        vehicleData.m_flags &= ~Vehicle.Flags.WaitingPath;
                        Singleton<PathManager>.instance.ReleasePath(vehicleData.m_path);
                        vehicleData.m_path = 0u;
                        vehicleData.Unspawn(vehicleId);
                        return;
                    }
                } else if ((pathFindFlags & PathUnit.FLAG_FAILED) != 0 || vehicleData.m_path == 0) {
                    vehicleData.m_flags &= ~Vehicle.Flags.WaitingPath;
                    Singleton<PathManager>.instance.ReleasePath(vehicleData.m_path);
                    vehicleData.m_path = 0u;
                    vehicleData.Unspawn(vehicleId);
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
            var reversed = (vehicleData.m_flags & Vehicle.Flags.Reversed) != 0;
            ushort connectedVehicleId;
            connectedVehicleId = reversed ? vehicleData.GetLastVehicle(vehicleId) : vehicleId;

            var instance = Singleton<VehicleManager>.instance;
            var info = instance.m_vehicles.m_buffer[connectedVehicleId].Info;
            info.m_vehicleAI.SimulationStep(
                connectedVehicleId,
                ref instance.m_vehicles.m_buffer[connectedVehicleId],
                vehicleId,
                ref vehicleData,
                0);

            if ((vehicleData.m_flags & (Vehicle.Flags.Created
                                        | Vehicle.Flags.Deleted)) != Vehicle.Flags.Created) {
                return;
            }

            var newReversed = (vehicleData.m_flags & Vehicle.Flags.Reversed) != 0;

            if (newReversed != reversed) {
                reversed = newReversed;
                connectedVehicleId = reversed ? vehicleData.GetLastVehicle(vehicleId) : vehicleId;
                info = instance.m_vehicles.m_buffer[connectedVehicleId].Info;
                info.m_vehicleAI.SimulationStep(
                    connectedVehicleId,
                    ref instance.m_vehicles.m_buffer[connectedVehicleId],
                    vehicleId,
                    ref vehicleData,
                    0);

                if ((vehicleData.m_flags & (Vehicle.Flags.Created
                                            | Vehicle.Flags.Deleted)) != Vehicle.Flags.Created) {
                    return;
                }

                newReversed = (vehicleData.m_flags & Vehicle.Flags.Reversed) != 0;
                if (newReversed != reversed) {
                    Singleton<VehicleManager>.instance.ReleaseVehicle(vehicleId);
                    return;
                }
            }

            if (reversed) {
                connectedVehicleId = instance.m_vehicles.m_buffer[connectedVehicleId].m_leadingVehicle;
                var num2 = 0;
                while (connectedVehicleId != 0) {
                    info = instance.m_vehicles.m_buffer[connectedVehicleId].Info;
                    info.m_vehicleAI.SimulationStep(
                        connectedVehicleId,
                        ref instance.m_vehicles.m_buffer[connectedVehicleId],
                        vehicleId,
                        ref vehicleData,
                        0);

                    if ((vehicleData.m_flags & (Vehicle.Flags.Created | Vehicle.Flags.Deleted)) !=
                        Vehicle.Flags.Created) {
                        return;
                    }

                    connectedVehicleId = instance.m_vehicles.m_buffer[connectedVehicleId].m_leadingVehicle;

                    if (++num2 > 16384) {
                        CODebugBase<LogChannel>.Error(
                            LogChannel.Core,
                            $"Invalid list detected!\n{Environment.StackTrace}");
                        break;
                    }
                }
            } else {
                connectedVehicleId = instance.m_vehicles.m_buffer[connectedVehicleId].m_trailingVehicle;
                var num3 = 0;
                while (connectedVehicleId != 0) {
                    info = instance.m_vehicles.m_buffer[connectedVehicleId].Info;
                    info.m_vehicleAI.SimulationStep(
                        connectedVehicleId,
                        ref instance.m_vehicles.m_buffer[connectedVehicleId],
                        vehicleId,
                        ref vehicleData,
                        0);
                    if ((vehicleData.m_flags & (Vehicle.Flags.Created
                                                | Vehicle.Flags.Deleted)) != Vehicle.Flags.Created) {
                        return;
                    }

                    connectedVehicleId = instance.m_vehicles.m_buffer[connectedVehicleId].m_trailingVehicle;

                    if (++num3 > 16384) {
                        CODebugBase<LogChannel>.Error(LogChannel.Core,
                                                      $"Invalid list detected!\n{Environment.StackTrace}");
                        break;
                    }
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
        public void CustomSimulationStep(ushort vehicleId,
                                         ref Vehicle vehicleData,
                                         ref Vehicle.Frame frameData,
                                         ushort leaderId,
                                         ref Vehicle leaderData,
                                         int lodPhysics) {
            var reversed = (leaderData.m_flags & Vehicle.Flags.Reversed) != 0;
            var frontVehicleId = (!reversed) ? vehicleData.m_leadingVehicle : vehicleData.m_trailingVehicle;
            VehicleInfo vehicleInfo;

            if (leaderId != vehicleId) {
                vehicleInfo = leaderData.Info;
            } else {
                vehicleInfo = m_info;
            }

            var trainAi = vehicleInfo.m_vehicleAI as TrainAI;

            if (frontVehicleId != 0) {
                frameData.m_position += frameData.m_velocity * 0.4f;
            } else {
                frameData.m_position += frameData.m_velocity * 0.5f;
            }

            frameData.m_swayPosition += frameData.m_swayVelocity * 0.5f;

            var posBeforeWheelRot = frameData.m_position;
            var posAfterWheelRot = frameData.m_position;
            var wheelBaseRot = frameData.m_rotation
                               * new Vector3(0f, 0f, m_info.m_generatedInfo.m_wheelBase * 0.5f);

            if (reversed) {
                posBeforeWheelRot -= wheelBaseRot;
                posAfterWheelRot += wheelBaseRot;
            } else {
                posBeforeWheelRot += wheelBaseRot;
                posAfterWheelRot -= wheelBaseRot;
            }

            var acceleration = m_info.m_acceleration;
            var braking = m_info.m_braking;
            var curSpeed = frameData.m_velocity.magnitude;

            var beforeRotToTargetPos1Diff = (Vector3)vehicleData.m_targetPos1 - posBeforeWheelRot;
            var beforeRotToTargetPos1DiffSqrMag = beforeRotToTargetPos1Diff.sqrMagnitude;

            var curInvRot = Quaternion.Inverse(frameData.m_rotation);
            var curveTangent = curInvRot * frameData.m_velocity;

            var forward = Vector3.forward;
            var targetMotion = Vector3.zero;
            var targetSpeed = 0f;
            var motionFactor = 0.5f;

            if (frontVehicleId != 0) {
                var vehMan = Singleton<VehicleManager>.instance;
                var frontVehLastFrameData = vehMan.m_vehicles.m_buffer[frontVehicleId].GetLastFrameData();
                var frontVehInfo = vehMan.m_vehicles.m_buffer[frontVehicleId].Info;
                float attachOffset;

                if ((vehicleData.m_flags & Vehicle.Flags.Inverted) != 0 != reversed) {
                    attachOffset = m_info.m_attachOffsetBack - (m_info.m_generatedInfo.m_size.z * 0.5f);
                } else {
                    attachOffset = m_info.m_attachOffsetFront - (m_info.m_generatedInfo.m_size.z * 0.5f);
                }

                float frontAttachOffset;

                if ((vehMan.m_vehicles.m_buffer[frontVehicleId].m_flags & Vehicle.Flags.Inverted) != 0 != reversed) {
                    frontAttachOffset = frontVehInfo.m_attachOffsetFront -
                                        (frontVehInfo.m_generatedInfo.m_size.z * 0.5f);
                } else {
                    frontAttachOffset =
                        (frontVehInfo.m_attachOffsetBack - (frontVehInfo.m_generatedInfo.m_size.z * 0.5f));
                }

                var posMinusAttachOffset = frameData.m_position;
                if (reversed) {
                    posMinusAttachOffset += frameData.m_rotation * new Vector3(0f, 0f, attachOffset);
                } else {
                    posMinusAttachOffset -= frameData.m_rotation * new Vector3(0f, 0f, attachOffset);
                }

                var frontPosPlusAttachOffset = frontVehLastFrameData.m_position;
                if (reversed) {
                    frontPosPlusAttachOffset -= frontVehLastFrameData.m_rotation
                                                * new Vector3(0f, 0f, frontAttachOffset);
                } else {
                    frontPosPlusAttachOffset += frontVehLastFrameData.m_rotation
                                                * new Vector3(0f, 0f, frontAttachOffset);
                }

                var frontPosMinusWheelBaseRot = frontVehLastFrameData.m_position;
                wheelBaseRot = frontVehLastFrameData.m_rotation * new Vector3(
                                   0f,
                                   0f,
                                   frontVehInfo.m_generatedInfo.m_wheelBase * 0.5f);
                if (reversed) {
                    frontPosMinusWheelBaseRot += wheelBaseRot;
                } else {
                    frontPosMinusWheelBaseRot -= wheelBaseRot;
                }

                if (Vector3.Dot(
                        vehicleData.m_targetPos1 - vehicleData.m_targetPos0,
                        (Vector3)vehicleData.m_targetPos0 - posAfterWheelRot) < 0f
                    && vehicleData.m_path != 0u
                    && (leaderData.m_flags & Vehicle.Flags.WaitingPath) == 0)
                {
                    var someIndex = -1;
                    UpdatePathTargetPositions(
                        trainAi,
                        vehicleId,
                        ref vehicleData,
                        vehicleData.m_targetPos0,
                        posAfterWheelRot,
                        0,
                        ref leaderData,
                        ref someIndex,
                        0,
                        0,
                        Vector3.SqrMagnitude(posAfterWheelRot - (Vector3)vehicleData.m_targetPos0) + 1f,
                        1f);
                    beforeRotToTargetPos1DiffSqrMag = 0f;
                }

                var maxAttachDist = Mathf.Max(Vector3.Distance(posMinusAttachOffset,
                                                               frontPosPlusAttachOffset),
                                              2f);
                const float ONE = 1f;
                var maxAttachSqrDist = maxAttachDist * maxAttachDist;
                const float ONE_SQR = ONE * ONE;
                var i = 0;
                if (beforeRotToTargetPos1DiffSqrMag < maxAttachSqrDist) {
                    if (vehicleData.m_path != 0u
                        && (leaderData.m_flags & Vehicle.Flags.WaitingPath) == 0) {
                        UpdatePathTargetPositions(
                            trainAi,
                            vehicleId,
                            ref vehicleData,
                            posAfterWheelRot,
                            posBeforeWheelRot,
                            0,
                            ref leaderData,
                            ref i,
                            1,
                            2,
                            maxAttachSqrDist,
                            ONE_SQR);
                    }

                    while (i < 4) {
                        vehicleData.SetTargetPos(i, vehicleData.GetTargetPos(i - 1));
                        i++;
                    }

                    beforeRotToTargetPos1Diff = (Vector3)vehicleData.m_targetPos1 - posBeforeWheelRot;
                    beforeRotToTargetPos1DiffSqrMag = beforeRotToTargetPos1Diff.sqrMagnitude;
                }

                if (vehicleData.m_path != 0u) {
                    var netMan = Singleton<NetManager>.instance;
                    var pathPosIndex = vehicleData.m_pathPositionIndex;
                    var lastPathOffset = vehicleData.m_lastPathOffset;
                    if (pathPosIndex == 255) {
                        pathPosIndex = 0;
                    }

                    var pathMan = Singleton<PathManager>.instance;
                    if (pathMan.m_pathUnits.m_buffer[vehicleData.m_path]
                               .GetPosition(pathPosIndex >> 1, out var curPathPos)) {
                        netMan.m_segments.m_buffer[curPathPos.m_segment].AddTraffic(
                            Mathf.RoundToInt(m_info.m_generatedInfo.m_size.z * 3f),
                            GetNoiseLevel());

                        if ((pathPosIndex & 1) == 0 || lastPathOffset == 0 ||
                            (leaderData.m_flags & Vehicle.Flags.WaitingPath) != 0) {
                            var laneId = PathManager.GetLaneID(curPathPos);
                            if (laneId != 0u) {
                                netMan.m_lanes.m_buffer[laneId].ReserveSpace(m_info.m_generatedInfo.m_size.z);
                            }
                        } else if (pathMan.m_pathUnits.m_buffer[vehicleData.m_path]
                                          .GetNextPosition(pathPosIndex >> 1, out var nextPathPos)) {
                            // NON-STOCK CODE START
                            ushort transitNodeId;

                            if (curPathPos.m_offset < 128) {
                                transitNodeId = netMan.m_segments.m_buffer[curPathPos.m_segment].m_startNode;
                            } else {
                                transitNodeId = netMan.m_segments.m_buffer[curPathPos.m_segment].m_endNode;
                            }

                            if (VehicleBehaviorManager.Instance.IsSpaceReservationAllowed(
                                transitNodeId,
                                curPathPos,
                                nextPathPos)) {
                                // NON-STOCK CODE END
                                var nextLaneId = PathManager.GetLaneID(nextPathPos);
                                if (nextLaneId != 0u) {
                                    netMan.m_lanes.m_buffer[nextLaneId].ReserveSpace(m_info.m_generatedInfo.m_size.z);
                                }
                            } // NON-STOCK CODE
                        }
                    }
                }

                beforeRotToTargetPos1Diff = curInvRot * beforeRotToTargetPos1Diff;
                var negTotalAttachLen = -(((m_info.m_generatedInfo.m_wheelBase + frontVehInfo.m_generatedInfo.m_wheelBase) * 0.5f) + attachOffset + frontAttachOffset);
                var hasPath = false;

                if (vehicleData.m_path != 0u && (leaderData.m_flags & Vehicle.Flags.WaitingPath) == 0) {
                    if (Line3.Intersect(
                        posBeforeWheelRot,
                        vehicleData.m_targetPos1,
                        frontPosMinusWheelBaseRot,
                        negTotalAttachLen,
                        out var u1,
                        out var u2)) {
                        targetMotion = beforeRotToTargetPos1Diff
                                       * Mathf.Clamp(Mathf.Min(u1, u2) / 0.6f, 0f, 2f);
                    } else {
                        Line3.DistanceSqr(
                            posBeforeWheelRot,
                            vehicleData.m_targetPos1,
                            frontPosMinusWheelBaseRot,
                            out u1);
                        targetMotion = beforeRotToTargetPos1Diff * Mathf.Clamp(u1 / 0.6f, 0f, 2f);
                    }

                    hasPath = true;
                }

                if (hasPath) {
                    if (Vector3.Dot(
                            frontPosMinusWheelBaseRot - posBeforeWheelRot,
                            posBeforeWheelRot - posAfterWheelRot) < 0f) {
                        motionFactor = 0f;
                    }
                } else {
                    var frontPosBeforeToAfterWheelRotDist = Vector3.Distance(
                        frontPosMinusWheelBaseRot,
                        posBeforeWheelRot);
                    motionFactor = 0f;
                    targetMotion = curInvRot
                                   * ((frontPosMinusWheelBaseRot - posBeforeWheelRot)
                                      * (Mathf.Max(0f, frontPosBeforeToAfterWheelRotDist - negTotalAttachLen)
                                         / Mathf.Max(1f, frontPosBeforeToAfterWheelRotDist * 0.6f)));
                }
            } else {
                var estimatedFrameDist = (curSpeed + acceleration)
                                         * (0.5f + (0.5f * (curSpeed + acceleration) / braking));
                var maxSpeedAdd = Mathf.Max(curSpeed + acceleration, 2f);
                var meanSpeedAdd = Mathf.Max((estimatedFrameDist - maxSpeedAdd) / 2f, 1f);
                var maxSpeedAddSqr = maxSpeedAdd * maxSpeedAdd;
                var meanSpeedAddSqr = meanSpeedAdd * meanSpeedAdd;

                if (Vector3.Dot(
                        vehicleData.m_targetPos1 - vehicleData.m_targetPos0,
                        (Vector3)vehicleData.m_targetPos0 - posAfterWheelRot) < 0f
                    && vehicleData.m_path != 0u
                    && (leaderData.m_flags & (Vehicle.Flags.WaitingPath | Vehicle.Flags.Stopped)) == 0)
                {
                    var someIndex = -1;
                    UpdatePathTargetPositions(
                        trainAi,
                        vehicleId,
                        ref vehicleData,
                        vehicleData.m_targetPos0,
                        posAfterWheelRot,
                        leaderId,
                        ref leaderData,
                        ref someIndex,
                        0,
                        0,
                        Vector3.SqrMagnitude(posAfterWheelRot - (Vector3)vehicleData.m_targetPos0) + 1f,
                        1f);
                    beforeRotToTargetPos1DiffSqrMag = 0f;
                }

                var posIndex = 0;
                var flag3 = false;
                if ((beforeRotToTargetPos1DiffSqrMag < maxSpeedAddSqr
                     || vehicleData.m_targetPos3.w < 0.01f)
                    && (leaderData.m_flags & (Vehicle.Flags.WaitingPath | Vehicle.Flags.Stopped)) == 0)
                {
                    if (vehicleData.m_path != 0u) {
                        UpdatePathTargetPositions(
                            trainAi,
                            vehicleId,
                            ref vehicleData,
                            posAfterWheelRot,
                            posBeforeWheelRot,
                            leaderId,
                            ref leaderData,
                            ref posIndex,
                            1,
                            4,
                            maxSpeedAddSqr,
                            meanSpeedAddSqr);
                    }
                    if (posIndex < 4) {
                        flag3 = true;
                        while (posIndex < 4) {
                            vehicleData.SetTargetPos(posIndex, vehicleData.GetTargetPos(posIndex - 1));
                            posIndex++;
                        }
                    }

                    beforeRotToTargetPos1Diff = (Vector3)vehicleData.m_targetPos1 - posBeforeWheelRot;
                    beforeRotToTargetPos1DiffSqrMag = beforeRotToTargetPos1Diff.sqrMagnitude;
                }

                if ((leaderData.m_flags & (Vehicle.Flags.WaitingPath
                                           | Vehicle.Flags.Stopped)) == 0
                    && m_info.m_vehicleType != VehicleInfo.VehicleType.Monorail) {
                    CustomForceTrafficLights(vehicleId, ref vehicleData, curSpeed > 0.1f);
                    // NON-STOCK CODE
                }

                if (vehicleData.m_path != 0u) {
                    var netMan = Singleton<NetManager>.instance;
                    var pathPosIndex = vehicleData.m_pathPositionIndex;
                    var lastPathOffset = vehicleData.m_lastPathOffset;
                    if (pathPosIndex == 255) {
                        pathPosIndex = 0;
                    }

                    var pathMan = Singleton<PathManager>.instance;
                    if (pathMan.m_pathUnits.m_buffer[vehicleData.m_path]
                               .GetPosition(pathPosIndex >> 1, out var curPathPos))
                    {
                        netMan.m_segments.m_buffer[curPathPos.m_segment]
                              .AddTraffic(Mathf.RoundToInt(m_info.m_generatedInfo.m_size.z * 3f),
                                          GetNoiseLevel());

                        if ((pathPosIndex & 1) == 0
                            || lastPathOffset == 0
                            || (leaderData.m_flags & Vehicle.Flags.WaitingPath) != 0)
                        {
                            var laneId = PathManager.GetLaneID(curPathPos);
                            if (laneId != 0u) {
                                netMan.m_lanes.m_buffer[laneId].ReserveSpace(m_info.m_generatedInfo.m_size.z, vehicleId);
                            }
                        } else if (pathMan.m_pathUnits.m_buffer[vehicleData.m_path]
                                          .GetNextPosition(pathPosIndex >> 1, out var nextPathPos)) {
                            // NON-STOCK CODE START
                            ushort transitNodeId;
                            transitNodeId = curPathPos.m_offset < 128
                                                ? netMan.m_segments.m_buffer[curPathPos.m_segment].m_startNode
                                                : netMan.m_segments.m_buffer[curPathPos.m_segment].m_endNode;

                            if (VehicleBehaviorManager.Instance.IsSpaceReservationAllowed(
                                transitNodeId,
                                curPathPos,
                                nextPathPos)) {
                                // NON-STOCK CODE END
                                var nextLaneId = PathManager.GetLaneID(nextPathPos);
                                if (nextLaneId != 0u) {
                                    netMan.m_lanes.m_buffer[nextLaneId].ReserveSpace(
                                        m_info.m_generatedInfo.m_size.z, vehicleId);
                                }
                            } // NON-STOCK CODE
                        }
                    }
                }

                float maxSpeed;

                maxSpeed = (leaderData.m_flags & Vehicle.Flags.Stopped) != 0
                               ? 0f
                               : Mathf.Min(vehicleData.m_targetPos1.w, GetMaxSpeed(leaderId, ref leaderData));

                beforeRotToTargetPos1Diff = curInvRot * beforeRotToTargetPos1Diff;
                if (reversed) {
                    beforeRotToTargetPos1Diff = -beforeRotToTargetPos1Diff;
                }

                var blocked = false;
                var forwardLen = 0f;

                if (beforeRotToTargetPos1DiffSqrMag > 1f) {
                    forward = VectorUtils.NormalizeXZ(beforeRotToTargetPos1Diff, out forwardLen);

                    if (forwardLen > 1f) {
                        var fwd = beforeRotToTargetPos1Diff;
                        maxSpeedAdd = Mathf.Max(curSpeed, 2f);
                        maxSpeedAddSqr = maxSpeedAdd * maxSpeedAdd;
                        if (beforeRotToTargetPos1DiffSqrMag > maxSpeedAddSqr) {
                            var num20 = maxSpeedAdd / Mathf.Sqrt(beforeRotToTargetPos1DiffSqrMag);
                            fwd.x *= num20;
                            fwd.y *= num20;
                        }

                        if (fwd.z < -1f) {
                            if (vehicleData.m_path != 0u && (leaderData.m_flags & Vehicle.Flags.WaitingPath) == 0) {
                                Vector3 targetPos0TargetPos1Diff = vehicleData.m_targetPos1 - vehicleData.m_targetPos0;
                                targetPos0TargetPos1Diff = curInvRot * targetPos0TargetPos1Diff;
                                if (reversed) {
                                    targetPos0TargetPos1Diff = -targetPos0TargetPos1Diff;
                                }

                                if (targetPos0TargetPos1Diff.z < -0.01f) {
                                    if (beforeRotToTargetPos1Diff.z < Mathf.Abs(beforeRotToTargetPos1Diff.x) * -10f) {
                                        if (curSpeed < 0.01f) {
                                            Reverse(leaderId, ref leaderData);
                                            return;
                                        }

                                        fwd.z = 0f;
                                        beforeRotToTargetPos1Diff = Vector3.zero;
                                        maxSpeed = 0f;
                                    } else {
                                        posBeforeWheelRot =
                                            posAfterWheelRot +
                                            Vector3.Normalize(vehicleData.m_targetPos1 - vehicleData.m_targetPos0) *
                                            m_info.m_generatedInfo.m_wheelBase;
                                        posIndex = -1;
                                        UpdatePathTargetPositions(
                                            trainAi,
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
                                    }
                                } else {
                                    posIndex = -1;
                                    UpdatePathTargetPositions(
                                        trainAi,
                                        vehicleId,
                                        ref vehicleData,
                                        vehicleData.m_targetPos0,
                                        posAfterWheelRot,
                                        leaderId,
                                        ref leaderData,
                                        ref posIndex,
                                        0,
                                        0,
                                        Vector3.SqrMagnitude(
                                            posAfterWheelRot - (Vector3)vehicleData.m_targetPos0) + 1f,
                                        1f);
                                    vehicleData.m_targetPos1 = posBeforeWheelRot;
                                    fwd.z = 0f;
                                    beforeRotToTargetPos1Diff = Vector3.zero;
                                    maxSpeed = 0f;
                                }
                            }

                            motionFactor = 0f;
                        }

                        forward = VectorUtils.NormalizeXZ(fwd, out forwardLen);
                        var curve = Mathf.PI / 2f * (1f - forward.z);
                        if (forwardLen > 1f) {
                            curve /= forwardLen;
                        }

                        maxSpeed = Mathf.Min(maxSpeed,
                                             CalculateTargetSpeed(vehicleId,
                                                                  ref vehicleData,
                                                                  1000f, curve));
                        var targetDist = forwardLen;
                        maxSpeed = Mathf.Min(maxSpeed,
                                             CalculateMaxSpeed(targetDist,
                                                               vehicleData.m_targetPos2.w, braking));
                        targetDist += VectorUtils.LengthXZ(vehicleData.m_targetPos2 - vehicleData.m_targetPos1);
                        maxSpeed = Mathf.Min(maxSpeed,
                                             CalculateMaxSpeed(targetDist,
                                                               vehicleData.m_targetPos3.w,
                                                               braking));
                        targetDist += VectorUtils.LengthXZ(vehicleData.m_targetPos3 - vehicleData.m_targetPos2);
                        maxSpeed = Mathf.Min(maxSpeed,
                                             CalculateMaxSpeed(targetDist, 0f, braking));

                        if (maxSpeed < curSpeed) {
                            var brake = Mathf.Max(acceleration, Mathf.Min(braking, curSpeed));
                            targetSpeed = Mathf.Max(maxSpeed, curSpeed - brake);
                        } else {
                            var accel = Mathf.Max(acceleration, Mathf.Min(braking, -curSpeed));
                            targetSpeed = Mathf.Min(maxSpeed, curSpeed + accel);
                        }
                    }
                } else if (curSpeed < 0.1f && flag3 &&
                           vehicleInfo.m_vehicleAI.ArriveAtDestination(leaderId, ref leaderData)) {
                    leaderData.Unspawn(leaderId);
                    return;
                }

                if ((leaderData.m_flags & Vehicle.Flags.Stopped) == 0 && maxSpeed < 0.1f) {
                    blocked = true;
                }

                if (blocked) {
                    leaderData.m_blockCounter = (byte)Mathf.Min(leaderData.m_blockCounter + 1, 255);
                } else {
                    leaderData.m_blockCounter = 0;
                }

                if (forwardLen > 1f) {
                    if (reversed) {
                        forward = -forward;
                    }

                    targetMotion = forward * targetSpeed;
                } else {
                    if (reversed) {
                        beforeRotToTargetPos1Diff = -beforeRotToTargetPos1Diff;
                    }

                    var vel = Vector3.ClampMagnitude(beforeRotToTargetPos1Diff * 0.5f - curveTangent, braking);
                    targetMotion = curveTangent + vel;
                }
            }

            var springs = targetMotion - curveTangent;
            var targetAfterWheelRotMotion = frameData.m_rotation * targetMotion;
            var posAfterWheelRotToTargetDiff = Vector3.Normalize((Vector3)vehicleData.m_targetPos0 - posAfterWheelRot)
                                               * (targetMotion.magnitude * motionFactor);
            posBeforeWheelRot += targetAfterWheelRotMotion;
            posAfterWheelRot += posAfterWheelRotToTargetDiff;

            Vector3 targetPos;
            if (reversed) {
                frameData.m_rotation = Quaternion.LookRotation(posAfterWheelRot - posBeforeWheelRot);
                targetPos = posBeforeWheelRot + (frameData.m_rotation * new Vector3(0f, 0f, m_info.m_generatedInfo.m_wheelBase * 0.5f));
            } else {
                frameData.m_rotation = Quaternion.LookRotation(posBeforeWheelRot - posAfterWheelRot);
                targetPos = (posBeforeWheelRot - frameData.m_rotation * new Vector3(0f, 0f, m_info.m_generatedInfo.m_wheelBase * 0.5f));
            }

            frameData.m_velocity = targetPos - frameData.m_position;

            if (frontVehicleId != 0) {
                frameData.m_position += frameData.m_velocity * 0.6f;
            } else {
                frameData.m_position += frameData.m_velocity * 0.5f;
            }

            frameData.m_swayVelocity =
                ((frameData.m_swayVelocity * (1f - m_info.m_dampers)) - (springs * (1f - m_info.m_springs)))
                - (frameData.m_swayPosition * m_info.m_springs);
            frameData.m_swayPosition += frameData.m_swayVelocity * 0.5f;
            frameData.m_steerAngle = 0f;
            frameData.m_travelDistance += targetMotion.z;
            frameData.m_lightIntensity.x = (!reversed) ? 5f : 0f;
            frameData.m_lightIntensity.y = (!reversed) ? 0f : 5f;
            frameData.m_lightIntensity.z = 0f;
            frameData.m_lightIntensity.w = 0f;
            frameData.m_underground = (vehicleData.m_flags & Vehicle.Flags.Underground) != 0;
            frameData.m_transition = (vehicleData.m_flags & Vehicle.Flags.Transition) != 0;

            // base.SimulationStep(vehicleID, ref vehicleData, ref frameData, leaderID, ref leaderData, lodPhysics);
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

            if (info.m_lanes != null && info.m_lanes.Length > position.m_lane) {
                var laneSpeedLimit = Options.customSpeedLimitsEnabled
                                         ? SpeedLimitManager.Instance.GetLockFreeGameSpeedLimit(
                                             position.m_segment,
                                             position.m_lane,
                                             laneId,
                                             info.m_lanes[position.m_lane])
                                         : info.m_lanes[position.m_lane].m_speedLimit;
                maxSpeed = CalculateTargetSpeed(
                    vehicleId,
                    ref vehicleData,
                    laneSpeedLimit,
                    instance.m_lanes.m_buffer[laneId].m_curve);
            } else {
                maxSpeed = CalculateTargetSpeed(vehicleId, ref vehicleData, 1f, 0f);
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
            // NON-STOCK CODE START
            var vehicleType = ExtVehicleManager.Instance.OnStartPathFind(vehicleId, ref vehicleData, null);
            if (vehicleType == ExtVehicleType.None) {
#if DEBUG
                Log.Warning($"CustomTrainAI.CustomStartPathFind: Vehicle {vehicleId} " +
                            $"does not have a valid vehicle type!");
#endif
                vehicleType = ExtVehicleType.RailVehicle;
            } else if (vehicleType == ExtVehicleType.CargoTrain) {
                vehicleType = ExtVehicleType.CargoVehicle;
            }

            // NON-STOCK CODE END
            var info = m_info;
            if ((vehicleData.m_flags & Vehicle.Flags.Spawned) == 0 && Vector3.Distance(startPos, endPos) < 100f) {
                startPos = endPos;
            }

            bool allowUnderground;
            bool allowUnderground2;
            if (info.m_vehicleType == VehicleInfo.VehicleType.Metro) {
                allowUnderground = true;
                allowUnderground2 = true;
            } else {
                allowUnderground = ((vehicleData.m_flags & (Vehicle.Flags.Underground
                                                            | Vehicle.Flags.Transition)) != 0);
                allowUnderground2 = false;
            }

            if (!PathManager.FindPathPosition(
                    startPos,
                    m_transportInfo.m_netService,
                    m_transportInfo.m_secondaryNetService,
                    NetInfo.LaneType.Vehicle,
                    info.m_vehicleType,
                    VehicleInfo.VehicleType.None,
                    allowUnderground,
                    false,
                    32f,
                    out var startPosA,
                    out var startPosB,
                    out var startSqrDistA,
                    out var startSqrDistB)
                || !PathManager.FindPathPosition(
                    endPos,
                    m_transportInfo.m_netService,
                    m_transportInfo.m_secondaryNetService,
                    NetInfo.LaneType.Vehicle,
                    info.m_vehicleType,
                    VehicleInfo.VehicleType.None,
                    allowUnderground2,
                    false,
                    32f,
                    out var endPosA,
                    out var endPosB,
                    out var endSqrDistA,
                    out var endSqrDistB)) {
                return false;
            }

            if (!startBothWays || startSqrDistB > startSqrDistA * 1.2f) {
                startPosB = default;
            }

            if (!endBothWays || endSqrDistB > endSqrDistA * 1.2f) {
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
            args.skipQueue = (vehicleData.m_flags & Vehicle.Flags.Spawned) != 0;

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

            return false;
        }

        [RedirectMethod]
        public void CustomCheckNextLane(ushort vehicleId,
                                        ref Vehicle vehicleData,
                                        ref float maxSpeed,
                                        PathUnit.Position nextPosition,
                                        uint nextLaneId,
                                        byte nextOffset,
                                        PathUnit.Position refPosition,
                                        uint refLaneId,
                                        byte refOffset,
                                        Bezier3 bezier) {
            var netManager = Singleton<NetManager>.instance;

            var nextSourceNodeId = nextOffset < nextPosition.m_offset
                                       ? netManager.m_segments.m_buffer[nextPosition.m_segment].m_startNode
                                       : netManager.m_segments.m_buffer[nextPosition.m_segment].m_endNode;

            var refTargetNodeId = refOffset == 0
                                      ? netManager.m_segments.m_buffer[refPosition.m_segment].m_startNode
                                      : netManager.m_segments.m_buffer[refPosition.m_segment].m_endNode;

#if DEBUG
            var logLogic = DebugSwitch.CalculateSegmentPosition.Get()
                           && (DebugSettings.NodeId <= 0
                               || refTargetNodeId == DebugSettings.NodeId)
                           && (GlobalConfig.Instance.Debug.ApiExtVehicleType == ExtVehicleType.None
                               || GlobalConfig.Instance.Debug.ApiExtVehicleType == ExtVehicleType.RailVehicle)
                           && (DebugSettings.VehicleId == 0
                               || DebugSettings.VehicleId == vehicleId);

            if (logLogic) {
                Log._Debug($"CustomTrainAI.CustomCheckNextLane({vehicleId}) called.\n" +
                           $"\trefPosition.m_segment={refPosition.m_segment}, " +
                           $"refPosition.m_offset={refPosition.m_offset}\n" +
                           $"\tnextPosition.m_segment={nextPosition.m_segment}, " +
                           $"nextPosition.m_offset={nextPosition.m_offset}\n" +
                           $"\trefLaneId={refLaneId}, refOffset={refOffset}\n" +
                           $"\tprevLaneId={nextLaneId}, prevOffset={nextOffset}\n" +
                           $"\tnextSourceNodeId={nextSourceNodeId}\n" +
                           $"\trefTargetNodeId={refTargetNodeId}, " +
                           $"refTargetNodeId={refTargetNodeId}");
            }
#endif

            var lastFrameData = vehicleData.GetLastFrameData();
            var sqrVelocity = lastFrameData.m_velocity.sqrMagnitude;

            var lastPosPlusRot = lastFrameData.m_position;
            var lastPosMinusRot = lastFrameData.m_position;
            var rotationAdd = lastFrameData.m_rotation
                              * new Vector3(0f, 0f, m_info.m_generatedInfo.m_wheelBase * 0.5f);
            lastPosPlusRot += rotationAdd;
            lastPosMinusRot -= rotationAdd;
            var breakingDist = 0.5f * sqrVelocity / m_info.m_braking;
            var distToTargetAfterRot = Vector3.Distance(lastPosPlusRot, bezier.a);
            var distToTargetBeforeRot = Vector3.Distance(lastPosMinusRot, bezier.a);

#if DEBUG
            if (logLogic) {
                Log._Debug($"CustomTrainAI.CustomCheckNextLane({vehicleId}): " +
                           $"lastPos={lastFrameData.m_position} " +
                           $"lastPosMinusRot={lastPosMinusRot} " +
                           $"lastPosPlusRot={lastPosPlusRot} " +
                           $"rotationAdd={rotationAdd} " +
                           $"breakingDist={breakingDist} " +
                           $"distToTargetAfterRot={distToTargetAfterRot} " +
                           $"distToTargetBeforeRot={distToTargetBeforeRot}");
            }
#endif

            if (Mathf.Min(distToTargetAfterRot, distToTargetBeforeRot) >= breakingDist - 5f) {
                /*VehicleManager vehMan = Singleton<VehicleManager>.instance;
                ushort firstVehicleId = vehicleData.GetFirstVehicle(vehicleId);
                if (VehicleBehaviorManager.Instance.MayDespawn(ref vehMan.m_vehicles.m_buffer[firstVehicleId]) || vehMan.m_vehicles.m_buffer[firstVehicleId].m_blockCounter < 100) {*/ // NON-STOCK CODE
#if DEBUG
                if (logLogic) {
                    Log._Debug($"CustomTrainAI.CustomCheckNextLane({vehicleId}): " +
                               $"Checking for free space on lane {nextLaneId}.");
                }
#endif

                if (!netManager.m_lanes.m_buffer[nextLaneId].CheckSpace(1000f, vehicleId)) {
#if DEBUG
                    if (logLogic) {
                        Log._Debug($"CustomTrainAI.CustomCheckNextLane({vehicleId}): " +
                                   $"No space available on lane {nextLaneId}. ABORT.");
                    }
#endif
                    vehicleData.m_flags2 |= Vehicle.Flags2.Yielding;
                    vehicleData.m_waitCounter = 0;
                    maxSpeed = 0f;
                    return;
                }

                var bezierMiddlePoint = bezier.Position(0.5f);

                var segment = Vector3.SqrMagnitude(vehicleData.m_segment.a - bezierMiddlePoint) <
                              Vector3.SqrMagnitude(bezier.a - bezierMiddlePoint)
                                  ? new Segment3(vehicleData.m_segment.a, bezierMiddlePoint)
                                  : new Segment3(bezier.a, bezierMiddlePoint);

#if DEBUG
                if (logLogic) {
                    Log._Debug($"CustomTrainAI.CustomCheckNextLane({vehicleId}): " +
                               $"Checking for overlap (1). segment.a={segment.a} segment.b={segment.b}");
                }
#endif
                if (segment.LengthSqr() >= 3f) {
                    segment.a += (segment.b - segment.a).normalized * 2.5f;

                    if (CheckOverlap(vehicleId, ref vehicleData, segment, vehicleId)) {
#if DEBUG
                        if (logLogic) {
                            Log._Debug($"CustomTrainAI.CustomCheckNextLane({vehicleId}): " +
                                       $"Overlap detected (1). segment.LengthSqr()={segment.LengthSqr()} " +
                                       $"segment.a={segment.a} ABORT.");
                        }
#endif
                        vehicleData.m_flags2 |= Vehicle.Flags2.Yielding;
                        vehicleData.m_waitCounter = 0;
                        maxSpeed = 0f;
                        return;
                    }
                }

                segment = new Segment3(bezierMiddlePoint, bezier.d);
#if DEBUG
                if (logLogic) {
                    Log._Debug($"CustomTrainAI.CustomCheckNextLane({vehicleId}): " +
                               $"Checking for overlap (2). segment.a={segment.a} segment.b={segment.b}");
                }
#endif
                if (segment.LengthSqr() >= 1f
                    && CheckOverlap(vehicleId, ref vehicleData, segment, vehicleId)) {
#if DEBUG
                    if (logLogic) {
                        Log._Debug($"CustomTrainAI.CustomCheckNextLane({vehicleId}): " +
                                   $"Overlap detected (2). ABORT.");
                    }
#endif
                    vehicleData.m_flags2 |= Vehicle.Flags2.Yielding;
                    vehicleData.m_waitCounter = 0;
                    maxSpeed = 0f;
                    return;
                }

                // } // NON-STOCK CODE
                // if (this.m_info.m_vehicleType != VehicleInfo.VehicleType.Monorail) { // NON-STOCK CODE
                if (nextSourceNodeId != refTargetNodeId) {
                    return;
                }
#if DEBUG
                if (logLogic) {
                    Log._Debug($"CustomTrainAI.CustomCheckNextLane({vehicleId}): " +
                               $"Checking if vehicle is allowed to change segment.");
                }
#endif
                var oldMaxSpeed = maxSpeed;
                if (!VehicleBehaviorManager.Instance.MayChangeSegment(
                        vehicleId,
                        ref vehicleData,
                        sqrVelocity,
                        ref refPosition,
                        ref netManager.m_segments.m_buffer[refPosition.m_segment],
                        refTargetNodeId,
                        refLaneId,
                        ref nextPosition,
                        refTargetNodeId,
                        ref netManager.m_nodes.m_buffer[refTargetNodeId],
                        nextLaneId)) {
#if DEBUG
                    if (logLogic) {
                        Log._Debug($"CustomTrainAI.CustomCheckNextLane({vehicleId}): " +
                                   $"Vehicle is NOT allowed to change segment. ABORT.");
                    }
#endif
                    maxSpeed = 0;
                    return;
                }

                ExtVehicleManager.Instance.UpdateVehiclePosition(vehicleId, ref vehicleData
                    /*, lastFrameData.m_velocity.magnitude*/
                    );
                maxSpeed = oldMaxSpeed;
                //} // NON-STOCK CODE
            }
        }

        [RedirectMethod]
        private void CustomForceTrafficLights(ushort vehicleId, ref Vehicle vehicleData, bool reserveSpace) {
            var pathUnitId = vehicleData.m_path;
            if (pathUnitId != 0u) {
                var netMan = Singleton<NetManager>.instance;
                var pathMan = Singleton<PathManager>.instance;
                var pathPosIndex = vehicleData.m_pathPositionIndex;

                if (pathPosIndex == 255) {
                    pathPosIndex = 0;
                }

                pathPosIndex = (byte)(pathPosIndex >> 1);
                var stopLoop = false; // NON-STOCK CODE
                for (var i = 0; i < 6; i++) {
                    if (!pathMan.m_pathUnits.m_buffer[pathUnitId].GetPosition(pathPosIndex, out var position)) {
                        return;
                    }

                    // NON-STOCK CODE START
                    var transitNodeId = position.m_offset < 128
                                            ? netMan.m_segments.m_buffer[position.m_segment].m_startNode
                                            : netMan.m_segments.m_buffer[position.m_segment].m_endNode;

                    if (Options.timedLightsEnabled) {
                        // when a TTL is active only reserve space if it shows green
                        if (pathMan.m_pathUnits.m_buffer[pathUnitId].GetNextPosition(pathPosIndex, out var nextPos)) {
                            if (!VehicleBehaviorManager.Instance.IsSpaceReservationAllowed(
                                    transitNodeId,
                                    position,
                                    nextPos)) {
                                stopLoop = true;
                            }
                        }
                    }

                    // NON-STOCK CODE END
                    if (reserveSpace && i >= 1 && i <= 2) {
                        var laneId = PathManager.GetLaneID(position);
                        if (laneId != 0u) {
                            reserveSpace = netMan.m_lanes.m_buffer[laneId]
                                                 .ReserveSpace(m_info.m_generatedInfo.m_size.z, vehicleId);
                        }
                    }

                    ForceTrafficLights(transitNodeId, position); // NON-STOCK CODE

                    // NON-STOCK CODE START
                    if (stopLoop) {
                        return;
                    }

                    // NON-STOCK CODE END
                    if ((pathPosIndex += 1) >= pathMan.m_pathUnits.m_buffer[pathUnitId].m_positionCount) {
                        pathUnitId = pathMan.m_pathUnits.m_buffer[pathUnitId].m_nextPathUnit;
                        pathPosIndex = 0;
                        if (pathUnitId == 0u) {
                            return;
                        }
                    }
                }
            }
        }

        // slightly modified version of TrainAI.ForceTrafficLights(PathUnit.Position)
        private static void ForceTrafficLights(ushort transitNodeId, PathUnit.Position position) {
            var netMan = Singleton<NetManager>.instance;
            if ((netMan.m_nodes.m_buffer[transitNodeId].m_flags & NetNode.Flags.TrafficLights) == NetNode.Flags.None) {
                return;
            }

            var frame = Singleton<SimulationManager>.instance.m_currentFrameIndex;
            var simGroup = (uint)transitNodeId >> 7;
            var rand = frame - simGroup & 255u;
            RoadBaseAI.GetTrafficLightState(
                transitNodeId,
                ref netMan.m_segments.m_buffer[position.m_segment],
                frame - simGroup,
                out var vehicleLightState,
                out var pedestrianLightState,
                out var vehicles,
                out var pedestrians);

            if (!vehicles && rand >= 196u) {
                vehicles = true;
                RoadBaseAI.SetTrafficLightState(
                    transitNodeId,
                    ref netMan.m_segments.m_buffer[position.m_segment],
                    frame - simGroup,
                    vehicleLightState,
                    pedestrianLightState,
                    vehicles,
                    pedestrians);
            }
        }

        [RedirectReverse]
        [MethodImpl(MethodImplOptions.NoInlining)]
        [UsedImplicitly]
        protected static bool CheckOverlap(ushort vehicleId,
                                           ref Vehicle vehicleData,
                                           Segment3 segment,
                                           ushort ignoreVehicle) {
            Log.Error("CustomTrainAI.CheckOverlap (1) called.");
            return false;
        }

        [RedirectReverse]
        [MethodImpl(MethodImplOptions.NoInlining)]
        [UsedImplicitly]
        protected static ushort CheckOverlap(ushort vehicleId,
                                             ref Vehicle vehicleData,
                                             Segment3 segment,
                                             ushort ignoreVehicle,
                                             ushort otherId,
                                             ref Vehicle otherData,
                                             ref bool overlap,
                                             Vector3 min,
                                             Vector3 max) {
            Log.Error("CustomTrainAI.CheckOverlap (2) called.");
            return 0;
        }

        [RedirectReverse]
        [MethodImpl(MethodImplOptions.NoInlining)]
        [UsedImplicitly]
        private static void InitializePath(ushort vehicleId, ref Vehicle vehicleData) {
            Log.Error("CustomTrainAI.InitializePath called");
        }

        [RedirectReverse]
        [MethodImpl(MethodImplOptions.NoInlining)]
        [UsedImplicitly]
        public static void UpdatePathTargetPositions(TrainAI trainAi,
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
            Log.Error($"CustomTrainAI.InvokeUpdatePathTargetPositions called! trainAI={trainAi}");
        }

        [RedirectReverse]
        [MethodImpl(MethodImplOptions.NoInlining)]
        [UsedImplicitly]
        private static void Reverse(ushort leaderId, ref Vehicle leaderData) {
            Log.Error("CustomTrainAI.Reverse called");
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
    }
}