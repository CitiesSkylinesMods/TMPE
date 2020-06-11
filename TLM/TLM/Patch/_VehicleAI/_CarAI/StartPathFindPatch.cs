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

        /// <summary>
        /// Notifies the extended citizen manager about a citizen that arrived at their destination if the Parking AI is active.
        /// </summary>
        [UsedImplicitly]
        public static void Prefix(ushort vehicleID, ref Vehicle vehicleData,
            Vector3 startPos, Vector3 endPos, bool startBothWays, bool endBothWays, bool undergroundTarget) {
#if DEBUG
            bool vehDebug = DebugSettings.VehicleId == 0
                           || DebugSettings.VehicleId == vehicleID;
            bool logParkingAi = DebugSwitch.BasicParkingAILog.Get() && vehDebug;
#else
            var logParkingAi = false;
#endif
            Log._DebugOnlyWarningIf(
                logParkingAi,
                () => $"_CarAI.StartPathFindPatch.Prefix({vehicleID}): called for vehicle " +
                $"{vehicleID}, startPos={startPos}, endPos={endPos}, " +
                $"startBothWays={startBothWays}, endBothWays={endBothWays}, " +
                $"undergroundTarget={undergroundTarget}");

            CreatePathPatch.ExtVehicleType = ExtVehicleManager.Instance.OnStartPathFind(vehicleID, ref vehicleData, null);
            CreatePathPatch.ExtPathType = ExtPathType.None;
            CreatePathPatch.VehicleID = vehicleID;

            // override vanilla value.
            // TODO [issue #<number>] Is this a mistake?
            CreatePathPatch.LaneTypes = NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle; 
        }
    }
}
