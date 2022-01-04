namespace TrafficManager.Patch._VehicleAI._CarAI {
    using System.Reflection;
    using HarmonyLib;
    using JetBrains.Annotations;
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
        public static MethodBase TargetMethod() => TranspilerUtil.DeclaredMethod<CalculatePositionDelegate>(typeof(CarAI), "CalculateSegmentPosition");

        [UsedImplicitly]
        public static bool Prefix(CarAI __instance,
                                  ushort vehicleID,
                                  ref Vehicle vehicleData,
                                  PathUnit.Position position,
                                  uint laneID,
                                  byte offset,
                                  out Vector3 pos,
                                  out Vector3 dir,
                                  out float maxSpeed) {
            // NON-STOCK CODE START
            VehicleAICommons.CustomCalculateSegmentPosition(__instance,
                                                            vehicleID,
                                                            ref vehicleData,
                                                            position,
                                                            laneID,
                                                            offset,
                                                            out pos,
                                                            out dir,
                                                            out maxSpeed);

            maxSpeed = Constants.ManagerFactory.VehicleBehaviorManager.CalcMaxSpeed(
                vehicleID,
                ref Constants.ManagerFactory.ExtVehicleManager.ExtVehicles[vehicleID],
                __instance.m_info,
                position,
                ref position.m_segment.ToSegment(),
                pos,
                maxSpeed,
                false);

            // NON-STOCK CODE END
            return false;
        }

    }
}