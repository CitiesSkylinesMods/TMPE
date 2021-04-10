namespace TrafficManager.Patch._VehicleAI._TrainAI{
    using HarmonyLib;
    using JetBrains.Annotations;
    using TrafficManager.Patch._PathManager;
    using System.Reflection;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.Manager.Impl;

    [HarmonyPatch]
    public class StartPathFindPatch {
        [UsedImplicitly]
        public static MethodBase TargetMethod() => StartPathFindCommons.TargetMethod2<TrainAI>();

        [UsedImplicitly]
        public static void Prefix(ushort vehicleID, ref Vehicle vehicleData) {
            ExtVehicleType vehicleType = ExtVehicleManager.Instance.OnStartPathFind(vehicleID, ref vehicleData, null);
            vehicleType = vehicleType switch {
                ExtVehicleType.None => ExtVehicleType.RailVehicle,
                ExtVehicleType.CargoTrain => ExtVehicleType.CargoVehicle,
                _ => vehicleType,
            };

            CreatePathPatch.ExtVehicleType = vehicleType;
            CreatePathPatch.ExtPathType = ExtPathType.None;
            CreatePathPatch.VehicleID = vehicleID;
        }
    }
}
