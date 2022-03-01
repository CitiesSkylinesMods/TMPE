namespace TrafficManager.Patch._VehicleAI._TrainAI {
    using System.Reflection;
    using API.Traffic.Enums;
    using ColossalFramework.Math;
    using Connection;
    using CSUtil.Commons;
    using HarmonyLib;
    using JetBrains.Annotations;
    using Manager.Impl;
    using State;
    using State.ConfigData;
    using TrafficManager.Util.Extensions;
    using UnityEngine;
    using Util;

    [UsedImplicitly]
    [HarmonyPatch]
    public class CheckNextLanePatch {
        private delegate void TargetDelegate(ushort vehicleID,
                                             ref Vehicle vehicleData,
                                             ref float maxSpeed,
                                             PathUnit.Position position,
                                             uint laneID,
                                             byte offset,
                                             PathUnit.Position prevPos,
                                             uint prevLaneID,
                                             byte prevOffset,
                                             Bezier3 bezier);

        [UsedImplicitly]
        public static MethodBase TargetMethod() => TranspilerUtil.DeclaredMethod<TargetDelegate>(typeof(TrainAI), "CheckNextLane");

        private static CheckOverlapDelegate CheckOverlap;

        [UsedImplicitly]
        public static void Prepare() {
            CheckOverlap = GameConnectionManager.Instance.TrainAIConnection.CheckOverlap;
        }

        [UsedImplicitly]
        public static bool Prefix(TrainAI __instance,
                                  ushort vehicleID,
                                  ref Vehicle vehicleData,
                                  ref float maxSpeed,
                                  PathUnit.Position position,
                                  uint laneID,
                                  byte offset,
                                  PathUnit.Position prevPos,
                                  uint prevLaneID,
                                  byte prevOffset,
                                  Bezier3 bezier) {
            NetManager netManager = NetManager.instance;

            ref NetSegment currentPositionSegment = ref position.m_segment.ToSegment();

            ushort nextSourceNodeId = offset < position.m_offset
                ? currentPositionSegment.m_startNode
                : currentPositionSegment.m_endNode;

            ref NetSegment previousPositionSegment = ref prevPos.m_segment.ToSegment();

            ushort refTargetNodeId = prevOffset == 0
                ? previousPositionSegment.m_startNode
                : previousPositionSegment.m_endNode;

#if DEBUG
            bool logLogic = DebugSwitch.CalculateSegmentPosition.Get()
                           && (DebugSettings.NodeId <= 0
                               || refTargetNodeId == DebugSettings.NodeId)
                           && (GlobalConfig.Instance.Debug.ApiExtVehicleType == ExtVehicleType.None
                               || GlobalConfig.Instance.Debug.ApiExtVehicleType == ExtVehicleType.RailVehicle)
                           && (DebugSettings.VehicleId == 0
                               || DebugSettings.VehicleId == vehicleID);

            if (logLogic) {
                Log._Debug($"CustomTrainAI.CustomCheckNextLane({vehicleID}) called.\n" +
                           $"\tprevPos.m_segment={prevPos.m_segment}, " +
                           $"prevPos.m_offset={prevPos.m_offset}\n" +
                           $"\tposition.m_segment={position.m_segment}, " +
                           $"position.m_offset={position.m_offset}\n" +
                           $"\tprevLaneID={prevLaneID}, prevOffset={prevOffset}\n" +
                           $"\tprevLaneId={laneID}, prevOffset={offset}\n" +
                           $"\tnextSourceNodeId={nextSourceNodeId}\n" +
                           $"\trefTargetNodeId={refTargetNodeId}, " +
                           $"refTargetNodeId={refTargetNodeId}");
            }
#endif

            Vehicle.Frame lastFrameData = vehicleData.GetLastFrameData();

            Vector3 lastPosPlusRot = lastFrameData.m_position;
            Vector3 lastPosMinusRot = lastFrameData.m_position;
            Vector3 rotationAdd = lastFrameData.m_rotation
                              * new Vector3(0f, 0f, __instance.m_info.m_generatedInfo.m_wheelBase * 0.5f);
            lastPosPlusRot += rotationAdd;
            lastPosMinusRot -= rotationAdd;
            float breakingDist = 0.5f * lastFrameData.m_velocity.sqrMagnitude / __instance.m_info.m_braking;
            float distToTargetAfterRot = Vector3.Distance(lastPosPlusRot, bezier.a);
            float distToTargetBeforeRot = Vector3.Distance(lastPosMinusRot, bezier.a);

#if DEBUG
            if (logLogic) {
                Log._Debug($"CustomTrainAI.CustomCheckNextLane({vehicleID}): " +
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
#if DEBUG
                if (logLogic) {
                    Log._Debug($"CustomTrainAI.CustomCheckNextLane({vehicleID}): " +
                               $"Checking for free space on lane {laneID}.");
                }
#endif

                if (!laneID.ToLane().CheckSpace(1000f, vehicleID)) {
#if DEBUG
                    if (logLogic) {
                        Log._Debug($"CustomTrainAI.CustomCheckNextLane({vehicleID}): " +
                                   $"No space available on lane {laneID}. ABORT.");
                    }
#endif
                    vehicleData.m_flags2 |= Vehicle.Flags2.Yielding;
                    vehicleData.m_waitCounter = 0;
                    maxSpeed = 0f;
                    return false;
                }

                Vector3 bezierMiddlePoint = bezier.Position(0.5f);

                Segment3 segment = Vector3.SqrMagnitude(vehicleData.m_segment.a - bezierMiddlePoint) <
                              Vector3.SqrMagnitude(bezier.a - bezierMiddlePoint)
                                  ? new Segment3(vehicleData.m_segment.a, bezierMiddlePoint)
                                  : new Segment3(bezier.a, bezierMiddlePoint);

#if DEBUG
                if (logLogic) {
                    Log._Debug($"CustomTrainAI.CustomCheckNextLane({vehicleID}): " +
                               $"Checking for overlap (1). segment.a={segment.a} segment.b={segment.b}");
                }
#endif
                if (segment.LengthSqr() >= 3f) {
                    segment.a += (segment.b - segment.a).normalized * 2.5f;

                    if (CheckOverlap(vehicleID, ref vehicleData, segment, vehicleID)) {
#if DEBUG
                        if (logLogic) {
                            Log._Debug($"CustomTrainAI.CustomCheckNextLane({vehicleID}): " +
                                       $"Overlap detected (1). segment.LengthSqr()={segment.LengthSqr()} " +
                                       $"segment.a={segment.a} ABORT.");
                        }
#endif
                        vehicleData.m_flags2 |= Vehicle.Flags2.Yielding;
                        vehicleData.m_waitCounter = 0;
                        maxSpeed = 0f;
                        return false;
                    }
                }

                segment = new Segment3(bezierMiddlePoint, bezier.d);
#if DEBUG
                if (logLogic) {
                    Log._Debug($"CustomTrainAI.CustomCheckNextLane({vehicleID}): " +
                               $"Checking for overlap (2). segment.a={segment.a} segment.b={segment.b}");
                }
#endif
                if (segment.LengthSqr() >= 1f
                    && CheckOverlap(vehicleID, ref vehicleData, segment, vehicleID)) {
#if DEBUG
                    if (logLogic) {
                        Log._Debug($"CustomTrainAI.CustomCheckNextLane({vehicleID}): " +
                                   $"Overlap detected (2). ABORT.");
                    }
#endif
                    vehicleData.m_flags2 |= Vehicle.Flags2.Yielding;
                    vehicleData.m_waitCounter = 0;
                    maxSpeed = 0f;
                    return false;
                }

                // } // NON-STOCK CODE
                // if (this.m_info.m_vehicleType != VehicleInfo.VehicleType.Monorail) { // NON-STOCK CODE
                if (nextSourceNodeId != refTargetNodeId) {
                    return false;
                }
#if DEBUG
                if (logLogic) {
                    Log._Debug($"CustomTrainAI.CustomCheckNextLane({vehicleID}): " +
                               $"Checking if vehicle is allowed to change segment.");
                }
#endif
                float oldMaxSpeed = maxSpeed;
                if (!VehicleBehaviorManager.Instance.MayChangeSegment(
                        vehicleID,
                        ref vehicleData,
                        lastFrameData.m_velocity.sqrMagnitude,
                        ref prevPos,
                        ref previousPositionSegment,
                        refTargetNodeId,
                        prevLaneID,
                        ref position,
                        refTargetNodeId,
                        ref refTargetNodeId.ToNode(),
                        laneID,
                        out maxSpeed)) {
#if DEBUG
                    if (logLogic) {
                        Log._Debug($"CustomTrainAI.CustomCheckNextLane({vehicleID}): " +
                                   $"Vehicle is NOT allowed to change segment. ABORT.");
                    }
#endif
                    maxSpeed = 0;
                    return false;
                }

                ExtVehicleManager.Instance.UpdateVehiclePosition(
                        vehicleID,
                        ref vehicleData);
                maxSpeed = oldMaxSpeed;

                // NON-STOCK CODE
            }
            return false;
        }
    }
}