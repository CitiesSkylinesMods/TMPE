namespace TrafficManager.Patch._VehicleAI._CarAI{
    using HarmonyLib;
    using JetBrains.Annotations;
    using TrafficManager.Patch._PathManager;
    using System.Reflection;
    using UnityEngine;
    using TrafficManager.State.ConfigData;
    using CSUtil.Commons;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.Manager.Impl;
    using ColossalFramework;

    [HarmonyPatch]
    public class StartPathFindPatch {
        public static MethodBase TargetMethod() => StartPathFindCommons.TargetMethod<CarAI>();

        [UsedImplicitly]
        public static void Prefix(ushort vehicleID, ref Vehicle vehicleData,
            Vector3 startPos, Vector3 endPos, bool startBothWays, bool endBothWays, bool undergroundTarget) {
#if DEBUG
            // TODO [issue #951]: why this message is not printed for all overrides of CarAI?
            bool vehDebug = DebugSettings.VehicleId == 0
                           || DebugSettings.VehicleId == vehicleID;
            bool logParkingAi = DebugSwitch.BasicParkingAILog.Get() && vehDebug;
            Log._DebugOnlyWarningIf(
                logParkingAi,
                () => $"_CarAI.StartPathFindPatch.Prefix({vehicleID}): called for vehicle " +
                $"{vehicleID}, startPos={startPos}, endPos={endPos}, " +
                $"startBothWays={startBothWays}, endBothWays={endBothWays}, " +
                $"undergroundTarget={undergroundTarget}");
#endif

            var vehicleType = ExtVehicleManager.Instance.OnStartPathFind(vehicleID, ref vehicleData, null);
            if (vehicleType == ExtVehicleType.None)
                vehicleType = ExtVehicleType.RoadVehicle;

            CreatePathPatch.ExtVehicleType = vehicleType;
            CreatePathPatch.ExtPathType = ExtPathType.None;
            CreatePathPatch.VehicleID = vehicleID;
        }
    }
}
