namespace TrafficManager.Patch._VehicleAI._PoliceCarAI {
    using HarmonyLib;
    using JetBrains.Annotations;
    using TrafficManager.Patch._PathManager;
    using System.Reflection;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.Manager.Impl;
    using ColossalFramework;

    [HarmonyPatch]
    public class StartPathFindPatch {
        [UsedImplicitly]
        public static MethodBase TargetMethod() => StartPathFindCommons.TargetMethod<PoliceCarAI>();

        /// <summary>
        /// Notifies the extended citizen manager about a citizen that arrived at their destination if the Parking AI is active.
        /// </summary>
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
