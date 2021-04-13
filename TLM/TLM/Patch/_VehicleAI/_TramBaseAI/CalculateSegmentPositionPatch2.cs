namespace TrafficManager.Patch._VehicleAI._TramBaseAI {
    using System.Reflection;
    using ColossalFramework;
    using HarmonyLib;
    using JetBrains.Annotations;
    using Manager.Impl;
    using State;
    using UnityEngine;
    using Util;

    [UsedImplicitly]
    [HarmonyPatch]
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

        [UsedImplicitly]
        public static MethodBase TargetMethod() => TranspilerUtil.DeclaredMethod<CalculatePositionDelegate>(typeof(TramBaseAI), "CalculateSegmentPosition");

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
            NetSegment[] segBuffer = netManager.m_segments.m_buffer;

            if (offset < position.m_offset) {
                prevSourceNodeId = segBuffer[position.m_segment].m_startNode;
                prevTargetNodeId = segBuffer[position.m_segment].m_endNode;
            } else {
                prevSourceNodeId = segBuffer[position.m_segment].m_endNode;
                prevTargetNodeId = segBuffer[position.m_segment].m_startNode;
            }

            ushort refTargetNodeId = prevOffset == 0
                                         ? segBuffer[prevPos.m_segment].m_startNode
                                         : segBuffer[prevPos.m_segment].m_endNode;
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
            float crazyValue = 0.5f * sqrVelocity / __instance.m_info.m_braking;
            float a3 = Vector3.Distance(a, b);
            float b3 = Vector3.Distance(a2, b);

            if (Mathf.Min(a3, b3) >= crazyValue - 1f) {
                if (prevSourceNodeId == refTargetNodeId) {
                    if (!VehicleBehaviorManager.Instance.MayChangeSegment(
                            vehicleID,
                            ref vehicleData,
                            sqrVelocity,
                            ref prevPos,
                            ref segBuffer[prevPos.m_segment],
                            refTargetNodeId,
                            prevLaneID,
                            ref position,
                            prevSourceNodeId,
                            ref netManager.m_nodes.m_buffer[
                                prevSourceNodeId],
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

            NetInfo info = segBuffer[position.m_segment].Info;
            VehicleAICommons.CustomCalculateTargetSpeed(
                __instance,
                vehicleID,
                ref vehicleData,
                position,
                laneID,
                info,
                out maxSpeed);
            // NON-STOCK CODE END
            return false;
        }
    }
}