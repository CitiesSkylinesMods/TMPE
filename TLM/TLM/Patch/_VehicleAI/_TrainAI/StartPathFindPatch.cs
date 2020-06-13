namespace TrafficManager.Patch._VehicleAI._TrainAI{
    using HarmonyLib;
    using JetBrains.Annotations;
    using TrafficManager.Patch._PathManager;
    using System.Reflection;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.Manager.Impl;
    using ColossalFramework;
    using System.Reflection.Emit;
    using System.Collections.Generic;
    using CSUtil.Commons;
    using static TrafficManager.Util.TranspilerUtil;
    using TrafficManager.UI.MainMenu.OSD;
    using TrafficManager.Util;

    [HarmonyPatch]
    public class StartPathFindPatch {

        [UsedImplicitly]
        public static MethodBase TargetMethod() => StartPathFindCommons.TargetMethod<TrainAI>();

        [UsedImplicitly]
        public static void Prefix(ushort vehicleID, ref Vehicle vehicleData) {
            ExtVehicleType vehicleType = ExtVehicleManager.Instance.OnStartPathFind(vehicleID, ref vehicleData, null);
            if (vehicleType == ExtVehicleType.None)
                vehicleType = ExtVehicleType.RailVehicle;
            else if (vehicleType == ExtVehicleType.CargoTrain)
                vehicleType = ExtVehicleType.CargoVehicle;

            CreatePathPatch.ExtVehicleType = vehicleType;
            CreatePathPatch.ExtPathType = ExtPathType.None;
            CreatePathPatch.VehicleID = vehicleID;
        }
    }
}
