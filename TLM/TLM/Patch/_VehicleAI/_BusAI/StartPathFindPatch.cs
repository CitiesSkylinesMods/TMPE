namespace TrafficManager.Patch._VehicleAI._BusAI{
    using ColossalFramework;
    using HarmonyLib;
    using JetBrains.Annotations;
    using TrafficManager.API.Traffic.Data;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.Manager.Impl;
    using UnityEngine;
    using TrafficManager.Patch._RoadBaseAI;

    [HarmonyPatch(typeof(BusAI), "StartPathFind")]
    public class BusAIPatch {
        /// <summary>
        /// Notifies the extended citizen manager about a citizen that arrived at their destination if the Parking AI is active.
        /// </summary>
        [HarmonyPrefix]
        [UsedImplicitly]
        public static void Prefix(BusAI __instance, ushort vehicleID, ref Vehicle vehicleData, Vector3 startPos, Vector3 endPos) {
            ExtVehicleType emergencyVehType = (vehicleData.m_flags & Vehicle.Flags.Emergency2) != 0
                                     ? ExtVehicleType.Emergency
                                     : ExtVehicleType.Service;
            ExtVehicleType vehicleType = ExtVehicleManager.Instance.OnStartPathFind(vehicleID, ref vehicleData, emergencyVehType);
            ref PathCreationArgs args = ref CreatePathPatch.Args;

            args.extPathType = ExtPathType.None;
            args.extVehicleType = ExtVehicleType.Bus;
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
            args.randomParking = false;
            args.ignoreCosts = false;
            args.stablePath = true;
            args.skipQueue = true;

            Singleton<PathManager>.instance.CreatePath(out path,
                ref Singleton<SimulationManager>.instance.m_randomizer, Singleton<SimulationManager>.instance.m_currentBuildIndex,
                startPosA, startPosB, endPosA, endPosB, default(PathUnit.Position),
                NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle, info.m_vehicleType, 20000f,
                this.IsHeavyVehicle(), this.IgnoreBlocked(vehicleID, ref vehicleData),
                false, false, false, false,
                this.CombustionEngine())
        }
    }
}
