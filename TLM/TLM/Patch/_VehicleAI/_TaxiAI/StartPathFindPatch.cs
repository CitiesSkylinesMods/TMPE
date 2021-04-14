namespace TrafficManager.Patch._VehicleAI._TaxiAI{
    using HarmonyLib;
    using JetBrains.Annotations;
    using TrafficManager.Patch._PathManager;
    using System.Reflection;

    using TrafficManager.API.Traffic.Enums;


    [HarmonyPatch]
    public class StartPathFindPatch {
        public static MethodBase TargetMethod() => StartPathFindCommons.TargetMethod<TaxiAI>();

        [UsedImplicitly]
        public static void Prefix(ushort vehicleID, ref Vehicle vehicleData) {
            CreatePathPatch.ExtVehicleType = ExtVehicleType.Taxi;
            CreatePathPatch.ExtPathType = ExtPathType.None;
            CreatePathPatch.VehicleID = vehicleID;
        }
    }
}
