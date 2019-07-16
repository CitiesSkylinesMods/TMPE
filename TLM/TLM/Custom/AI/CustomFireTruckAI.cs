﻿namespace TrafficManager.Custom.AI {
    using API.Traffic.Data;
    using API.Traffic.Enums;
    using ColossalFramework;
    using CSUtil.Commons.Benchmark;
    using Custom.PathFinding;
    using Manager.Impl;
    using RedirectionFramework.Attributes;
    using Traffic.Data;
    using UnityEngine;

    [TargetType(typeof(FireTruckAI))]
    public class CustomFireTruckAI : CarAI {
        [RedirectMethod]
        public bool CustomStartPathFind(ushort vehicleID, ref Vehicle vehicleData, Vector3 startPos, Vector3 endPos, bool startBothWays, bool endBothWays, bool undergroundTarget) {
#if DEBUG
            //Log._Debug($"CustomFireTruckAI.CustomStartPathFind called for vehicle {vehicleID}");
#endif
            ExtVehicleType vehicleType = ExtVehicleType.None;
#if BENCHMARK
			using (var bm = new Benchmark(null, "OnStartPathFind")) {
#endif
            vehicleType = ExtVehicleManager.Instance.OnStartPathFind(vehicleID, ref vehicleData, (vehicleData.m_flags & Vehicle.Flags.Emergency2) != 0 ? ExtVehicleType.Emergency : ExtVehicleType.Service);
#if BENCHMARK
			}
#endif
            VehicleInfo info = this.m_info;
            bool allowUnderground = (vehicleData.m_flags & (Vehicle.Flags.Underground | Vehicle.Flags.Transition)) != 0;
            PathUnit.Position startPosA;
            PathUnit.Position startPosB;
            float startDistSqrA;
            float startDistSqrB;
            PathUnit.Position endPosA;
            PathUnit.Position endPosB;
            float endDistSqrA;
            float endDistSqrB;
            if (CustomPathManager.FindPathPosition(startPos, ItemClass.Service.Road, NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle, info.m_vehicleType, allowUnderground, false, 32f, out startPosA, out startPosB, out startDistSqrA, out startDistSqrB) &&
                CustomPathManager.FindPathPosition(endPos, ItemClass.Service.Road, NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle, info.m_vehicleType, undergroundTarget, false, 32f, out endPosA, out endPosB, out endDistSqrA, out endDistSqrB)) {
                if (!startBothWays || startDistSqrA < 10f) {
                    startPosB = default(PathUnit.Position);
                }
                if (!endBothWays || endDistSqrA < 10f) {
                    endPosB = default(PathUnit.Position);
                }
                uint path;
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
                args.vehiclePosition = default(PathUnit.Position);
                args.laneTypes = NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle;
                args.vehicleTypes = info.m_vehicleType;
                args.maxLength = 20000f;
                args.isHeavyVehicle = this.IsHeavyVehicle();
                args.hasCombustionEngine = this.CombustionEngine();
                args.ignoreBlocked = this.IgnoreBlocked(vehicleID, ref vehicleData);
                args.ignoreFlooded = false;
                args.ignoreCosts = false;
                args.randomParking = false;
                args.stablePath = false;
                args.skipQueue = (vehicleData.m_flags & Vehicle.Flags.Spawned) != 0;

                if (CustomPathManager._instance.CustomCreatePath(out path, ref Singleton<SimulationManager>.instance.m_randomizer, args)) {
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