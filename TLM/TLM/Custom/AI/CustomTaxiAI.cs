namespace TrafficManager.Custom.AI {
    using API.Traffic.Data;
    using API.Traffic.Enums;
    using ColossalFramework;
    using JetBrains.Annotations;
    using PathFinding;
    using RedirectionFramework.Attributes;
    using UnityEngine;

    [TargetType(typeof(TaxiAI))]
    public class CustomTaxiAI : CarAI {
        [RedirectMethod]
        [UsedImplicitly]
        public bool CustomStartPathFind(ushort vehicleId,
                                        ref Vehicle vehicleData,
                                        Vector3 startPos,
                                        Vector3 endPos,
                                        bool startBothWays,
                                        bool endBothWays,
                                        bool undergroundTarget) {
            var instance = Singleton<CitizenManager>.instance;
            var passengerInstanceId = Constants.ManagerFactory.ExtVehicleManager
                                               .GetDriverInstanceId(vehicleId, ref vehicleData);

            var ctzInstance = instance.m_instances.m_buffer[passengerInstanceId];
            if (passengerInstanceId == 0
                || (ctzInstance.m_flags & CitizenInstance.Flags.Character) != CitizenInstance.Flags.None) {
                return StartPathFind(
                    vehicleId,
                    ref vehicleData,
                    startPos,
                    endPos,
                    startBothWays,
                    endBothWays,
                    undergroundTarget);
            }

            var info = m_info;
            var laneTypes = NetInfo.LaneType.Vehicle
                            | NetInfo.LaneType.Pedestrian
                            | NetInfo.LaneType.TransportVehicle;
            var vehicleTypes = m_info.m_vehicleType;
            var allowUnderground = (vehicleData.m_flags & Vehicle.Flags.Underground) != 0;

            if (!PathManager.FindPathPosition(
                    startPos,
                    ItemClass.Service.Road,
                    NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle,
                    info.m_vehicleType,
                    allowUnderground,
                    false,
                    32f,
                    out var startPosA,
                    out var startPosB,
                    out var startSqrDistA,
                    out _)
                || !Constants.ManagerFactory.ExtCitizenInstanceManager.FindPathPosition(
                    passengerInstanceId,
                    ref ctzInstance,
                    endPos,
                    laneTypes,
                    vehicleTypes,
                    undergroundTarget,
                    out var endPosA)) {
                return false;
            }

            if ((ctzInstance.m_flags & CitizenInstance.Flags.CannotUseTransport) == CitizenInstance.Flags.None) {
                laneTypes |= NetInfo.LaneType.PublicTransport;

                var citizenId = ctzInstance.m_citizen;

                if (citizenId != 0u
                    && (instance.m_citizens.m_buffer[citizenId].m_flags
                        & Citizen.Flags.Evacuating) != Citizen.Flags.None) {
                    laneTypes |= NetInfo.LaneType.EvacuationTransport;
                }
            }

            if (!startBothWays || startSqrDistA < 10f) {
                startPosB = default;
            }

            var endPosB = default(PathUnit.Position);
            var simMan = Singleton<SimulationManager>.instance;

            // NON-STOCK CODE START
            PathCreationArgs args;
            args.extPathType = ExtPathType.None;
            args.extVehicleType = ExtVehicleType.Taxi;
            args.vehicleId = vehicleId;
            args.spawned = (vehicleData.m_flags & Vehicle.Flags.Spawned) != 0;
            args.buildIndex = simMan.m_currentBuildIndex;
            args.startPosA = startPosA;
            args.startPosB = startPosB;
            args.endPosA = endPosA;
            args.endPosB = endPosB;
            args.vehiclePosition = default;
            args.laneTypes = laneTypes;
            args.vehicleTypes = vehicleTypes;
            args.maxLength = 20000f;
            args.isHeavyVehicle = IsHeavyVehicle();
            args.hasCombustionEngine = CombustionEngine();
            args.ignoreBlocked = IgnoreBlocked(vehicleId, ref vehicleData);
            args.ignoreFlooded = false;
            args.ignoreCosts = false;
            args.randomParking = false;
            args.stablePath = false;
            args.skipQueue = (vehicleData.m_flags & Vehicle.Flags.Spawned) != 0;

            if (CustomPathManager._instance.CustomCreatePath(out var path, ref simMan.m_randomizer, args)) {
                // NON-STOCK CODE END
                if (vehicleData.m_path != 0u) {
                    Singleton<PathManager>.instance.ReleasePath(vehicleData.m_path);
                }

                vehicleData.m_path = path;
                vehicleData.m_flags |= Vehicle.Flags.WaitingPath;
                return true;
            }

            return false;
        }
    }
}