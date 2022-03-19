namespace TrafficManager.Patch._VehicleAI._AircraftAI {
    using System;
    using System.Reflection;
    using ColossalFramework.Math;
    using CSUtil.Commons;
    using HarmonyLib;
    using JetBrains.Annotations;
    using TrafficManager.API.Traffic.Data;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.Custom.BuildingSearch;
    using TrafficManager.Manager.Impl;
    using TrafficManager.Patch._VehicleAI._AircraftAI.Connection;
    using TrafficManager.Patch._VehicleAI.Connection;
    using TrafficManager.Util;
    using TrafficManager.Util.Extensions;
    using UnityEngine;

    [HarmonyPatch]
    public class SimulationStep2Patch {
        private delegate void SimulationStepTargetDelegate(ushort vehicleID,
                                                           ref Vehicle vehicleData,
                                                           ref Vehicle.Frame frameData,
                                                           ushort leaderID,
                                                           ref Vehicle leaderData,
                                                           int lodPhysics);


        [UsedImplicitly]
        public static MethodBase TargetMethod() =>
            TranspilerUtil.DeclaredMethod<SimulationStepTargetDelegate>(
                typeof(AircraftAI),
                "SimulationStep");

        // todo vvv replace with fn vvv
        public static float Angle100m = 0.22f;
        public static float Angle50m = 0.21f;
        public static float Angle25m = 0.20f;
        public static float Angle10m = 0.1875f;
        public static float Angle05m = 0.155f;
        public static float Angle03m = 0.065f;
        public static float Angle01m = 0.02f;
        // todo ^^^ replace ^^^

        private static ArriveAtDestinationDelegate ArriveAtDestination;
        private static CalculateTargetSpeedDelegate CalculateTargetSpeed;
        private static CalculateMaxSpeedDelegate CalculateMaxSpeed;
        private static IsFlightPathAheadDelegate IsFlightPathAhead;
        private static IsOnFlightPathDelegate IsOnFlightPath;
        private static ReserveSpaceDelegate ReserveSpace;
        private static UpdateBuildingTargetPositionsDelegate UpdateBuildingTargetPositions;
        private static UpdatePathTargetPositionsDelegate UpdatePathTargetPositions;

        [UsedImplicitly]
        public static void Prepare() {
            var vehicleAIConnection = GameConnectionManager.Instance.VehicleAIConnection;
            CalculateTargetSpeed = vehicleAIConnection.CalculateTargetSpeed;
            UpdateBuildingTargetPositions = vehicleAIConnection.UpdateBuildingTargetPositions;
            ArriveAtDestination = vehicleAIConnection.ArriveAtDestination;
            UpdatePathTargetPositions = vehicleAIConnection.UpdatePathTargetPositions;
            var aircraftAIConnection = GameConnectionManager.Instance.AircraftAIConnection;
            CalculateMaxSpeed = aircraftAIConnection.CalculateMaxSpeed;
            IsFlightPathAhead = aircraftAIConnection.IsFlightPathAhead;
            IsOnFlightPath = aircraftAIConnection.IsOnFlightPath;
            ReserveSpace = aircraftAIConnection.ReserveSpace;
        }

        [UsedImplicitly]
        public static bool Prefix(AircraftAI __instance,
                                  VehicleInfo ___m_info,
                                  ushort vehicleID,
                                  ref Vehicle vehicleData,
                                  ref Vehicle.Frame frameData,
                                  ushort leaderID,
                                  ref Vehicle leaderData,
                                  int lodPhysics) {
            bool logLogic = VersionUtil.IS_DEBUG && InstanceManager.instance.GetSelectedInstance().Vehicle == vehicleID;

            ref ExtVehicle extVehicle = ref vehicleID.ToExtVehicle();
            if (!extVehicle.airplaneRotation.HasValue) {
                extVehicle.airplaneRotation = frameData.m_rotation;
            }

            frameData.m_position += frameData.m_velocity * 0.5f;
            frameData.m_swayPosition += frameData.m_swayVelocity * 0.5f;
            float acceleration = ___m_info.m_acceleration;
            float braking = ___m_info.m_braking;
            float curSpeed = frameData.m_velocity.magnitude;
            Vector3 vector = (Vector3)vehicleData.m_targetPos0 - frameData.m_position; // previous vector to target
            float sqrMagnitude = vector.sqrMagnitude;
            float estimatedFrameDist = (curSpeed + acceleration) *
                                       (0.5f + 0.5f * (curSpeed + acceleration) / (braking * 0.9f)) +
                                       ___m_info.m_generatedInfo.m_size.z * 0.5f;
            float maxSpeedAdd = Mathf.Max(curSpeed + curSpeed * curSpeed * 0.1f + acceleration, 2f);
            float meanSpeedAdd = Mathf.Max((estimatedFrameDist - maxSpeedAdd) / 3f, 1f);
            float maxSpeedAddSqr = maxSpeedAdd * maxSpeedAdd;
            float meanSpeedAddSqr = meanSpeedAdd * meanSpeedAdd;
            int currTargetPosIndex = 0;

            if (logLogic) {
                Log._Debug($"AircraftAI.SimulationStep({vehicleID}): Call SimulationStep()," +
                           $"\n\tcurSpeed: {curSpeed}, sqrMagnitude: {sqrMagnitude}," +
                           $"\n\tminSqrDistanceA: {maxSpeedAddSqr}; minSqrDistanceB:{meanSpeedAddSqr}," +
                           $"\n\testimatedFrameDist: {estimatedFrameDist}, maxSpeedAdd: {maxSpeedAdd}, meanSpeedAdd: {meanSpeedAdd}," +
                           $"\n\tlastTargetPos3.w: {vehicleData.m_targetPos3.w} isWaitingPath/Stopped: {(leaderData.m_flags & (Vehicle.Flags.WaitingPath | Vehicle.Flags.Stopped)) != 0}");
            }

            bool onlyFirstTargetPosUpdated = false;
            if ((sqrMagnitude < maxSpeedAddSqr || vehicleData.m_targetPos3.w < 0.01f) &&
                (leaderData.m_flags & (Vehicle.Flags.WaitingPath | Vehicle.Flags.Stopped)) == 0) {
                if (leaderData.m_path != 0) {
                    Log._DebugIf(logLogic, () => $"AircraftAI.SimulationStep({vehicleID}): Call UpdatePathTargetPositions(), " +
                                                 $"sqrMagnitude: {sqrMagnitude}, minSqrDistanceA: {maxSpeedAddSqr}; minSqrDistanceB:{meanSpeedAddSqr}");

                    // calculate new target positions
                    UpdatePathTargetPositions(
                        __instance,
                        vehicleID,
                        ref vehicleData,
                        frameData.m_position,
                        ref currTargetPosIndex,
                        4,
                        maxSpeedAddSqr,
                        meanSpeedAddSqr);

                    Log._DebugIf(logLogic, () => $"AircraftAI.SimulationStep({vehicleID}): After call UpdatePathTargetPositions(), i: {currTargetPosIndex}");
                }

                if (currTargetPosIndex < 4) {
                    if (currTargetPosIndex == 0) {
                        onlyFirstTargetPosUpdated = true;
                    }
                    // building reached? update target positions

                    Log._DebugIf(logLogic, () => $"AircraftAI.SimulationStep({vehicleID}): [currTargetPosIndex < 4]: {currTargetPosIndex}, onlyFirst: {onlyFirstTargetPosUpdated}");
                    UpdateBuildingTargetPositions(
                        __instance,
                        vehicleID,
                        ref vehicleData,
                        frameData.m_position,
                        leaderID,
                        ref leaderData,
                        ref currTargetPosIndex,
                        maxSpeedAddSqr);
                }

                // copy last calculated position in this frame to remaining target positions
                for (; currTargetPosIndex < 4; currTargetPosIndex++) {
                    vehicleData.SetTargetPos(currTargetPosIndex, vehicleData.GetTargetPos(currTargetPosIndex - 1));
                }

                vector = (Vector3)vehicleData.m_targetPos0 - frameData.m_position;

                Log._DebugIf(logLogic, () => $"AircraftAI.SimulationStep({vehicleID}): TargetPos0_FramePos_Diff: {vector}, mag: {vector.magnitude}");
            }

            Vehicle.Flags flags = leaderData.m_flags;
            if ((flags & Vehicle.Flags.Flying) == 0 || (flags & Vehicle.Flags.Landing) != 0) {
                Log._DebugIf(logLogic, () => $"AircraftAI.SimulationStep({vehicleID}): Reserving space.");

                ReserveSpace(__instance, vehicleID, ref vehicleData, curSpeed > 0.1f);
            }

            float currGroundSpeed = VectorUtils.LengthXZ(frameData.m_velocity);
            float lengthToTargetPos0 = VectorUtils.LengthXZ(vector);

            Log._DebugIf(logLogic, () => $"AircraftAI.SimulationStep({vehicleID}): Updating flags: {flags} | flatSpeed: {currGroundSpeed}, vectorLength: {lengthToTargetPos0}");

            if ((flags & Vehicle.Flags.TakingOff) != 0) {
                if (currGroundSpeed > ___m_info.m_maxSpeed * 0.4f/*STOCK VALUE 0.5f*/) {
                    vector.y += currGroundSpeed + (currGroundSpeed * currGroundSpeed * 0.09f/*STOCK VALUE 0.1f*/);
                    flags |= Vehicle.Flags.Flying;
                }

                if (vehicleData.m_targetPos0.w > 40f) {
                    flags &= ~Vehicle.Flags.TakingOff;
                    flags |= Vehicle.Flags.Flying;
                    vehicleData.m_waitCounter = 0;
                }

                if (vehicleData.m_waitCounter < 100) {
                    //can't find any other references, probably not used anywhere else
                    vehicleData.m_waitCounter++;
                } else if (!IsOnFlightPath(__instance, vehicleData)) {
                    flags &= ~Vehicle.Flags.TakingOff;
                    flags &= ~Vehicle.Flags.Flying;
                    vehicleData.m_waitCounter = 0;
                }
            } else if ((flags & Vehicle.Flags.Landing) != 0) {
                if (vector.y > -0.1f) {
                    flags &= ~Vehicle.Flags.Flying;
                }

                if (vehicleData.m_targetPos0.w < 16f) {
                    flags &= ~Vehicle.Flags.Landing;
                    flags &= ~Vehicle.Flags.Flying;
                }
            } else if ((flags & Vehicle.Flags.Flying) != 0) {
                Vector3 targetPos0 = vehicleData.m_targetPos0;
                Vector3 targetPos1 = vehicleData.m_targetPos1;
                Vector3 targetBeforePos1 = Vector3.Normalize(
                    new Vector3(targetPos0.x - targetPos1.x, 0f, targetPos0.z - targetPos1.z));

                float magnitude = vector.magnitude;
                // adjust position to do 15deg turn? on flat plane
                targetPos0 += targetBeforePos1 * (magnitude * 0.9659258f); // 0.9659258f == ~cos(15 deg)
                // then adjust height, 0.258819044f is some kind of **Magic Value**, maybe related with other magic limits or
                // maybe it if just hardcoded gradual descend rate?
                targetPos0.y = Mathf.Min(targetPos0.y + (magnitude * 0.258819044f), Mathf.Max(targetPos0.y, 700f/*STOCK VALUE 1500f*/));
                // calc new diff
                vector = targetPos0 - frameData.m_position;
                // sqr dist to target position, in this case probably a runway
                if (vector.sqrMagnitude < 40000f) {
                    // a = vehicleData.m_targetPos0;
                    vector = (Vector3)vehicleData.m_targetPos0 - frameData.m_position;
                    // check target pos, if segment has slow speed (e.g: start pos of the runway)
                    // and closer than X switch to Landing mode (in-game it will show aircraft wheels)
                    if (vehicleData.m_targetPos0.w < 40f && lengthToTargetPos0 < 500f) {
                        flags |= Vehicle.Flags.Landing;
                    }
                } else {
                    // update value with length of new vector
                    lengthToTargetPos0 = VectorUtils.LengthXZ(vector);
                }
            } else if (vehicleData.m_targetPos0.w > 16f &&
                       (vector.y > 5f || IsFlightPathAhead(__instance, vehicleData))) {
                // calc target speed, climb rate is enough
                // or flight path ahead (todo test if might cause weird touch&go landings)
                flags |= Vehicle.Flags.TakingOff;
            }

            Log._DebugIf(logLogic, () => $"AircraftAI.SimulationStep({vehicleID}): Flags updated: {flags}");

            leaderData.m_flags = flags;
            if ((leaderData.m_flags & Vehicle.Flags.Flying) != 0) {
                // Vector3 b = frameData.m_rotation * Vector3.forward;
                Vector3 b = extVehicle.airplaneRotation.Value * Vector3.forward;
                b.y = 0f;
                b.Normalize();
                Vector3 b2 = new Vector3(b.z, 0f, 0f - b.x);
                float num8 = VectorUtils.DotXZ(vector, b);
                float num9 = VectorUtils.DotXZ(vector, b2);
                if (num8 < Mathf.Abs(num9) * 5f) {
                    num9 = (!(num9 >= 0f)) ? (lengthToTargetPos0 * -0.2f) : (lengthToTargetPos0 * 0.2f);
                    num8 = lengthToTargetPos0;
                    vector.x = (b.x * num8) + (b2.x * num9);
                    vector.z = (b.z * num8) + (b2.z * num9);
                }

                float max = Mathf.Max(0.1f, lengthToTargetPos0 * ((currGroundSpeed / ___m_info.m_maxSpeed) - 0.5f));
                float min = Mathf.Min(-0.1f, lengthToTargetPos0 * -0.5f);
                vector.y = Mathf.Clamp(vector.y, min, max);
            }
            // Quaternion curvInvRot = Quaternion.Inverse(frameData.m_rotation);
            Quaternion curvInvRot = Quaternion.Inverse(extVehicle.airplaneRotation.Value);
            vector = curvInvRot * vector;
            Vector3 curveTangent = curvInvRot * frameData.m_velocity;
            if (curveTangent.z < 0f) {
                curSpeed = 0f - curSpeed;
            }

            Vector3 targetMotion = Vector3.zero;
            Vector3 forward = Vector3.forward;
            float targetSpeed = 0f;
            float steerAngle = 0f;
            float forwardLen = vector.magnitude;
            bool blocked = false;
            if (forwardLen > 1f) {
                Log._DebugIf(logLogic, () => $"AircraftAI.SimulationStep({vehicleID}): [forwardLen > 1f] ForwardLength: {forwardLen}, vector: {vector}, curSpeed: {curSpeed}, vectorMag: {vector.magnitude}");

                forward = vector / forwardLen;

                //NON STOCK CODE START
                float maxSpeed = ___m_info.m_maxSpeed;
                //NON STOCK CODE END
                Log._DebugIf(logLogic, () => $"AircraftAI.SimulationStep({vehicleID}): [forwardLen > 1f] Forward: {forward}, maxSpeed: {maxSpeed}");

                if ((leaderData.m_flags & Vehicle.Flags.Stopped) != 0) {
                    //stopped - at airplane stand stop
                    maxSpeed = 0f;
                } else {
                    float curve = 0f;
                    if ((leaderData.m_flags & Vehicle.Flags.Landing) != 0) {
                        maxSpeed = Mathf.Min(
                            maxSpeed,
                            CalculateMaxSpeed(forwardLen, vehicleData.m_targetPos0.w, braking));
                        if (logLogic) {
                            Log._Debug($"AircraftAI.SimulationStep({vehicleID}): [forwardLen > 1f] Landing: maxSpeed = {maxSpeed}, fwdLen: {forwardLen}, target0Spd: {vehicleData.m_targetPos0.w}, brake: {braking}");
                        }
                    } else if ((leaderData.m_flags & Vehicle.Flags.Flying) == 0) {
                        curve = (float)Math.PI / 2f * (1f - forward.z);
                        maxSpeed = Mathf.Min(
                            vehicleData.m_targetPos0.w,/* STOCK CODE: maxSpeed,*/
                            CalculateTargetSpeed(
                                __instance,
                                vehicleID,
                                ref vehicleData,
                                vehicleData.m_targetPos0.w,
                                curve));
                        if (logLogic) {
                            Log._Debug($"AircraftAI.SimulationStep({vehicleID}): [forwardLen > 1f] NotFlying: maxSpeed: {maxSpeed}, target0Spd: {vehicleData.m_targetPos0.w} curve: {curve}");
                        }
                    }

                    if ((leaderData.m_flags & (Vehicle.Flags.Flying | Vehicle.Flags.Landing)) != Vehicle.Flags.Flying) {
                        // landing not flying (not in the air)
                        float targetDist = forwardLen;
                        maxSpeed = Mathf.Min(maxSpeed, CalculateMaxSpeed(targetDist, vehicleData.m_targetPos1.w, braking * 0.9f));
                        Vehicle data = vehicleData;

                        Log._DebugIf(logLogic, () => $"AircraftAI.SimulationStep({vehicleID}): [forwardLen > 1f] NotFlying but may Land2: maxSpeed: {maxSpeed}, targDist: {targetDist}, vehPos1 {data.m_targetPos1} brk: {braking * 0.9}");

                        targetDist += VectorUtils.LengthXZ(vehicleData.m_targetPos1 - vehicleData.m_targetPos0);

                        Log._DebugIf(logLogic, () => $"AircraftAI.SimulationStep({vehicleID}): [forwardLen > 1f] targDis0: {data.m_targetPos1 - data.m_targetPos0}, targDist: {targetDist} tg0: {data.m_targetPos0} tg1: {data.m_targetPos1}");

                        maxSpeed = Mathf.Min(maxSpeed, CalculateMaxSpeed(targetDist, vehicleData.m_targetPos2.w, braking * 0.9f));

                        Log._DebugIf(logLogic, () => $"AircraftAI.SimulationStep({vehicleID}): [forwardLen > 1f] maxSpeed2: {maxSpeed}, tg2 {data.m_targetPos2}");
                        targetDist += VectorUtils.LengthXZ(vehicleData.m_targetPos2 - vehicleData.m_targetPos1);

                        Log._DebugIf(logLogic, () => $"AircraftAI.SimulationStep({vehicleID}): [forwardLen > 1f] targDis1: {data.m_targetPos2 - data.m_targetPos1}, targDist: {targetDist}");

                        maxSpeed = Mathf.Min(maxSpeed, CalculateMaxSpeed(targetDist, vehicleData.m_targetPos3.w, braking * 0.9f));

                        Log._DebugIf(logLogic, () => $"AircraftAI.SimulationStep({vehicleID}): [forwardLen > 1f] maxSpeed3: {maxSpeed}, tg3 {data.m_targetPos3}");

                        targetDist += VectorUtils.LengthXZ(vehicleData.m_targetPos3 - vehicleData.m_targetPos2);

                        Log._DebugIf(logLogic, () => $"AircraftAI.SimulationStep({vehicleID}): [forwardLen > 1f] targDis2: {data.m_targetPos3 - data.m_targetPos2}, targDist: {targetDist}");
                        // check for RunwayAI - ignore vanilla runways
                        // NON-STOCK CODE START
                        if (vehicleData.m_targetPos3.w < 0.01f &&
                            PathManager.instance.m_pathUnits.m_buffer[leaderData.m_path].GetPosition(leaderData.m_pathPositionIndex >> 1).m_segment.ToSegment().Info.m_netAI.GetType() != typeof(RunwayAI))
                        {
                            targetDist = Mathf.Max(0f, targetDist - (___m_info.m_generatedInfo.m_size.z * 0.75f));
                        }
                        // NON-STOCK CODE END
                        Log._DebugIf(logLogic, () => $"AircraftAI.SimulationStep({vehicleID}): [forwardLen > 1f] targDisLast: {data.m_targetPos3}, targDist: {targetDist}");
                        maxSpeed = Mathf.Min(maxSpeed, CalculateMaxSpeed(targetDist, 0f, braking * 0.9f));

                        Log._DebugIf(logLogic, () => $"AircraftAI.SimulationStep({vehicleID}): [forwardLen > 1f] Landing targetDist: {targetDist}, maxSpeed: {maxSpeed}");
                    }
                }

                if ((leaderData.m_flags & Vehicle.Flags.Flying) == 0) {
                    Log._DebugIf(logLogic, () => $"AircraftAI.SimulationStep({vehicleID}): NotFlying2 Checking (vector.z < Mathf.Abs(vector.x) * 2f): {vector.z} < {Mathf.Abs(vector.x) * 2}" +
                                                 $" z {vector.z}, abs_x: {Mathf.Abs(vector.x)}");
                    // no idea what is it for yet (turn while going backwards?)...
                    if (vector.z < Mathf.Abs(vector.x) * 2f) {
                        Log._DebugIf(logLogic, () => $"AircraftAI.SimulationStep({vehicleID}): NotFlying2 (vector.z < Mathf.Abs(vector.x) * 2f)" +
                                                     $"z {vector.z}, abs_x: {Mathf.Abs(vector.x)}");

                        if (vector.z < (0f - Mathf.Abs(vector.x)) * 20f) {
                            forward = -forward;
                            maxSpeed = 0f - maxSpeed;

                            Log._DebugIf(logLogic, () => $"AircraftAI.SimulationStep({vehicleID}): NotFlying2 (vector.z < (0f - Mathf.Abs(vector.x)) * 20f) fwd: {forward}, maxSpeed: {0-maxSpeed}");
                        } else {
                            Log._DebugIf(logLogic, () => $"AircraftAI.SimulationStep({vehicleID}): NotFlying2 NOT(vector.z < (0f - Mathf.Abs(vector.x)) * 20f) vectorBefore: {vector}, fwd: {forward}");

                            if (vector.x >= 0f) {
                                vector.x = Mathf.Max(1f, vector.x);
                            } else {
                                vector.x = Mathf.Min(-1f, vector.x);
                            }

                            vector.z = Mathf.Abs(vector.x) * 2f;
                            forward = vector.normalized;
                            maxSpeed = 0f;
                            Log._DebugIf(logLogic, () => $"AircraftAI.SimulationStep({vehicleID}): NotFlying2 !(vector.z < (0f - Mathf.Abs(vector.x)) * 20f) vectorAfter: {vector}, fwd: {forward} maxSpeed: 0f");
                        }
                    }
                } else if (Mathf.Abs(forward.y) > 0.1f) {
                    forward.y = Mathf.Clamp(forward.y, -0.1f, 0.1f);
                    Vector3 fwdBefore = forward;
                    forward.Normalize();

                    Log._DebugIf(logLogic, () => $"AircraftAI.SimulationStep({vehicleID}): Clamp forward: {fwdBefore}, normalized: {forward}");
                }
                Log._DebugIf(logLogic, () => $"AircraftAI.SimulationStep({vehicleID}): Forward2: {forward}, maxSpeed: {maxSpeed}, curSpeed: {curSpeed}, vector: {vector}");

                if (maxSpeed < curSpeed) {
                    float brake = Mathf.Max(acceleration, Mathf.Min(braking, curSpeed));
                    targetSpeed = Mathf.Max(maxSpeed, curSpeed - brake);
                } else {
                    float accel = Mathf.Max(acceleration, Mathf.Min(braking, 0f - curSpeed));
                    targetSpeed = Mathf.Min(maxSpeed, curSpeed + accel);
                }

                if ((leaderData.m_flags & Vehicle.Flags.Stopped) == 0 && Mathf.Abs(maxSpeed) < 0.1f) {
                    blocked = true;
                }
                Log._DebugIf(logLogic, () => $"AircraftAI.SimulationStep({vehicleID}): TargetSpeed: {targetSpeed} currSpeed: {curSpeed}, maxSpeed: {maxSpeed}, blocked: {blocked}");

            } else if (Mathf.Abs(curSpeed) < 0.1f && onlyFirstTargetPosUpdated &&
                       ArriveAtDestination(__instance, leaderID, ref leaderData)) {
                Log._DebugIf(logLogic, () => $"AircraftAI.SimulationStep({vehicleID}): curSpeed < 1.0f && ArriveAtDestination. Unspawning...");

                leaderData.Unspawn(leaderID);
                return false;
            }

            if (blocked) {
                leaderData.m_blockCounter = (byte)Mathf.Min(leaderData.m_blockCounter + 1, 255);

                // NON-STOCK CODE START
                if (CanSearchForStand(leaderData.m_blockCounter) &&
                    leaderData.Info.m_vehicleAI is CargoPlaneAI &&
                    (ExtVehicleManager.Instance.ExtVehicles[vehicleID].flags & ExtVehicleFlags.GoingFromOutside) != 0) {
                    // check if vehicle is in the same aiport area as its target aircraft stand
                    byte currectParkId = DistrictManager.instance.GetPark(vehicleData.GetLastFramePosition());
                    if (currectParkId != 0 &&
                        currectParkId == DistrictManager.instance.GetPark(vehicleData.m_targetBuilding.ToBuilding().m_position)) {
                        // search for better stand
                        ushort buildingID = AirplaneStandSearch.FindBetterCargoPlaneStand(ref extVehicle, ref leaderData);
                        if (buildingID != 0 && leaderData.m_targetBuilding != buildingID) {
                            if (leaderData.m_sourceBuilding == leaderData.m_targetBuilding) {
                                // returning to source building
                                BuildingManager.instance.m_buildings.m_buffer[leaderData.m_sourceBuilding]
                                               .RemoveOwnVehicle(vehicleID, ref leaderData);
                                BuildingManager.instance.m_buildings.m_buffer[buildingID]
                                               .AddOwnVehicle(vehicleID, ref leaderData);
                                leaderData.m_sourceBuilding = buildingID;
                            }

                            leaderData.Info.m_vehicleAI.SetTarget(vehicleID, ref leaderData, buildingID);
                            extVehicle.requiresCargoPathRecalculation = true;
                        }
                    }
                }
                // NON-STOCK CODE END
            } else {
                leaderData.m_blockCounter = 0;
            }

            if (forwardLen > 1f) {
                steerAngle = Mathf.Asin(forward.x) * Mathf.Sign(targetSpeed);
                targetMotion = forward * targetSpeed;
            } else {
                targetSpeed = 0f;
                Vector3 vel = Vector3.ClampMagnitude(vector * 0.5f - curveTangent, braking);
                targetMotion = curveTangent + vel;
            }
            Log._DebugIf(logLogic, () => $"AircraftAI.SimulationStep({vehicleID}): Forward: {forward} FwdLen: {forwardLen}, targMot: {targetMotion}, tgtSpeed: {targetSpeed}, steerAngle: {steerAngle}");

            // forward = frameData.m_rotation * forward;
            forward = extVehicle.airplaneRotation.Value * forward;
            // frameData.m_velocity = frameData.m_rotation * targetMotion;
            frameData.m_velocity = extVehicle.airplaneRotation.Value * targetMotion;
            frameData.m_position += frameData.m_velocity * 0.5f;
            if (logLogic) {
                Log._Debug($"AircraftAI.SimulationStep({vehicleID}): Finishing... f_vel: {frameData.m_velocity}, f_velMag: {frameData.m_velocity.magnitude}, f_pos: {frameData.m_position}");
            }

            if ((leaderData.m_flags & Vehicle.Flags.Flying) != 0) {
                Vector3 a3 = default(Vector3);
                a3.x = (curveTangent.x - targetMotion.x) * 0.5f;
                a3.y = 0f;
                a3.z = Mathf.Max(0f, forward.y * -10f);
                frameData.m_swayVelocity = frameData.m_swayVelocity * 0.5f - a3 * 0.5f -
                                           frameData.m_swayPosition * 0.5f;
                frameData.m_swayPosition += frameData.m_swayVelocity * 0.5f;
                frameData.m_steerAngle = 0f;
            } else {
                //on the ground
                Vector3 springs = targetMotion - curveTangent;
                frameData.m_swayVelocity = frameData.m_swayVelocity * (1f - ___m_info.m_dampers) -
                                           springs * (1f - ___m_info.m_springs) -
                                           frameData.m_swayPosition * ___m_info.m_springs;
                frameData.m_swayPosition += frameData.m_swayVelocity * 0.5f;
                frameData.m_steerAngle = steerAngle;
                frameData.m_travelDistance += targetMotion.z;
            }

            frameData.m_lightIntensity.x = 5f;
            frameData.m_lightIntensity.y = 5f;
            frameData.m_lightIntensity.z = 5f;
            frameData.m_lightIntensity.w = 5f;
            if (forwardLen > 1f) {
                Quaternion q = Quaternion.LookRotation(forward);
                //frameData.m_rotation = Quaternion.LookRotation(forward);
                // NON-STOCK CODE
                extVehicle.airplaneRotation = q;
                if (vehicleData.m_targetPos0.w < 40f &&
                    !leaderData.m_flags.IsFlagSet(Vehicle.Flags.TakingOff) &&
                    (leaderData.m_flags & (Vehicle.Flags.Flying | Vehicle.Flags.Landing)) != 0) {

                    // TODO IMPROVE!!!
                    float height = frameData.m_position.y - TerrainManager.instance.SampleDetailHeightSmooth(frameData.m_position);
                    float angle = 0;
                    if (height > 100) {
                        angle = Angle100m;
                    } else if (height > 50) {
                        angle = Angle50m;
                    } else if (height > 25) {
                        angle = Angle25m;
                    } else if (height > 10) {
                        angle = Angle10m;
                    } else if (height > 0.50) {
                        angle = Angle05m;
                    } else if (height > 0.33) {
                        angle = Angle03m;
                    } else if (height >= 0.25) {
                        angle = Angle01m;
                    } else {
                        angle = Mathf.Lerp(0.0005f, 0.02f, height / 0.25f);
                    }

                    Vector3 v = new Vector3(forward.x, forward.y + angle, forward.z);
                    frameData.m_rotation = Quaternion.LookRotation(v);
                    // NON-STOCK CODE END
                } else {
                    // STOCK CODE
                    frameData.m_rotation = Quaternion.LookRotation(forward);
                    // ---
                }
            }

            // vehicleai, virtual no impl.
            //SimulationStep(vehicleID, ref vehicleData, ref frameData, leaderID, ref leaderData, lodPhysics);

            return false;
        }

        private static bool CanSearchForStand(byte blockCounter) {
            return blockCounter == 30 ||
                   blockCounter == 60 ||
                   blockCounter == 100 ||
                   blockCounter == 160 ||
                   blockCounter == 240;
        }
    }
}