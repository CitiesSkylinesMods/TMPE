/// <summary>
/// Patching RTramBaseAI from Reversible Tram AI mod
/// https://steamcommunity.com/sharedfiles/filedetails/?id=2740907672
/// https://github.com/sway2020/ReversibleTramAI
/// </summary>

namespace TrafficManager.Patch._VehicleAI._RTramBaseAI {
    using System;
    using ColossalFramework;
    using HarmonyLib;
    using JetBrains.Annotations;
    using Manager.Impl;
    using State;
    using TrafficManager.Util.Extensions;
    using UnityEngine;
    using Util;

    [UsedImplicitly]
    public class CalculateSegmentPositionPatch2 {
        private delegate void CalculatePositionDelegate(ushort vehicleID,
                                                        ref Vehicle vehicleData,
                                                        PathUnit.Position nextPosition,
                                                        PathUnit.Position position,
                                                        uint laneID,
                                                        byte offset,
                                                        PathUnit.Position prevPos,
                                                        uint prevLaneID,
                                                        byte prevOffset,
                                                        int index,
                                                        out Vector3 pos,
                                                        out Vector3 dir,
                                                        out float maxSpeed);

        public static bool ApplyPatch(Harmony harmonyInstance, Type rTramBaseAIType) {
            try {
                var original = TranspilerUtil.DeclaredMethod<CalculatePositionDelegate>(rTramBaseAIType, "CalculateSegmentPosition");
                if (original == null) return false;

                var prefix = typeof(CalculateSegmentPositionPatch2).GetMethod("Prefix");
                harmonyInstance.Patch(original, new HarmonyMethod(prefix));
                return true;
            }
            catch (Exception ex) {
                ex.LogException();
                return false;
            }
        }

        [UsedImplicitly]
        public static bool Prefix(TrolleybusAI __instance,
                                  ushort vehicleID,
                                  ref Vehicle vehicleData,
                                  PathUnit.Position nextPosition,
                                  PathUnit.Position position,
                                  uint laneID,
                                  byte offset,
                                  PathUnit.Position prevPos,
                                  uint prevLaneID,
                                  byte prevOffset,
                                  int index,
                                  out Vector3 pos,
                                  out Vector3 dir,
                                  out float maxSpeed) {
            NetManager netManager = Singleton<NetManager>.instance;
            ushort prevSourceNodeId;
            ushort prevTargetNodeId;
            ref NetSegment currentPositionSegment = ref position.m_segment.ToSegment();

            if (offset < position.m_offset) {
                prevSourceNodeId = currentPositionSegment.m_startNode;
                prevTargetNodeId = currentPositionSegment.m_endNode;
            } else {
                prevSourceNodeId = currentPositionSegment.m_endNode;
                prevTargetNodeId = currentPositionSegment.m_startNode;
            }

            ref NetSegment previousPositionSegment = ref prevPos.m_segment.ToSegment();
            ushort refTargetNodeId = prevOffset == 0
                ? previousPositionSegment.m_startNode
                : previousPositionSegment.m_endNode;
            Vehicle.Frame lastFrameData = vehicleData.GetLastFrameData();
            float sqrVelocity = lastFrameData.m_velocity.sqrMagnitude;

            netManager.m_lanes.m_buffer[laneID].CalculatePositionAndDirection(
                Constants.ByteToFloat(offset),
                out pos,
                out dir);
            Vector3 b = netManager.m_lanes.m_buffer[prevLaneID].CalculatePosition(
                Constants.ByteToFloat(prevOffset));
            Vector3 a = lastFrameData.m_position;
            Vector3 a2 = lastFrameData.m_position;
            Vector3 b2 = lastFrameData.m_rotation * new Vector3(
                             0f,
                             0f,
                             __instance.m_info.m_generatedInfo.m_wheelBase * 0.5f);
            a += b2;
            a2 -= b2;
            float crazyValue = 0.5f * sqrVelocity / (__instance.m_info.m_braking * 0.3f);  // 0.3 works better for the reversible tram ai for some reason
            float a3 = Vector3.Distance(a, b);
            float b3 = Vector3.Distance(a2, b);

            if (Mathf.Min(a3, b3) >= crazyValue - 1f) {
                if (prevSourceNodeId == refTargetNodeId) {
                    if (!VehicleBehaviorManager.Instance.MayChangeSegment(
                            vehicleID,
                            ref vehicleData,
                            sqrVelocity,
                            ref prevPos,
                            ref previousPositionSegment,
                            refTargetNodeId,
                            prevLaneID,
                            ref position,
                            prevSourceNodeId,
                            ref prevSourceNodeId.ToNode(),
                            laneID,
                            ref nextPosition,
                            prevTargetNodeId,
                            out maxSpeed)) {
                        maxSpeed = 0;
                        return false;
                    }

                    ExtVehicleManager.Instance.UpdateVehiclePosition(
                            vehicleID,
                            ref vehicleData);
                }
            }

            VehicleAICommons.CustomCalculateTargetSpeed(
                __instance,
                vehicleID,
                ref vehicleData,
                position,
                laneID,
                currentPositionSegment.Info,
                out maxSpeed);
            // NON-STOCK CODE END
            return false;
        }
    }
}