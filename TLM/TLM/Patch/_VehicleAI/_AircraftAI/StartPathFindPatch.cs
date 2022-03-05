namespace TrafficManager.Patch._VehicleAI._AircraftAI{
    using HarmonyLib;
    using JetBrains.Annotations;
    using TrafficManager.Patch._PathManager;
    using System.Reflection;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.Manager.Impl;


    [HarmonyPatch]
    public class StartPathFindPatch {

        [UsedImplicitly]
        public static MethodBase TargetMethod() => StartPathFindCommons.TargetMethod2<AircraftAI>();

        [UsedImplicitly]
        public static void Prefix(ushort vehicleID, ref Vehicle vehicleData) {
            ExtVehicleManager.Instance.OnStartPathFind(vehicleID, ref vehicleData, null);
            CreatePathPatch.ExtVehicleType = vehicleData.Info.m_vehicleAI is CargoPlaneAI ? ExtVehicleType.CargoPlane : ExtVehicleType.PassengerPlane;
            CreatePathPatch.ExtPathType = ExtPathType.None;
            CreatePathPatch.VehicleID = vehicleID;
        }
    }
}
