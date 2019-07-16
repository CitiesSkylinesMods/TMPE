namespace TrafficManager.Custom.AI {
    using API.Traffic.Data;
    using API.Traffic.Enums;
    using ColossalFramework;
    using PathFinding;
    using JetBrains.Annotations;
    using Manager.Impl;
    using RedirectionFramework.Attributes;
    using UnityEngine;
#if BENCHMARK
    using CSUtil.Commons.Benchmark;
#endif

    [TargetType(typeof(FireTruckAI))]
    public class CustomFireTruckAI : CarAI {
        [RedirectMethod]
        [UsedImplicitly]
        public bool CustomStartPathFind(ushort vehicleID,
                                        ref Vehicle vehicleData,
                                        Vector3 startPos,
                                        Vector3 endPos,
                                        bool startBothWays,
                                        bool endBothWays,
                                        bool undergroundTarget) {
#if BENCHMARK
            using (var bm = new Benchmark(null, "OnStartPathFind")) {
#endif
            var vehicleType = ExtVehicleManager.Instance.OnStartPathFind(
                vehicleID,
                ref vehicleData,
                (vehicleData.m_flags & Vehicle.Flags.Emergency2) != 0
                    ? ExtVehicleType.Emergency
                    : ExtVehicleType.Service);
#if BENCHMARK
            }
#endif
            var info = m_info;
            var allowUnderground = (vehicleData.m_flags & (Vehicle.Flags.Underground
                                                           | Vehicle.Flags.Transition)) != 0;

            if (PathManager.FindPathPosition(
                    startPos,
                    ItemClass.Service.Road,
                    NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle,
                    info.m_vehicleType,
                    allowUnderground,
                    false,
                    32f,
                    out var startPosA,
                    out var startPosB,
                    out var startDistSqrA,
                    out _)
                && PathManager.FindPathPosition(
                    endPos,
                    ItemClass.Service.Road,
                    NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle,
                    info.m_vehicleType,
                    undergroundTarget,
                    false,
                    32f,
                    out var endPosA,
                    out var endPosB,
                    out var endDistSqrA,
                    out _)) {
                if (!startBothWays || startDistSqrA < 10f) {
                    startPosB = default;
                }

                if (!endBothWays || endDistSqrA < 10f) {
                    endPosB = default;
                }

                // NON-STOCK CODE START
                PathCreationArgs args;
                args.extPathType = ExtPathType.None;
                args.extVehicleType = vehicleType;
                args.vehicleId = vehicleID;
                args.spawned = (vehicleData.m_flags & Vehicle.Flags.Spawned) != 0;
                args.buildIndex = Singleton<SimulationManager>.instance.m_currentBuildIndex;
                args.startPosA = startPosA;
                args.startPosB = startPosB;
                args.endPosA = endPosA;
                args.endPosB = endPosB;
                args.vehiclePosition = default;
                args.laneTypes = NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle;
                args.vehicleTypes = info.m_vehicleType;
                args.maxLength = 20000f;
                args.isHeavyVehicle = IsHeavyVehicle();
                args.hasCombustionEngine = CombustionEngine();
                args.ignoreBlocked = IgnoreBlocked(vehicleID, ref vehicleData);
                args.ignoreFlooded = false;
                args.ignoreCosts = false;
                args.randomParking = false;
                args.stablePath = false;
                args.skipQueue = (vehicleData.m_flags & Vehicle.Flags.Spawned) != 0;

                if (CustomPathManager._instance.CustomCreatePath(
                    out var path,
                    ref Singleton<SimulationManager>.instance.m_randomizer,
                    args)) {
                    // NON-STOCK CODE END
                    if (vehicleData.m_path != 0u) {
                        Singleton<PathManager>.instance.ReleasePath(vehicleData.m_path);
                    }

                    vehicleData.m_path = path;
                    vehicleData.m_flags |= Vehicle.Flags.WaitingPath;
                    return true;
                }
            } else {
                PathfindFailure(vehicleID, ref vehicleData);
            }

            return false;
        }
    }
}