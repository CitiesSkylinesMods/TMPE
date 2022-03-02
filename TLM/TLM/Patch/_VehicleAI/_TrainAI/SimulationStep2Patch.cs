namespace TrafficManager.Patch._VehicleAI._TrainAI {
    using System.Reflection;
    using ColossalFramework;
    using ColossalFramework.Math;
    using Connection;
    using HarmonyLib;
    using JetBrains.Annotations;
    using Manager.Impl;
    using TrafficManager.Util.Extensions;
    using UnityEngine;
    using Util;

    [UsedImplicitly]
    [HarmonyPatch]
    public class SimulationStep2Patch {
        private delegate void SimulationStepDelegate(ushort vehicleID,
                                                        ref Vehicle vehicleData,
                                                        ref Vehicle.Frame frameData,
                                                        ushort leaderID,
                                                        ref Vehicle leaderData,
                                                        int lodPhysics);

        [UsedImplicitly]
        public static MethodBase TargetMethod() => TranspilerUtil.DeclaredMethod<SimulationStepDelegate>(typeof(TrainAI), "SimulationStep");

        private static UpdatePathTargetPositionsDelegate UpdatePathTargetPositions;
        private static GetNoiseLevelDelegate GetNoiseLevel;
        private static GetMaxSpeedDelegate GetMaxSpeed;
        private static CalculateMaxSpeedDelegate CalculateMaxSpeed;
        private static ReverseDelegate Reverse;
        private static CalculateTargetSpeedTrainDelegate CalculateTargetSpeed;
        private static ForceTrafficLightsDelegate ForceTrafficLights;

        [UsedImplicitly]
        public static void Prepare() {
            UpdatePathTargetPositions = GameConnectionManager.Instance.TrainAIConnection.UpdatePathTargetPositions;
            GetNoiseLevel = GameConnectionManager.Instance.TrainAIConnection.GetNoiseLevel;
            GetMaxSpeed = GameConnectionManager.Instance.TrainAIConnection.GetMaxSpeed;
            CalculateMaxSpeed = GameConnectionManager.Instance.TrainAIConnection.CalculateMaxSpeed;
            Reverse = GameConnectionManager.Instance.TrainAIConnection.Reverse;
            CalculateTargetSpeed = GameConnectionManager.Instance.TrainAIConnection.CalculateTargetSpeed;
            ForceTrafficLights = GameConnectionManager.Instance.TrainAIConnection.ForceTrafficLights;
        }

        [UsedImplicitly]
        public static bool Prefix(TrainAI __instance,
                                  VehicleInfo ___m_info,
                                  ushort vehicleID,
                                  ref Vehicle vehicleData,
                                  ref Vehicle.Frame frameData,
                                  ushort leaderID,
                                  ref Vehicle leaderData,
                                  int lodPhysics) {
            bool reversed = (leaderData.m_flags & Vehicle.Flags.Reversed) != 0;
            ushort frontVehicleId = (!reversed) ? vehicleData.m_leadingVehicle : vehicleData.m_trailingVehicle;
            VehicleInfo vehicleInfo = leaderID != vehicleID ? leaderData.Info : ___m_info;
            TrainAI trainAi = vehicleInfo.m_vehicleAI as TrainAI;

            if (frontVehicleId != 0) {
                frameData.m_position += frameData.m_velocity * 0.4f;
            } else {
                frameData.m_position += frameData.m_velocity * 0.5f;
            }

            frameData.m_swayPosition += frameData.m_swayVelocity * 0.5f;

            Vector3 posBeforeWheelRot = frameData.m_position;
            Vector3 posAfterWheelRot = frameData.m_position;
            Vector3 wheelBaseRot = frameData.m_rotation
                               * new Vector3(0f, 0f, ___m_info.m_generatedInfo.m_wheelBase * 0.5f);

            if (reversed) {
                posBeforeWheelRot -= wheelBaseRot;
                posAfterWheelRot += wheelBaseRot;
            } else {
                posBeforeWheelRot += wheelBaseRot;
                posAfterWheelRot -= wheelBaseRot;
            }

            float acceleration = ___m_info.m_acceleration;
            float braking = ___m_info.m_braking;
            float curSpeed = frameData.m_velocity.magnitude;

            Vector3 beforeRotToTargetPos1Diff = (Vector3)vehicleData.m_targetPos1 - posBeforeWheelRot;
            float beforeRotToTargetPos1DiffSqrMag = beforeRotToTargetPos1Diff.sqrMagnitude;

            Quaternion curInvRot = Quaternion.Inverse(frameData.m_rotation);
            Vector3 curveTangent = curInvRot * frameData.m_velocity;

            Vector3 forward = Vector3.forward;
            Vector3 targetMotion = Vector3.zero;
            float targetSpeed = 0f;
            float motionFactor = 0.5f;

            if (frontVehicleId != 0) {
                ref Vehicle frontVehicle = ref frontVehicleId.ToVehicle();
                Vehicle.Frame frontVehLastFrameData = frontVehicle.GetLastFrameData();
                VehicleInfo frontVehInfo = frontVehicle.Info;
                float attachOffset;

                if ((vehicleData.m_flags & Vehicle.Flags.Inverted) != 0 != reversed) {
                    attachOffset = ___m_info.m_attachOffsetBack - (___m_info.m_generatedInfo.m_size.z * 0.5f);
                } else {
                    attachOffset = ___m_info.m_attachOffsetFront - (___m_info.m_generatedInfo.m_size.z * 0.5f);
                }

                float frontAttachOffset;

                if ((frontVehicle.m_flags & Vehicle.Flags.Inverted) != 0 != reversed) {
                    frontAttachOffset = frontVehInfo.m_attachOffsetFront -
                                        (frontVehInfo.m_generatedInfo.m_size.z * 0.5f);
                } else {
                    frontAttachOffset =
                        (frontVehInfo.m_attachOffsetBack - (frontVehInfo.m_generatedInfo.m_size.z * 0.5f));
                }

                Vector3 posMinusAttachOffset = frameData.m_position;
                if (reversed) {
                    posMinusAttachOffset += frameData.m_rotation * new Vector3(0f, 0f, attachOffset);
                } else {
                    posMinusAttachOffset -= frameData.m_rotation * new Vector3(0f, 0f, attachOffset);
                }

                Vector3 frontPosPlusAttachOffset = frontVehLastFrameData.m_position;
                if (reversed) {
                    frontPosPlusAttachOffset -= frontVehLastFrameData.m_rotation
                                                * new Vector3(0f, 0f, frontAttachOffset);
                } else {
                    frontPosPlusAttachOffset += frontVehLastFrameData.m_rotation
                                                * new Vector3(0f, 0f, frontAttachOffset);
                }

                Vector3 frontPosMinusWheelBaseRot = frontVehLastFrameData.m_position;
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
                    int someIndex = -1;
                    UpdatePathTargetPositions(
                        trainAi,
                        vehicleID,
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

                float maxAttachDist = Mathf.Max(Vector3.Distance(posMinusAttachOffset,
                                                               frontPosPlusAttachOffset),
                                              2f);
                const float ONE = 1f;
                float maxAttachSqrDist = maxAttachDist * maxAttachDist;
                const float ONE_SQR = ONE * ONE;
                int i = 0;
                if (beforeRotToTargetPos1DiffSqrMag < maxAttachSqrDist) {
                    if (vehicleData.m_path != 0u
                        && (leaderData.m_flags & Vehicle.Flags.WaitingPath) == 0) {
                        UpdatePathTargetPositions(
                            trainAi,
                            vehicleID,
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
                    byte pathPosIndex = vehicleData.m_pathPositionIndex;
                    byte lastPathOffset = vehicleData.m_lastPathOffset;
                    if (pathPosIndex == 255) {
                        pathPosIndex = 0;
                    }

                    PathManager pathMan = PathManager.instance;
                    if (pathMan.m_pathUnits.m_buffer[vehicleData.m_path]
                               .GetPosition(pathPosIndex >> 1, out PathUnit.Position curPathPos)) {

                        ref NetSegment currentPositionSegment = ref curPathPos.m_segment.ToSegment();

                        currentPositionSegment.AddTraffic(
                            Mathf.RoundToInt(___m_info.m_generatedInfo.m_size.z * 3f),
                            GetNoiseLevel(trainAi));

                        if ((pathPosIndex & 1) == 0 || lastPathOffset == 0 ||
                            (leaderData.m_flags & Vehicle.Flags.WaitingPath) != 0) {
                            uint laneId = PathManager.GetLaneID(curPathPos);
                            if (laneId != 0u) {
                                laneId.ToLane().ReserveSpace(___m_info.m_generatedInfo.m_size.z);
                            }
                        } else if (pathMan.m_pathUnits.m_buffer[vehicleData.m_path]
                                          .GetNextPosition(pathPosIndex >> 1, out PathUnit.Position nextPathPos)) {
                            // NON-STOCK CODE START
                            ushort transitNodeId;

                            if (curPathPos.m_offset < 128) {
                                transitNodeId = currentPositionSegment.m_startNode;
                            } else {
                                transitNodeId = currentPositionSegment.m_endNode;
                            }

                            if (VehicleBehaviorManager.Instance.IsSpaceReservationAllowed(
                                transitNodeId,
                                curPathPos,
                                nextPathPos)) {
                                // NON-STOCK CODE END
                                uint nextLaneId = PathManager.GetLaneID(nextPathPos);
                                if (nextLaneId != 0u) {
                                    nextLaneId.ToLane().ReserveSpace(___m_info.m_generatedInfo.m_size.z);
                                }
                            } // NON-STOCK CODE
                        }
                    }
                }

                beforeRotToTargetPos1Diff = curInvRot * beforeRotToTargetPos1Diff;
                float negTotalAttachLen =
                    -(((___m_info.m_generatedInfo.m_wheelBase +
                        frontVehInfo.m_generatedInfo.m_wheelBase) * 0.5f) + attachOffset +
                      frontAttachOffset);
                bool hasPath = false;

                if (vehicleData.m_path != 0u && (leaderData.m_flags & Vehicle.Flags.WaitingPath) == 0) {
                    if (Line3.Intersect(
                        posBeforeWheelRot,
                        vehicleData.m_targetPos1,
                        frontPosMinusWheelBaseRot,
                        negTotalAttachLen,
                        out float u1,
                        out float u2)) {
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
                    float frontPosBeforeToAfterWheelRotDist = Vector3.Distance(
                        frontPosMinusWheelBaseRot,
                        posBeforeWheelRot);
                    motionFactor = 0f;
                    targetMotion = curInvRot
                                   * ((frontPosMinusWheelBaseRot - posBeforeWheelRot)
                                      * (Mathf.Max(0f, frontPosBeforeToAfterWheelRotDist - negTotalAttachLen)
                                         / Mathf.Max(1f, frontPosBeforeToAfterWheelRotDist * 0.6f)));
                }
            } else {
                float estimatedFrameDist = (curSpeed + acceleration)
                                         * (0.5f + (0.5f * (curSpeed + acceleration) / braking));
                float maxSpeedAdd = Mathf.Max(curSpeed + acceleration, 2f);
                float meanSpeedAdd = Mathf.Max((estimatedFrameDist - maxSpeedAdd) / 2f, 1f);
                float maxSpeedAddSqr = maxSpeedAdd * maxSpeedAdd;
                float meanSpeedAddSqr = meanSpeedAdd * meanSpeedAdd;

                if (Vector3.Dot(
                        vehicleData.m_targetPos1 - vehicleData.m_targetPos0,
                        (Vector3)vehicleData.m_targetPos0 - posAfterWheelRot) < 0f
                    && vehicleData.m_path != 0u
                    && (leaderData.m_flags & (Vehicle.Flags.WaitingPath | Vehicle.Flags.Stopped)) == 0)
                {
                    int someIndex = -1;
                    UpdatePathTargetPositions(
                        trainAi,
                        vehicleID,
                        ref vehicleData,
                        vehicleData.m_targetPos0,
                        posAfterWheelRot,
                        leaderID,
                        ref leaderData,
                        ref someIndex,
                        0,
                        0,
                        Vector3.SqrMagnitude(posAfterWheelRot - (Vector3)vehicleData.m_targetPos0) + 1f,
                        1f);
                    beforeRotToTargetPos1DiffSqrMag = 0f;
                }

                int posIndex = 0;
                bool flag3 = false;
                if ((beforeRotToTargetPos1DiffSqrMag < maxSpeedAddSqr
                     || vehicleData.m_targetPos3.w < 0.01f)
                    && (leaderData.m_flags & (Vehicle.Flags.WaitingPath | Vehicle.Flags.Stopped)) == 0)
                {
                    if (vehicleData.m_path != 0u) {
                        UpdatePathTargetPositions(
                            trainAi,
                            vehicleID,
                            ref vehicleData,
                            posAfterWheelRot,
                            posBeforeWheelRot,
                            leaderID,
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
                    && ___m_info.m_vehicleType != VehicleInfo.VehicleType.Monorail) {
                    ForceTrafficLights(__instance, vehicleID, ref vehicleData, curSpeed > 0.1f);
                    // NON-STOCK CODE
                }

                if (vehicleData.m_path != 0u) {
                    NetManager netMan = Singleton<NetManager>.instance;
                    byte pathPosIndex = vehicleData.m_pathPositionIndex;
                    byte lastPathOffset = vehicleData.m_lastPathOffset;
                    if (pathPosIndex == 255) {
                        pathPosIndex = 0;
                    }

                    PathManager pathMan = Singleton<PathManager>.instance;
                    if (pathMan.m_pathUnits.m_buffer[vehicleData.m_path]
                               .GetPosition(pathPosIndex >> 1, out PathUnit.Position curPathPos))
                    {
                        ref NetSegment currentPositionSegment = ref curPathPos.m_segment.ToSegment();

                        currentPositionSegment.AddTraffic(
                            Mathf.RoundToInt(___m_info.m_generatedInfo.m_size.z * 3f),
                            GetNoiseLevel(trainAi));

                        if ((pathPosIndex & 1) == 0
                            || lastPathOffset == 0
                            || (leaderData.m_flags & Vehicle.Flags.WaitingPath) != 0)
                        {
                            uint laneId = PathManager.GetLaneID(curPathPos);
                            if (laneId != 0u) {
                                laneId.ToLane().ReserveSpace(___m_info.m_generatedInfo.m_size.z, vehicleID);
                            }
                        } else if (pathMan.m_pathUnits.m_buffer[vehicleData.m_path]
                                          .GetNextPosition(pathPosIndex >> 1, out PathUnit.Position nextPathPos)) {
                            // NON-STOCK CODE START
                            ushort transitNodeId;
                            transitNodeId = curPathPos.m_offset < 128
                                ? currentPositionSegment.m_startNode
                                : currentPositionSegment.m_endNode;

                            if (VehicleBehaviorManager.Instance.IsSpaceReservationAllowed(
                                transitNodeId,
                                curPathPos,
                                nextPathPos)) {
                                // NON-STOCK CODE END
                                uint nextLaneId = PathManager.GetLaneID(nextPathPos);
                                if (nextLaneId != 0u) {
                                    nextLaneId.ToLane().ReserveSpace(
                                        ___m_info.m_generatedInfo.m_size.z, vehicleID);
                                }
                            } // NON-STOCK CODE
                        }
                    }
                }

                float maxSpeed;

                maxSpeed = (leaderData.m_flags & Vehicle.Flags.Stopped) != 0
                               ? 0f
                               : Mathf.Min(vehicleData.m_targetPos1.w, GetMaxSpeed(leaderID, ref leaderData));

                beforeRotToTargetPos1Diff = curInvRot * beforeRotToTargetPos1Diff;
                if (reversed) {
                    beforeRotToTargetPos1Diff = -beforeRotToTargetPos1Diff;
                }

                bool blocked = false;
                float forwardLen = 0f;

                if (beforeRotToTargetPos1DiffSqrMag > 1f) {
                    forward = VectorUtils.NormalizeXZ(beforeRotToTargetPos1Diff, out forwardLen);

                    if (forwardLen > 1f) {
                        Vector3 fwd = beforeRotToTargetPos1Diff;
                        maxSpeedAdd = Mathf.Max(curSpeed, 2f);
                        maxSpeedAddSqr = maxSpeedAdd * maxSpeedAdd;
                        if (beforeRotToTargetPos1DiffSqrMag > maxSpeedAddSqr) {
                            float num20 = maxSpeedAdd / Mathf.Sqrt(beforeRotToTargetPos1DiffSqrMag);
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
                                            Reverse(leaderID, ref leaderData);
                                            return false;
                                        }

                                        fwd.z = 0f;
                                        beforeRotToTargetPos1Diff = Vector3.zero;
                                        maxSpeed = 0f;
                                    } else {
                                        posBeforeWheelRot =
                                            posAfterWheelRot +
                                            Vector3.Normalize(
                                                vehicleData.m_targetPos1 -
                                                vehicleData.m_targetPos0) *
                                            ___m_info.m_generatedInfo.m_wheelBase;
                                        posIndex = -1;
                                        UpdatePathTargetPositions(
                                            trainAi,
                                            vehicleID,
                                            ref vehicleData,
                                            vehicleData.m_targetPos0,
                                            vehicleData.m_targetPos1,
                                            leaderID,
                                            ref leaderData,
                                            ref posIndex,
                                            0,
                                            0,
                                            Vector3.SqrMagnitude(
                                                vehicleData.m_targetPos1 -
                                                vehicleData.m_targetPos0) + 1f,
                                            1f);
                                    }
                                } else {
                                    posIndex = -1;
                                    UpdatePathTargetPositions(
                                        trainAi,
                                        vehicleID,
                                        ref vehicleData,
                                        vehicleData.m_targetPos0,
                                        posAfterWheelRot,
                                        leaderID,
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
                        float curve = Mathf.PI / 2f * (1f - forward.z);
                        if (forwardLen > 1f) {
                            curve /= forwardLen;
                        }

                        maxSpeed = Mathf.Min(
                            maxSpeed,
                            CalculateTargetSpeed(
                                __instance,
                                vehicleID,
                                ref vehicleData,
                                1000f,
                                curve));

                        float targetDist = forwardLen;
                        maxSpeed = Mathf.Min(
                            maxSpeed,
                            CalculateMaxSpeed(
                                targetDist,
                                vehicleData.m_targetPos2.w,
                                braking));
                        targetDist += VectorUtils.LengthXZ(vehicleData.m_targetPos2 - vehicleData.m_targetPos1);
                        maxSpeed = Mathf.Min(maxSpeed,
                                             CalculateMaxSpeed(targetDist,
                                                               vehicleData.m_targetPos3.w,
                                                               braking));
                        targetDist += VectorUtils.LengthXZ(vehicleData.m_targetPos3 - vehicleData.m_targetPos2);
                        maxSpeed = Mathf.Min(maxSpeed,
                                             CalculateMaxSpeed(targetDist, 0f, braking));

                        if (maxSpeed < curSpeed) {
                            float brake = Mathf.Max(acceleration, Mathf.Min(braking, curSpeed));
                            targetSpeed = Mathf.Max(maxSpeed, curSpeed - brake);
                        } else {
                            float accel = Mathf.Max(acceleration, Mathf.Min(braking, -curSpeed));
                            targetSpeed = Mathf.Min(maxSpeed, curSpeed + accel);
                        }
                    }
                } else if (curSpeed < 0.1f && flag3 &&
                           vehicleInfo.m_vehicleAI.ArriveAtDestination(leaderID, ref leaderData)) {
                    leaderData.Unspawn(leaderID);
                    return false;
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

                    Vector3 vel = Vector3.ClampMagnitude((beforeRotToTargetPos1Diff * 0.5f) - curveTangent, braking);
                    targetMotion = curveTangent + vel;
                }
            }

            Vector3 springs = targetMotion - curveTangent;
            Vector3 targetAfterWheelRotMotion = frameData.m_rotation * targetMotion;
            Vector3 posAfterWheelRotToTargetDiff = Vector3.Normalize((Vector3)vehicleData.m_targetPos0 - posAfterWheelRot)
                                               * (targetMotion.magnitude * motionFactor);
            posBeforeWheelRot += targetAfterWheelRotMotion;
            posAfterWheelRot += posAfterWheelRotToTargetDiff;

            Vector3 targetPos;
            if (reversed) {
                frameData.m_rotation = Quaternion.LookRotation(posAfterWheelRot - posBeforeWheelRot);
                targetPos = posBeforeWheelRot + (frameData.m_rotation * new Vector3(
                                                     0f,
                                                     0f,
                                                     ___m_info.m_generatedInfo.m_wheelBase * 0.5f));
            } else {
                frameData.m_rotation = Quaternion.LookRotation(posBeforeWheelRot - posAfterWheelRot);
                targetPos = posBeforeWheelRot - (frameData.m_rotation * new Vector3(
                                                     0f,
                                                     0f,
                                                     ___m_info.m_generatedInfo.m_wheelBase * 0.5f));
            }

            frameData.m_velocity = targetPos - frameData.m_position;

            if (frontVehicleId != 0) {
                frameData.m_position += frameData.m_velocity * 0.6f;
            } else {
                frameData.m_position += frameData.m_velocity * 0.5f;
            }

            frameData.m_swayVelocity =
                ((frameData.m_swayVelocity * (1f - ___m_info.m_dampers)) - (springs * (1f - ___m_info.m_springs)))
                - (frameData.m_swayPosition * ___m_info.m_springs);
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
            return false;
        }
    }
}