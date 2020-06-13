namespace TrafficManager.Patch._VehicleAI._TramBaseAI{
    using HarmonyLib;
    using JetBrains.Annotations;
    using TrafficManager.Patch._PathManager;
    using System.Reflection;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.Manager.Impl;

    [HarmonyPatch]
    public class StartPathFindPatch {

        [UsedImplicitly]
        public static MethodBase TargetMethod() => StartPathFindCommons.TargetMethod2<TramBaseAI>();

        [UsedImplicitly]
        public static void Prefix(ushort vehicleID, ref Vehicle vehicleData) {
            ExtVehicleManager.Instance.OnStartPathFind(vehicleID, ref vehicleData, null);
            CreatePathPatch.ExtVehicleType = ExtVehicleType.Tram;
            CreatePathPatch.ExtPathType = ExtPathType.None;
            CreatePathPatch.VehicleID = vehicleID;
        }
    }
}
