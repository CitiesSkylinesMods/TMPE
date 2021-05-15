namespace TrafficManager.Patch._VehicleAI._CarAI {
    using System.Reflection;
    using API.Traffic.Enums;
    using ColossalFramework;
    using CSUtil.Commons;
    using HarmonyLib;
    using JetBrains.Annotations;
    using Manager.Impl;
    using State;
    using State.ConfigData;
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
        public static MethodBase TargetMethod() => TranspilerUtil.DeclaredMethod<CalculatePositionDelegate>( typeof(CarAI), "CalculateSegmentPosition");

        [UsedImplicitly]
        public static bool Prefix(CarAI __instance,
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
            ushort nextSourceNodeId;
            ushort nextTargetNodeId;
            NetSegment[] segmentsBuffer = netManager.m_segments.m_buffer;

            if (offset < position.m_offset) {
                nextSourceNodeId = segmentsBuffer[position.m_segment].m_startNode;
                nextTargetNodeId = segmentsBuffer[position.m_segment].m_endNode;
            } else {
                nextSourceNodeId = segmentsBuffer[position.m_segment].m_endNode;
                nextTargetNodeId = segmentsBuffer[position.m_segment].m_startNode;
            }

            ushort curTargetNodeId;
            curTargetNodeId = prevOffset == 0
                                  ? segmentsBuffer[prevPos.m_segment].m_startNode
                                  : segmentsBuffer[prevPos.m_segment].m_endNode;

#if DEBUG
            bool logCalculation = DebugSwitch.CalculateSegmentPosition.Get()
                        && (DebugSettings.NodeId <= 0
                            || curTargetNodeId == DebugSettings.NodeId)
                        && (GlobalConfig.Instance.Debug.ApiExtVehicleType == ExtVehicleType.None
                            || GlobalConfig.Instance.Debug.ApiExtVehicleType == ExtVehicleType.RoadVehicle)
                        && (DebugSettings.VehicleId == 0
                            || DebugSettings.VehicleId == vehicleID);

            if (logCalculation) {
                Log._Debug($"CustomCarAI.CustomCalculateSegmentPosition({vehicleID}) called.\n" +
                           $"\tcurPosition.m_segment={prevPos.m_segment}, " +
                           $"curPosition.m_offset={prevPos.m_offset}\n" +
                           $"\tposition.m_segment={position.m_segment}, " +
                           $"position.m_offset={position.m_offset}\n" +
                           $"\tnextNextPosition.m_segment={nextPosition.m_segment}, " +
                           $"nextNextPosition.m_offset={nextPosition.m_offset}\n" +
                           $"\tcurLaneId={prevLaneID}, prevOffset={prevOffset}\n" +
                           $"\tnextLaneId={laneID}, nextOffset={offset}\n" +
                           $"\tnextSourceNodeId={nextSourceNodeId}, nextTargetNodeId={nextTargetNodeId}\n" +
                           $"\tcurTargetNodeId={curTargetNodeId}, curTargetNodeId={curTargetNodeId}\n" +
                           $"\tindex={index}");
            }
#endif

            Vehicle.Frame lastFrameData = vehicleData.GetLastFrameData();
            Vector3 lastFrameVehiclePos = lastFrameData.m_position;
            float sqrVelocity = lastFrameData.m_velocity.sqrMagnitude;
            netManager.m_lanes.m_buffer[laneID].CalculatePositionAndDirection(
                Constants.ByteToFloat(offset),
                out pos,
                out dir);

            float braking = __instance.m_info.m_braking;
            if ((vehicleData.m_flags & Vehicle.Flags.Emergency2) != 0) {
                braking *= 2f;
            }

            // car position on the Bezier curve of the lane
            Vector3 refVehiclePosOnBezier = netManager.m_lanes.m_buffer[prevLaneID]
                                                      .CalculatePosition(
                                                          Constants.ByteToFloat(prevOffset));

            // ushort currentSegmentId = netManager.m_lanes.m_buffer[prevLaneID].m_segment;
            // this seems to be like the required braking force in order to stop the vehicle within its half length.
            float crazyValue = (0.5f * sqrVelocity / braking) +
                               (__instance.m_info.m_generatedInfo.m_size.z * 0.5f);
            float d = Vector3.Distance(lastFrameVehiclePos, refVehiclePosOnBezier);
            bool withinBrakingDistance = d >= crazyValue - 1f;

            if (nextSourceNodeId == curTargetNodeId
                && withinBrakingDistance) {
                // NON-STOCK CODE START (stock code replaced)
                if (!VehicleBehaviorManager.Instance.MayChangeSegment(
                        vehicleID,
                        ref vehicleData,
                        sqrVelocity,
                        ref prevPos,
                        ref segmentsBuffer[prevPos.m_segment],
                        curTargetNodeId,
                        prevLaneID,
                        ref position,
                        nextSourceNodeId,
                        ref netManager.m_nodes.m_buffer[nextSourceNodeId],
                        laneID,
                        ref nextPosition,
                        nextTargetNodeId,
                        out maxSpeed)) {
                    // NON-STOCK CODE
                    return false;
                }

                ExtVehicleManager.Instance.UpdateVehiclePosition(
                    vehicleID,
                    ref vehicleData /*, lastFrameData.m_velocity.magnitude*/);

                // NON-STOCK CODE END
            }

            NetInfo prevSegmentInfo = segmentsBuffer[position.m_segment].Info;
            // NON-STOCK CODE START (stock code replaced)
            VehicleAICommons.CustomCalculateTargetSpeed(
                __instance,
                vehicleID,
                ref vehicleData,
                position,
                laneID,
                prevSegmentInfo,
                out maxSpeed);

            maxSpeed = Constants.ManagerFactory.VehicleBehaviorManager.CalcMaxSpeed(
                vehicleID,
                ref Constants.ManagerFactory.ExtVehicleManager.ExtVehicles[vehicleID],
                __instance.m_info,
                position,
                ref segmentsBuffer[position.m_segment],
                pos,
                maxSpeed,
                false);

            // NON-STOCK CODE END
            return false;
        }
    }
}