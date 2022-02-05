/// <summary>
/// Patching RTramBaseAI from Reversible Tram AI mod
/// https://steamcommunity.com/sharedfiles/filedetails/?id=2740907672
/// https://github.com/sway2020/ReversibleTramAI
/// </summary>

namespace TrafficManager.Patch._VehicleAI._RTramBaseAI {
    using System;
    using HarmonyLib;
    using JetBrains.Annotations;
    using UnityEngine;
    using Util;

    [UsedImplicitly]
    public class CalculateSegmentPositionPatch {
        private delegate void CalculatePositionDelegate(ushort vehicleID,
                                                        ref Vehicle vehicleData,
                                                        PathUnit.Position position,
                                                        uint laneID,
                                                        byte offset,
                                                        out Vector3 pos,
                                                        out Vector3 dir,
                                                        out float maxSpeed);

        public static bool ApplyPatch(Harmony harmonyInstance, Type rTramBaseAIType) {
            try {
                var original = TranspilerUtil.DeclaredMethod<CalculatePositionDelegate>(rTramBaseAIType, "CalculateSegmentPosition");
                if (original == null) return false;

                var prefix = typeof(CalculateSegmentPositionPatch).GetMethod("Prefix");
                harmonyInstance.Patch(original, new HarmonyMethod(prefix));
                return true;
            }
            catch (Exception ex) {
                ex.LogException();
                return false;
            }
        }

        [UsedImplicitly]
        public static bool Prefix(VehicleAI __instance,
                                  ushort vehicleID,
                                  ref Vehicle vehicleData,
                                  PathUnit.Position position,
                                  uint laneID,
                                  byte offset,
                                  out Vector3 pos,
                                  out Vector3 dir,
                                  out float maxSpeed) {
            VehicleAICommons.CustomCalculateSegmentPosition(
                __instance,
                vehicleID,
                ref vehicleData,
                position,
                laneID,
                offset,
                out pos,
                out dir,
                out maxSpeed);
            return false;
        }
    }
}