namespace TrafficManager.Patch._VehicleAI._ShipAI{
    using HarmonyLib;
    using JetBrains.Annotations;
    using TrafficManager.Patch._PathManager;
    using System.Reflection;
    using TrafficManager.API.Traffic.Enums;

    [HarmonyPatch]
    public class StartPathFindPatch {
        [UsedImplicitly]
        public static MethodBase TargetMethod() => StartPathFindCommons.TargetMethod2<ShipAI>();

        [UsedImplicitly]
        public static void Prefix(ushort vehicleID, ref Vehicle vehicleData) {
            CreatePathPatch.ExtPathType = ExtPathType.None;
            CreatePathPatch.ExtVehicleType =
                vehicleData.Info.m_vehicleAI is PassengerShipAI ? ExtVehicleType.PassengerShip : ExtVehicleType.CargoVehicle;
            CreatePathPatch.VehicleID = vehicleID;
        }
    }
}
