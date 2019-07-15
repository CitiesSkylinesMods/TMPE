namespace TrafficManager.Custom.AI {
    using API.Traffic.Data;
    using API.Traffic.Enums;
    using ColossalFramework;
    using Custom.PathFinding;
    using RedirectionFramework.Attributes;
    using Traffic.Data;
    using UnityEngine;

    [TargetType(typeof(ShipAI))]
    public class CustomShipAI : ShipAI {
        [RedirectMethod]
        public bool CustomStartPathFind(ushort vehicleID, ref Vehicle vehicleData, Vector3 startPos, Vector3 endPos, bool startBothWays, bool endBothWays) {
#if DEBUG
            //Log._Debug($"CustomShipAI.CustomStartPathFind called for vehicle {vehicleID}");
#endif

            /// NON-STOCK CODE START ///
            ExtVehicleType vehicleType = vehicleData.Info.m_vehicleAI is PassengerShipAI ? ExtVehicleType.PassengerShip : ExtVehicleType.CargoVehicle;
            /// NON-STOCK CODE END ///

            VehicleInfo info = this.m_info;
            PathUnit.Position startPosA;
            PathUnit.Position startPosB;
            float startSqrDistA;
            float startSqrDistB;
            PathUnit.Position endPosA;
            PathUnit.Position endPosB;
            float endSqrDistA;
            float endSqrDistB;
            if (CustomPathManager.FindPathPosition(startPos, ItemClass.Service.PublicTransport, NetInfo.LaneType.Vehicle, info.m_vehicleType, false, false, 64f, out startPosA, out startPosB, out startSqrDistA, out startSqrDistB) &&
                CustomPathManager.FindPathPosition(endPos, ItemClass.Service.PublicTransport, NetInfo.LaneType.Vehicle, info.m_vehicleType, false, false, 64f, out endPosA, out endPosB, out endSqrDistA, out endSqrDistB)) {
                if (!startBothWays || startSqrDistA < 10f) {
                    startPosB = default(PathUnit.Position);
                }
                if (!endBothWays || endSqrDistA < 10f) {
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

                if (CustomPathManager._instance.CustomCreatePath(out path, ref Singleton<SimulationManager>.instance.m_randomizer, args)) {
                    // NON-STOCK CODE END

                    if (vehicleData.m_path != 0u) {
                        Singleton<PathManager>.instance.ReleasePath(vehicleData.m_path);
                    }
                    vehicleData.m_path = path;
                    vehicleData.m_flags |= Vehicle.Flags.WaitingPath;
                    return true;
                }
            }
            return false;
        }

    }
}