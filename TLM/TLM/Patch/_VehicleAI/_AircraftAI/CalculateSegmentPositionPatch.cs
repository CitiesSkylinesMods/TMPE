namespace TrafficManager.Patch._VehicleAI._AircraftAI {
    using System.Reflection;
    using _VehicleAI.Connection;
    using ColossalFramework.Math;
    using HarmonyLib;
    using JetBrains.Annotations;
    using Manager.Impl;
    using State;
    using TrafficManager.Util.Extensions;
    using UnityEngine;
    using Util;

    [UsedImplicitly]
    [HarmonyPatch]
    public class CalculateSegmentPositionPatch {
        private delegate void CalculatePositionDelegate(ushort vehicleID,
                                                        ref Vehicle vehicleData,
                                                        PathUnit.Position position,
                                                        uint laneID,
                                                        byte offset,
                                                        out Vector3 pos,
                                                        out Vector3 dir,
                                                        out float maxSpeed);


        [UsedImplicitly]
        public static MethodBase TargetMethod() =>
            TranspilerUtil.DeclaredMethod<CalculatePositionDelegate>(
                typeof(AircraftAI),
                "CalculateSegmentPosition");

        private static CalculateTargetSpeedDelegate CalculateTargetSpeed;

        [UsedImplicitly]
        public static void Prepare() {
            CalculateTargetSpeed = GameConnectionManager.Instance.VehicleAIConnection.CalculateTargetSpeed;
        }

        [UsedImplicitly]
        public static bool Prefix(AircraftAI __instance,
                                  ushort vehicleID,
                                  ref Vehicle vehicleData,
                                  PathUnit.Position position,
                                  uint laneID,
                                  byte offset,
                                  out Vector3 pos,
                                  out Vector3 dir,
                                  out float maxSpeed) {
            NetInfo info = position.m_segment.ToSegment().Info;
            ref NetLane lane = ref laneID.ToLane();
            lane.CalculatePositionAndDirection( Constants.ByteToFloat(offset), out pos, out dir);

            if (info.m_lanes != null && info.m_lanes.Length > position.m_lane) {
                // NON-STOCK CODE
                float speedLimit = Options.customSpeedLimitsEnabled && info.m_netAI is not RunwayAI
                                       ? SpeedLimitManager.Instance.GetGameSpeedLimit(
                                           position.m_segment,
                                           position.m_lane,
                                           laneID,
                                           info.m_lanes[position.m_lane])
                                       : info.m_lanes[position.m_lane].m_speedLimit;

                // NON-STOCK CODE END
                maxSpeed = CalculateTargetSpeed(__instance, vehicleID, ref vehicleData, speedLimit, lane.m_curve);
                if (speedLimit > 5f) {
                    Randomizer randomizer = new Randomizer(vehicleID);
                    pos.x += randomizer.Int32(-500, 500);
                    pos.y += randomizer.Int32(1300, 1700);
                    pos.z += randomizer.Int32(-500, 500);
                }
            } else {
                maxSpeed = CalculateTargetSpeed(__instance, vehicleID, ref vehicleData, 1f, 0f);
            }

            return false;
        }
    }
}