namespace TrafficManager.Patch._VehicleAI._FireTruckAI {
    using HarmonyLib;
    using JetBrains.Annotations;
    using TrafficManager.Patch._PathManager;
    using System.Reflection;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.Manager.Impl;
    using ColossalFramework;
    using TrafficManager.Util.Extensions;

    [HarmonyPatch]
    public class StartPathFindPatch {
        [UsedImplicitly]
        public static MethodBase TargetMethod() => StartPathFindCommons.TargetMethod<FireTruckAI>();

        [UsedImplicitly]
        public static void Prefix(ushort vehicleID, ref Vehicle vehicleData) {
            ExtVehicleType emergencyVehType = vehicleData.m_flags.IsFlagSet(Vehicle.Flags.Emergency2)
                                     ? ExtVehicleType.Emergency
                                     : ExtVehicleType.Service;
            CreatePathPatch.ExtVehicleType = ExtVehicleManager.Instance.OnStartPathFind(vehicleID, ref vehicleData, emergencyVehType);
            CreatePathPatch.ExtPathType = ExtPathType.None;
            CreatePathPatch.VehicleID = vehicleID;
        }
    }
}
