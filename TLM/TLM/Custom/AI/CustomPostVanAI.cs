namespace TrafficManager.Custom.AI {
    using ColossalFramework;
    using JetBrains.Annotations;
    using TrafficManager.API.Traffic.Data;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.Custom.PathFinding;
    using TrafficManager.RedirectionFramework.Attributes;
    using UnityEngine;

    [TargetType(typeof(PostVanAI))]
    public class CustomPostVanAI : CarAI {
        [RedirectMethod]
        [UsedImplicitly]
        public bool CustomStartPathFind(ushort vehicleId,
                                        ref Vehicle vehicleData,
                                        Vector3 startPos,
                                        Vector3 endPos,
                                        bool startBothWays,
                                        bool endBothWays,
                                        bool undergroundTarget) {
            if (vehicleData.m_transferType == (byte)TransferManager.TransferReason.Mail) {
                return base.StartPathFind(
                    vehicleId,
                    ref vehicleData,
                    startPos,
                    endPos,
                    startBothWays,
                    endBothWays,
                    undergroundTarget);
            }

            if ((vehicleData.m_flags & (Vehicle.Flags.TransferToSource | Vehicle.Flags.GoingBack)) != 0) {
                return base.StartPathFind(
                    vehicleId,
                    ref vehicleData,
                    startPos,
                    endPos,
                    startBothWays,
                    endBothWays,
                    undergroundTarget);
            }

            bool allowUnderground = (vehicleData.m_flags & (Vehicle.Flags.Underground
                                                           | Vehicle.Flags.Transition)) != 0;

            // try to find road start position
            bool startPosFound = PathManager.FindPathPosition(
                startPos,
                ItemClass.Service.Road,
                NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle,
                VehicleInfo.VehicleType.Car,
                allowUnderground,
                false,
                32f,
                out PathUnit.Position startPosA,
                out PathUnit.Position startPosB,
                out float startDistSqrA,
                out _);

            // try to find other start position (plane, train, ship)
            const VehicleInfo.VehicleType VEH_TYPE_MASK = VehicleInfo.VehicleType.Train
                                                          | VehicleInfo.VehicleType.Ship
                                                          | VehicleInfo.VehicleType.Plane;
            if (PathManager.FindPathPosition(
                startPos,
                ItemClass.Service.PublicTransport,
                NetInfo.LaneType.Vehicle,
                VEH_TYPE_MASK,
                allowUnderground,
                false,
                32f,
                out PathUnit.Position altStartPosA,
                out PathUnit.Position altStartPosB,
                out float altStartDistSqrA,
                out _)) {
                if (!startPosFound
                    || (altStartDistSqrA < startDistSqrA
                        && (Mathf.Abs(startPos.x) > 4800f
                            || Mathf.Abs(startPos.z) > 4800f))) {
                    startPosA = altStartPosA;
                    startPosB = altStartPosB;
                    startDistSqrA = altStartDistSqrA;
                }

                startPosFound = true;
            }

            // try to find road end position
            bool endPosFound = PathManager.FindPathPosition(
                endPos,
                ItemClass.Service.Road,
                NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle,
                VehicleInfo.VehicleType.Car,
                undergroundTarget,
                false,
                32f,
                out PathUnit.Position endPosA,
                out PathUnit.Position endPosB,
                out float endDistSqrA,
                out _);

            // try to find other end position (plane, train, ship)
            if (PathManager.FindPathPosition(
                endPos,
                ItemClass.Service.PublicTransport,
                NetInfo.LaneType.Vehicle,
                VEH_TYPE_MASK,
                undergroundTarget,
                false,
                32f,
                out PathUnit.Position altEndPosA,
                out PathUnit.Position altEndPosB,
                out float altEndDistSqrA,
                out _)) {
                if (!endPosFound
                    || (altEndDistSqrA < endDistSqrA
                        && (Mathf.Abs(endPos.x) > 4800f
                            || Mathf.Abs(endPos.z) > 4800f))) {
                    endPosA = altEndPosA;
                    endPosB = altEndPosB;
                    endDistSqrA = altEndDistSqrA;
                }

                endPosFound = true;
            }

            if (!startPosFound || !endPosFound) {
                return false;
            }

            CustomPathManager pathManager = CustomPathManager._instance;
            if (!startBothWays || startDistSqrA < 10f) {
                startPosB = default;
            }

            if (!endBothWays || endDistSqrA < 10f) {
                endPosB = default;
            }

            PathCreationArgs args;
            args.extPathType = ExtPathType.None;
            args.extVehicleType = ExtVehicleType.Service;
            args.vehicleId = vehicleId;
            args.spawned = (vehicleData.m_flags & Vehicle.Flags.Spawned) != 0;
            args.buildIndex = Singleton<SimulationManager>.instance.m_currentBuildIndex;
            args.startPosA = startPosA;
            args.startPosB = startPosB;
            args.endPosA = endPosA;
            args.endPosB = endPosB;
            args.vehiclePosition = default;
            args.laneTypes = NetInfo.LaneType.Vehicle
                             | NetInfo.LaneType.CargoVehicle;
            args.vehicleTypes = VehicleInfo.VehicleType.Car
                                | VehicleInfo.VehicleType.Train
                                | VehicleInfo.VehicleType.Ship
                                | VehicleInfo.VehicleType.Plane;
            args.maxLength = 20000f;
            args.isHeavyVehicle = IsHeavyVehicle();
            args.hasCombustionEngine = CombustionEngine();
            args.ignoreBlocked = IgnoreBlocked(vehicleId, ref vehicleData);
            args.ignoreFlooded = false;
            args.ignoreCosts = false;
            args.randomParking = false;
            args.stablePath = false;
            args.skipQueue = (vehicleData.m_flags & Vehicle.Flags.Spawned) != 0;

            if (pathManager.CustomCreatePath(
                out uint path,
                ref Singleton<SimulationManager>.instance.m_randomizer,
                args))
            {
                if (vehicleData.m_path != 0) {
                    pathManager.ReleasePath(vehicleData.m_path);
                }

                vehicleData.m_path = path;
                vehicleData.m_flags |= Vehicle.Flags.WaitingPath;
                return true;
            }

            return false;
        }
    }
}