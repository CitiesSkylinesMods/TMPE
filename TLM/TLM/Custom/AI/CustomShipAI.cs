﻿namespace TrafficManager.Custom.AI {
    using TrafficManager.API.Traffic.Data;
    using TrafficManager.API.Traffic.Enums;
    using ColossalFramework;
    using JetBrains.Annotations;
    using TrafficManager.Custom.PathFinding;
    using TrafficManager.RedirectionFramework.Attributes;
    using UnityEngine;

    [TargetType(typeof(ShipAI))]
    public class CustomShipAI : ShipAI {
        [RedirectMethod]
        [UsedImplicitly]
        public bool CustomStartPathFind(ushort vehicleId,
                                        ref Vehicle vehicleData,
                                        Vector3 startPos,
                                        Vector3 endPos,
                                        bool startBothWays,
                                        bool endBothWays) {
            // NON-STOCK CODE START
            ExtVehicleType vehicleType = vehicleData.Info.m_vehicleAI is PassengerShipAI
                                  ? ExtVehicleType.PassengerShip
                                  : ExtVehicleType.CargoVehicle;

            // NON-STOCK CODE END
            VehicleInfo info = m_info;

            if (!PathManager.FindPathPosition(
                    startPos,
                    ItemClass.Service.PublicTransport,
                    NetInfo.LaneType.Vehicle,
                    info.m_vehicleType,
                    false,
                    false,
                    64f,
                    out PathUnit.Position startPosA,
                    out PathUnit.Position startPosB,
                    out float startSqrDistA,
                    out _)
                || !PathManager.FindPathPosition(
                    endPos,
                    ItemClass.Service.PublicTransport,
                    NetInfo.LaneType.Vehicle,
                    info.m_vehicleType,
                    false,
                    false,
                    64f,
                    out PathUnit.Position endPosA,
                    out PathUnit.Position endPosB,
                    out float endSqrDistA,
                    out _)) {
                return false;
            }

            if (!startBothWays || startSqrDistA < 10f) {
                startPosB = default;
            }

            if (!endBothWays || endSqrDistA < 10f) {
                endPosB = default;
            }

            // NON-STOCK CODE START
            PathCreationArgs args;
            args.extPathType = ExtPathType.None;
            args.extVehicleType = vehicleType;
            args.vehicleId = vehicleId;
            args.spawned = (vehicleData.m_flags & Vehicle.Flags.Spawned) != 0;
            args.buildIndex = Singleton<SimulationManager>.instance.m_currentBuildIndex;
            args.startPosA = startPosA;
            args.startPosB = startPosB;
            args.endPosA = endPosA;
            args.endPosB = endPosB;
            args.vehiclePosition = default;
            args.laneTypes = NetInfo.LaneType.Vehicle;
            args.vehicleTypes = info.m_vehicleType;
            args.maxLength = 20000f;
            args.isHeavyVehicle = false;
            args.hasCombustionEngine = false;
            args.ignoreBlocked = false;
            args.ignoreFlooded = false;
            args.ignoreCosts = false;
            args.randomParking = false;
            args.stablePath = false;
            args.skipQueue = false;

            if (!CustomPathManager._instance.CustomCreatePath(
                    out uint path,
                    ref Singleton<SimulationManager>.instance.m_randomizer,
                    args)) {
                return false;
            }

            // NON-STOCK CODE END
            if (vehicleData.m_path != 0u) {
                Singleton<PathManager>.instance.ReleasePath(vehicleData.m_path);
            }

            vehicleData.m_path = path;
            vehicleData.m_flags |= Vehicle.Flags.WaitingPath;
            return true;
        }
    }
}