namespace TrafficManager.Patch._VehicleAI._BusAI{
    using HarmonyLib;
    using JetBrains.Annotations;
    using TrafficManager.Patch._PathManager;
    using System.Reflection;
    using TrafficManager.API.Traffic.Enums;

    [HarmonyPatch]
    public class StartPathFindPatch {

        [UsedImplicitly]
        public static MethodBase TargetMethod() => StartPathFindCommons.TargetMethod<BusAI>();

        [UsedImplicitly]
        public static void Prefix(ushort vehicleID, ref Vehicle vehicleData) {
            CreatePathPatch.ExtPathType = ExtPathType.None;
            CreatePathPatch.ExtVehicleType = ExtVehicleType.Bus;
            CreatePathPatch.VehicleID = vehicleID;
        }
    }
}
