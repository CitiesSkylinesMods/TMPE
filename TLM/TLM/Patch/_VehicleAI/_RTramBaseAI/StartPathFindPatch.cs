/// <summary>
/// Patching RTramBaseAI from Reversible Tram AI mod
/// https://steamcommunity.com/sharedfiles/filedetails/?id=2740907672
/// https://github.com/sway2020/ReversibleTramAI
/// </summary>

namespace TrafficManager.Patch._VehicleAI._RTramBaseAI {
    using HarmonyLib;
    using JetBrains.Annotations;
    using TrafficManager.Patch._PathManager;
    using System;
    using System.Reflection;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.Manager.Impl;
    using Util;

    [UsedImplicitly]
    public class StartPathFindPatch {

        public static bool ApplyPatch(Harmony harmonyInstance, Type rTramBaseAIType) {
            try {
                MethodInfo method = typeof(StartPathFindCommons).GetMethod(nameof(StartPathFindCommons.TargetMethod2));
                MethodInfo genericMethod = method.MakeGenericMethod(rTramBaseAIType);
                var original = (MethodBase)genericMethod.Invoke(null, null);
                if (original == null) return false;

                var prefix = typeof(StartPathFindPatch).GetMethod("Prefix");
                harmonyInstance.Patch(original, new HarmonyMethod(prefix));
                return true;
            }
            catch (Exception ex) {
                ex.LogException();
                return false;
            }
        }

        [UsedImplicitly]
        public static void Prefix(ushort vehicleID, ref Vehicle vehicleData) {
            ExtVehicleManager.Instance.OnStartPathFind(vehicleID, ref vehicleData, null);
            CreatePathPatch.ExtVehicleType = ExtVehicleType.Tram;
            CreatePathPatch.ExtPathType = ExtPathType.None;
            CreatePathPatch.VehicleID = vehicleID;
        }
    }
}
