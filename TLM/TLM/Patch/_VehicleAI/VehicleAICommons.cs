namespace TrafficManager.Patch._VehicleAI {
    using System.Collections.Generic;
    using System.Reflection;
    using System.Reflection.Emit;
    using Connection;
    using HarmonyLib;
    using Manager.Impl;
    using State;
    using TrafficManager.Util;
    using TrafficManager.Util.Extensions;
    using UnityEngine;
    using UnityEngine.UI;

    public class VehicleAICommons {
        private static readonly float vanillaSlowDrivingSpeed = 0.4f;

        private static CalculateTargetSpeedDelegate CalculateTargetSpeed = GameConnectionManager.Instance.VehicleAIConnection.CalculateTargetSpeed;
        private static CalculateTargetSpeedByNetInfoDelegate CalculateTargetSpeedByNetInfo = GameConnectionManager.Instance.VehicleAIConnection.CalculateTargetSpeedByNetInfo;
        public static void CustomCalculateSegmentPosition(VehicleAI instance,
                                                   ushort vehicleId,
                                                   ref Vehicle vehicleData,
                                                   PathUnit.Position position,
                                                   uint laneId,
                                                   byte offset,
                                                   out Vector3 pos,
                                                   out Vector3 dir,
                                                   out float maxSpeed) {
            laneId.ToLane().CalculatePositionAndDirection(Constants.ByteToFloat(offset), out pos, out dir);
            CustomCalculateTargetSpeed(
                instance,
                vehicleId,
                ref vehicleData,
                position,
                laneId,
                position.m_segment.ToSegment().Info,
                out maxSpeed);
        }

        /// <summary>
        /// Calculate segment position for other vehicles like Tram, Train that are excluded from 'Slow Driving' district policy
        /// </summary>
        public static void CustomCalculateSegmentPosition_NoSlowDriving(VehicleAI instance,
                                                          ushort vehicleId,
                                                          ref Vehicle vehicleData,
                                                          PathUnit.Position position,
                                                          uint laneId,
                                                          byte offset,
                                                          out Vector3 pos,
                                                          out Vector3 dir,
                                                          out float maxSpeed) {
            laneId.ToLane().CalculatePositionAndDirection(Constants.ByteToFloat(offset), out pos, out dir);
            CustomCalculateTargetSpeed_NoSlowDriving(
                instance,
                vehicleId,
                ref vehicleData,
                position,
                laneId,
                position.m_segment.ToSegment().Info,
                out maxSpeed);
        }

        /// <summary>
        /// Calculate target speed for other vehicles like Tram, Train that are excluded from 'Slow Driving' district policy
        /// </summary>
        public static void CustomCalculateTargetSpeed_NoSlowDriving(VehicleAI instance,
                                                      ushort vehicleId,
                                                      ref Vehicle vehicleData,
                                                      PathUnit.Position position,
                                                      uint laneId,
                                                      NetInfo info,
                                                      out float maxSpeed) {
            if (info.m_lanes != null && info.m_lanes.Length > position.m_lane) {
                float laneSpeedLimit = SavedGameOptions.Instance.customSpeedLimitsEnabled
                                           ? SpeedLimitManager.Instance.GetGameSpeedLimit(
                                               position.m_segment,
                                               position.m_lane,
                                               laneId,
                                               info.m_lanes[position.m_lane])
                                           : info.m_lanes[position.m_lane].m_speedLimit; // NON-STOCK CODE
                maxSpeed = CalculateTargetSpeed(
                    instance,
                    vehicleId,
                    ref vehicleData,
                    laneSpeedLimit,
                    laneId.ToLane().m_curve);
            } else {
                maxSpeed = CalculateTargetSpeed(instance, vehicleId, ref vehicleData, 1f, 0f);
            }
        }

        public static void CustomCalculateTargetSpeed(VehicleAI instance,
                                                      ushort vehicleId,
                                                      ref Vehicle vehicleData,
                                                      PathUnit.Position position,
                                                      uint laneId,
                                                      NetInfo info,
                                                      out float maxSpeed) {
            if (info.m_lanes != null && info.m_lanes.Length > position.m_lane) {
                if (SavedGameOptions.Instance.customSpeedLimitsEnabled) {
                    float laneSpeedLimit = SpeedLimitManager.Instance.GetGameSpeedLimit(
                                                   position.m_segment,
                                                   position.m_lane,
                                                   laneId,
                                                   info.m_lanes[position.m_lane]);
                    if (laneSpeedLimit > vanillaSlowDrivingSpeed &&
                        (instance.vehicleCategory & VehicleInfo.VehicleCategory.RoadTransport) != VehicleInfo.VehicleCategory.None &&
                        !info.m_netAI.IsHighway() && !info.m_netAI.IsTunnel() && !info.IsPedestrianZoneOrPublicTransportRoad()) {

                        // STOCK CODE
                        Vector3 lastFramePosition = vehicleData.GetLastFramePosition();
                        DistrictManager districtManager = DistrictManager.instance;
                        byte park = districtManager.GetPark(lastFramePosition);
                        if (park != 0 && (districtManager.m_parks.m_buffer[park].m_parkPolicies & DistrictPolicies.Park.SlowDriving) != DistrictPolicies.Park.None) {
                            maxSpeed = vanillaSlowDrivingSpeed;
                        } else {
                            maxSpeed = CalculateTargetSpeed(
                                instance,
                                vehicleId,
                                ref vehicleData,
                                laneSpeedLimit,
                                laneId.ToLane().m_curve);
                        }
                    } else {
                        maxSpeed = CalculateTargetSpeed(
                            instance,
                            vehicleId,
                            ref vehicleData,
                            laneSpeedLimit,
                            laneId.ToLane().m_curve);
                    }
                } else {
                    // STOCK CODE
                    maxSpeed = CalculateTargetSpeedByNetInfo(
                        instance,
                        vehicleId,
                        ref vehicleData,
                        info,
                        position.m_lane,
                        laneId.ToLane().m_curve);
                }
            } else {
                // STOCK CODE
                maxSpeed = CalculateTargetSpeed(instance, vehicleId, ref vehicleData, 1f, 0f);
            }
        }

        /// <summary>
        /// Common transpiler for calling patched version of CalculateSegmentPosition
        /// </summary>
        public static IEnumerable<CodeInstruction> TranspileCalculateSegmentPosition(
                IEnumerable<CodeInstruction> instructions,
                bool noSlowDriving = false,
                bool extendedDefinition = false)
        {
            MethodBase patchMethod = typeof(VehicleAICommons).GetMethod(
                noSlowDriving
                    ? nameof(VehicleAICommons.CustomCalculateSegmentPosition_NoSlowDriving)
                    : nameof(VehicleAICommons.CustomCalculateSegmentPosition)
                , BindingFlags.Static | BindingFlags.Public);

            if (extendedDefinition) {
                return new CodeInstruction[] {
                    new CodeInstruction(OpCodes.Ldarg_0),     // AI instance
                    new CodeInstruction(OpCodes.Ldarg_1),     // vehicleID
                    new CodeInstruction(OpCodes.Ldarg_2),     // ref vehicle data
                    new CodeInstruction(OpCodes.Ldarg_S, 4),  // pathUnit position
                    new CodeInstruction(OpCodes.Ldarg_S, 5),  // laneID
                    new CodeInstruction(OpCodes.Ldarg_S, 6),  // offset
                    new CodeInstruction(OpCodes.Ldarg_S, 11), // out Vector3 position
                    new CodeInstruction(OpCodes.Ldarg_S, 12), // out Vector3 direction
                    new CodeInstruction(OpCodes.Ldarg_S, 13), // out maxSpeed
                    new CodeInstruction(OpCodes.Call, patchMethod),
                    new CodeInstruction(OpCodes.Ret)
                };
            } else {
                return new CodeInstruction[] {
                    new CodeInstruction(OpCodes.Ldarg_0),    // AI instance
                    new CodeInstruction(OpCodes.Ldarg_1),    // vehicleID
                    new CodeInstruction(OpCodes.Ldarg_2),    // ref vehicle data
                    new CodeInstruction(OpCodes.Ldarg_3),    // pathUnit position
                    new CodeInstruction(OpCodes.Ldarg_S, 4), // laneID
                    new CodeInstruction(OpCodes.Ldarg_S, 5), // offset
                    new CodeInstruction(OpCodes.Ldarg_S, 6), // out Vector3 position
                    new CodeInstruction(OpCodes.Ldarg_S, 7), // out Vector3 direction
                    new CodeInstruction(OpCodes.Ldarg_S, 8), // out maxSpeed
                    new CodeInstruction(OpCodes.Call, patchMethod),
                    new CodeInstruction(OpCodes.Ret)
                };
            }
        }
    }
}