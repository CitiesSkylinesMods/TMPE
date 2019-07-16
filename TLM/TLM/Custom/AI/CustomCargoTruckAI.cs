namespace TrafficManager.Custom.AI {
    using System.Runtime.CompilerServices;
    using API.Traffic.Data;
    using API.Traffic.Enums;
    using ColossalFramework;
    using CSUtil.Commons;
    using JetBrains.Annotations;
    using Manager.Impl;
    using PathFinding;
    using RedirectionFramework.Attributes;
    using UnityEngine;

    [TargetType(typeof(CargoTruckAI))]
    public class CustomCargoTruckAI : CarAI {
        [RedirectMethod]
        public void CustomSimulationStep(ushort vehicleId, ref Vehicle vehicleData, Vector3 physicsLodRefPos) {
            if ((vehicleData.m_flags & Vehicle.Flags.Congestion) != 0 &&
                VehicleBehaviorManager.Instance.MayDespawn(ref vehicleData)) {
                Singleton<VehicleManager>.instance.ReleaseVehicle(vehicleId);
                return;
            }

            if ((vehicleData.m_flags & Vehicle.Flags.WaitingTarget) != 0
                && (vehicleData.m_waitCounter += 1) > 20)
            {
                RemoveOffers(vehicleId, ref vehicleData);
                vehicleData.m_flags &= ~Vehicle.Flags.WaitingTarget;
                vehicleData.m_flags |= Vehicle.Flags.GoingBack;
                vehicleData.m_waitCounter = 0;

                if (!StartPathFind(vehicleId, ref vehicleData)) {
                    vehicleData.Unspawn(vehicleId);
                }
            }

            base.SimulationStep(vehicleId, ref vehicleData, physicsLodRefPos);
        }

        [RedirectMethod]
        [UsedImplicitly]
        public bool CustomStartPathFind(ushort vehicleId,
                                        ref Vehicle vehicleData,
                                        Vector3 startPos,
                                        Vector3 endPos,
                                        bool startBothWays,
                                        bool endBothWays,
                                        bool undergroundTarget) {
            var vehicleType = ExtVehicleManager.Instance.OnStartPathFind(vehicleId, ref vehicleData, null);

            if (vehicleType == ExtVehicleType.None) {
                Log._DebugOnlyWarning(
                    $"CustomCargoTruck.CustomStartPathFind: Vehicle {vehicleId} " +
                    $"does not have a valid vehicle type!");
            }

            if ((vehicleData.m_flags & (Vehicle.Flags.TransferToSource
                                        | Vehicle.Flags.GoingBack)) != 0) {
                return base.StartPathFind(
                    vehicleId,
                    ref vehicleData,
                    startPos,
                    endPos,
                    startBothWays,
                    endBothWays,
                    undergroundTarget);
            }

            var allowUnderground = (vehicleData.m_flags & (Vehicle.Flags.Underground
                                                           | Vehicle.Flags.Transition)) != 0;
            var startPosFound = PathManager.FindPathPosition(
                startPos,
                ItemClass.Service.Road,
                NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle,
                VehicleInfo.VehicleType.Car,
                allowUnderground,
                false,
                32f,
                out var startPosA,
                out var startPosB,
                out var startDistSqrA,
                out _);

            const VehicleInfo.VehicleType TRAIN_SHIP_PLANE = VehicleInfo.VehicleType.Train
                                                             | VehicleInfo.VehicleType.Ship
                                                             | VehicleInfo.VehicleType.Plane;
            if (PathManager.FindPathPosition(
                startPos,
                ItemClass.Service.PublicTransport,
                NetInfo.LaneType.Vehicle,
                TRAIN_SHIP_PLANE,
                allowUnderground,
                false,
                32f,
                out var startAltPosA,
                out var startAltPosB,
                out var startAltDistSqrA,
                out _)) {
                if (!startPosFound
                    || (startAltDistSqrA < startDistSqrA
                        && (Mathf.Abs(endPos.x) > 8000f
                            || Mathf.Abs(endPos.z) > 8000f)))
                {
                    startPosA = startAltPosA;
                    startPosB = startAltPosB;
                    startDistSqrA = startAltDistSqrA;
                }

                startPosFound = true;
            }

            var endPosFound = PathManager.FindPathPosition(
                endPos,
                ItemClass.Service.Road,
                NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle,
                VehicleInfo.VehicleType.Car,
                undergroundTarget,
                false,
                32f,
                out var endPosA,
                out var endPosB,
                out var endDistSqrA,
                out _);

            if (PathManager.FindPathPosition(
                endPos,
                ItemClass.Service.PublicTransport,
                NetInfo.LaneType.Vehicle,
                TRAIN_SHIP_PLANE,
                undergroundTarget,
                false,
                32f,
                out var endAltPosA,
                out var endAltPosB,
                out var endAltDistSqrA,
                out _)) {
                if (!endPosFound
                    || (endAltDistSqrA < endDistSqrA
                        && (Mathf.Abs(endPos.x) > 8000f
                            || Mathf.Abs(endPos.z) > 8000f)))
                {
                    endPosA = endAltPosA;
                    endPosB = endAltPosB;
                    endDistSqrA = endAltDistSqrA;
                }

                endPosFound = true;
            }

            if (!startPosFound || !endPosFound) {
                return false;
            }

            var pathMan = CustomPathManager._instance;
            if (!startBothWays || startDistSqrA < 10f) {
                startPosB = default;
            }

            if (!endBothWays || endDistSqrA < 10f) {
                endPosB = default;
            }

            const NetInfo.LaneType LANE_TYPES = NetInfo.LaneType.Vehicle
                                                | NetInfo.LaneType.CargoVehicle;
            const VehicleInfo.VehicleType VEHICLE_TYPES = VehicleInfo.VehicleType.Car
                                             | VehicleInfo.VehicleType.Train
                                             | VehicleInfo.VehicleType.Ship
                                             | VehicleInfo.VehicleType.Plane;

            // NON-STOCK CODE START
            PathCreationArgs args;
            args.extPathType = ExtPathType.None;
            args.extVehicleType = ExtVehicleType.CargoVehicle;
            args.vehicleId = vehicleId;
            args.spawned = (vehicleData.m_flags & Vehicle.Flags.Spawned) != 0;
            args.buildIndex = Singleton<SimulationManager>.instance.m_currentBuildIndex;
            args.startPosA = startPosA;
            args.startPosB = startPosB;
            args.endPosA = endPosA;
            args.endPosB = endPosB;
            args.vehiclePosition = default;
            args.laneTypes = LANE_TYPES;
            args.vehicleTypes = VEHICLE_TYPES;
            args.maxLength = 20000f;
            args.isHeavyVehicle = IsHeavyVehicle();
            args.hasCombustionEngine = CombustionEngine();
            args.ignoreBlocked = IgnoreBlocked(vehicleId, ref vehicleData);
            args.ignoreFlooded = false;
            args.ignoreCosts = false;
            args.randomParking = false;
            args.stablePath = false;
            args.skipQueue = (vehicleData.m_flags & Vehicle.Flags.Spawned) != 0;

            if (pathMan.CustomCreatePath(out var path,
                                         ref Singleton<SimulationManager>.instance.m_randomizer,
                                         args)) {
                // NON-STOCK CODE END
                if (vehicleData.m_path != 0u) {
                    pathMan.ReleasePath(vehicleData.m_path);
                }

                vehicleData.m_path = path;
                vehicleData.m_flags |= Vehicle.Flags.WaitingPath;
                return true;
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [RedirectReverse]
        [UsedImplicitly]
        private void RemoveOffers(ushort vehicleId, ref Vehicle data) {
            Log.Error("CustomCargoTruckAI.RemoveOffers called");
        }
    }
}