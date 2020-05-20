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
            ExtVehicleType emergencyVehType = (vehicleData.m_flags & Vehicle.Flags.Emergency2) != 0
                                     ? ExtVehicleType.Emergency
                                     : ExtVehicleType.Service;
            ExtVehicleType vehicleType = ExtVehicleManager.Instance.OnStartPathFind(vehicleID, ref vehicleData, emergencyVehType);
            ref PathCreationArgs args = ref CreatePathPatch.Args;

            args.extPathType = ExtPathType.None;
            args.extVehicleType = vehicleType;
            args.vehicleId = vehicleID;
            args.spawned = (vehicleData.m_flags & Vehicle.Flags.Spawned) != 0;
            args.buildIndex = Singleton<SimulationManager>.instance.m_currentBuildIndex;
            //args.startPosA = startPosA;
            //args.startPosB = startPosB;
            //args.endPosA = endPosA;
            //args.endPosB = endPosB;
            args.vehiclePosition = default;
            args.laneTypes = NetInfo.LaneType.Vehicle
                             | NetInfo.LaneType.TransportVehicle;
            //args.vehicleTypes = info.m_vehicleType;
            args.maxLength = 20000f;
            //args.isHeavyVehicle = IsHeavyVehicle();
            //args.hasCombustionEngine = CombustionEngine();
            //args.ignoreBlocked = IgnoreBlocked(vehicleId, ref vehicleData);
            args.ignoreFlooded = false;
            args.ignoreCosts = false;
            args.randomParking = false;
            args.stablePath = false;
            args.skipQueue = (vehicleData.m_flags & Vehicle.Flags.Spawned) != 0;
        }
    }
}