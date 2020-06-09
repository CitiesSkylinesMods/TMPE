namespace TrafficManager.Patch._VehicleAI._AmbulanceAI {
    using ColossalFramework;
    using HarmonyLib;
    using JetBrains.Annotations;
    using TrafficManager.API.Traffic.Data;
    using TrafficManager.Custom.PathFinding;
    using TrafficManager.State;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.Manager.Impl;
    using TrafficManager.RedirectionFramework.Attributes;
    using UnityEngine;
    using TrafficManager.Patch._RoadBaseAI;

    [HarmonyPatch(typeof(AmbulanceAI), "StartPathFind")]
    public class StartPathFindPatch {
        [HarmonyPrefix]
        [UsedImplicitly]
        public static void Prefix( AmbulanceAI __instance, ushort vehicleID, ref Vehicle vehicleData, Vector3 startPos, Vector3 endPos) {
            ExtVehicleType vehicleType =
                vehicleData.m_flags.IsFlagSet(Vehicle.Flags.Emergency2)
                ? ExtVehicleType.Emergency
                : ExtVehicleType.Service;
            vehicleType = ExtVehicleManager.Instance.OnStartPathFind(vehicleID, ref vehicleData, vehicleType);

            ref PathCreationArgs args = ref CreatePathPatch.Args;
            args.extVehicleType = vehicleType;
            args.extPathType = ExtPathType.None;
            args.vehicleId = vehicleID;
        }
    }
}