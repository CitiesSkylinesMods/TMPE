namespace TrafficManager.Manager.Impl {
    using ColossalFramework;
    using System;
    using API.Traffic.Data;
    using ColossalFramework.Math;
    using State;
    using TrafficManager.API.Manager;
    using TrafficManager.Util;
    using UnityEngine;
    using TrafficManager.Util.Extensions;

    public class ExtPathManager
        : AbstractCustomManager,
          IExtPathManager
    {
        private readonly Spiral _spiral;

        public static readonly ExtPathManager Instance =
            new ExtPathManager(SingletonLite<Spiral>.instance);

        private ExtPathManager(Spiral spiral) {
            _spiral = spiral ?? throw new ArgumentNullException(nameof(spiral));
        }

        /// <summary>
        /// Finds a suitable path position for a walking citizen with the given world position.
        /// If secondary lane constraints are given also checks whether there exists another lane that matches those constraints.
        /// </summary>
        /// <param name="pos">world position</param>
        /// <param name="laneTypes">allowed lane types</param>
        /// <param name="vehicleTypes">allowed vehicle types</param>
        /// <param name="otherLaneTypes">allowed lane types for secondary lane</param>
        /// <param name="otherVehicleTypes">other vehicle types for secondary lane</param>
        /// <param name="allowTransport">public transport allowed?</param>
        /// <param name="allowUnderground">underground position allowed?</param>
        /// <param name="position">resulting path position</param>
        /// <returns><code>true</code> if a position could be found, <code>false</code> otherwise</returns>
        public bool FindCitizenPathPosition(Vector3 pos,
                                                   NetInfo.LaneType laneTypes,
                                                   VehicleInfo.VehicleType vehicleTypes,
                                                   NetInfo.LaneType otherLaneTypes,
                                                   VehicleInfo.VehicleType otherVehicleTypes,
                                                   bool allowTransport,
                                                   bool allowUnderground,
                                                   out PathUnit.Position position) {
            position = default(PathUnit.Position);
            float minDist = 1E+10f;
            if (FindPathPositionWithSpiralLoop(
                    position: pos,
                    service: ItemClass.Service.Road,
                    laneType: laneTypes,
                    vehicleType: vehicleTypes,
                    otherLaneType: otherLaneTypes,
                    otherVehicleType: otherVehicleTypes,
                    allowUnderground: allowUnderground,
                    requireConnect: false,
                    maxDistance: Options.parkingAI
                                     ? GlobalConfig.Instance.ParkingAI.MaxBuildingToPedestrianLaneDistance
                                     : 32f,
                    pathPosA: out PathUnit.Position posA,
                    pathPosB: out _,
                    distanceSqrA: out float distA,
                    distanceSqrB: out _) && distA < minDist) {
                minDist = distA;
                position = posA;
            }

            if (FindPathPositionWithSpiralLoop(
                    pos,
                    ItemClass.Service.Beautification,
                    laneTypes,
                    vehicleTypes,
                    otherLaneTypes,
                    otherVehicleTypes,
                    allowUnderground,
                    false,
                    Options.parkingAI
                        ? GlobalConfig.Instance.ParkingAI.MaxBuildingToPedestrianLaneDistance
                        : 32f,
                    out posA,
                    out _,
                    out distA,
                    out _) && distA < minDist) {
                minDist = distA;
                position = posA;
            }

            if (allowTransport && FindPathPositionWithSpiralLoop(
                    pos,
                    ItemClass.Service.PublicTransport,
                    laneTypes,
                    vehicleTypes,
                    otherLaneTypes,
                    otherVehicleTypes,
                    allowUnderground,
                    false,
                    Options.parkingAI
                        ? GlobalConfig
                          .Instance.ParkingAI.MaxBuildingToPedestrianLaneDistance
                        : 32f,
                    out posA,
                    out _,
                    out distA,
                    out _) && distA < minDist) {
                position = posA;
            }

            return position.m_segment != 0;
        }

        public bool FindPathPositionWithSpiralLoop(Vector3 position,
                                                   ItemClass.Service service,
                                                   NetInfo.LaneType laneType,
                                                   VehicleInfo.VehicleType vehicleType,
                                                   NetInfo.LaneType otherLaneType,
                                                   VehicleInfo.VehicleType otherVehicleType,
                                                   bool allowUnderground,
                                                   bool requireConnect,
                                                   float maxDistance,
                                                   out PathUnit.Position pathPos) {
            return FindPathPositionWithSpiralLoop(
                position,
                null,
                service,
                laneType,
                vehicleType,
                otherLaneType,
                otherVehicleType,
                allowUnderground,
                requireConnect,
                maxDistance,
                out pathPos);
        }

        public bool FindPathPositionWithSpiralLoop(Vector3 position,
                                                   Vector3? secondaryPosition,
                                                   ItemClass.Service service,
                                                   NetInfo.LaneType laneType,
                                                   VehicleInfo.VehicleType vehicleType,
                                                   NetInfo.LaneType otherLaneType,
                                                   VehicleInfo.VehicleType otherVehicleType,
                                                   bool allowUnderground,
                                                   bool requireConnect,
                                                   float maxDistance,
                                                   out PathUnit.Position pathPos) {
            return FindPathPositionWithSpiralLoop(
                position,
                secondaryPosition,
                service,
                laneType,
                vehicleType,
                otherLaneType,
                otherVehicleType,
                VehicleInfo.VehicleType.None,
                allowUnderground,
                requireConnect,
                maxDistance,
                out pathPos,
                out PathUnit.Position _,
                out float _,
                out float _);
        }

        public bool FindPathPositionWithSpiralLoop(Vector3 position,
                                                   ItemClass.Service service,
                                                   NetInfo.LaneType laneType,
                                                   VehicleInfo.VehicleType vehicleType,
                                                   NetInfo.LaneType otherLaneType,
                                                   VehicleInfo.VehicleType otherVehicleType,
                                                   bool allowUnderground,
                                                   bool requireConnect,
                                                   float maxDistance,
                                                   out PathUnit.Position pathPosA,
                                                   out PathUnit.Position pathPosB,
                                                   out float distanceSqrA,
                                                   out float distanceSqrB) {
            return FindPathPositionWithSpiralLoop(
                position,
                null,
                service,
                laneType,
                vehicleType,
                otherLaneType,
                otherVehicleType,
                allowUnderground,
                requireConnect,
                maxDistance,
                out pathPosA,
                out pathPosB,
                out distanceSqrA,
                out distanceSqrB);
        }

        public bool FindPathPositionWithSpiralLoop(Vector3 position,
                                                   Vector3? secondaryPosition,
                                                   ItemClass.Service service,
                                                   NetInfo.LaneType laneType,
                                                   VehicleInfo.VehicleType vehicleType,
                                                   NetInfo.LaneType otherLaneType,
                                                   VehicleInfo.VehicleType otherVehicleType,
                                                   bool allowUnderground,
                                                   bool requireConnect,
                                                   float maxDistance,
                                                   out PathUnit.Position pathPosA,
                                                   out PathUnit.Position pathPosB,
                                                   out float distanceSqrA,
                                                   out float distanceSqrB) {
            return FindPathPositionWithSpiralLoop(
                position,
                secondaryPosition,
                service,
                laneType,
                vehicleType,
                otherLaneType,
                otherVehicleType,
                VehicleInfo.VehicleType.None,
                allowUnderground,
                requireConnect,
                maxDistance,
                out pathPosA,
                out pathPosB,
                out distanceSqrA,
                out distanceSqrB);
        }

        public bool FindPathPositionWithSpiralLoop(Vector3 position,
                                                   ItemClass.Service service,
                                                   NetInfo.LaneType laneType,
                                                   VehicleInfo.VehicleType vehicleType,
                                                   NetInfo.LaneType otherLaneType,
                                                   VehicleInfo.VehicleType otherVehicleType,
                                                   VehicleInfo.VehicleType stopType,
                                                   bool allowUnderground,
                                                   bool requireConnect,
                                                   float maxDistance,
                                                   out PathUnit.Position pathPosA,
                                                   out PathUnit.Position pathPosB,
                                                   out float distanceSqrA,
                                                   out float distanceSqrB) {
            return FindPathPositionWithSpiralLoop(
                position,
                null,
                service,
                laneType,
                vehicleType,
                otherLaneType,
                otherVehicleType,
                stopType,
                allowUnderground,
                requireConnect,
                maxDistance,
                out pathPosA,
                out pathPosB,
                out distanceSqrA,
                out distanceSqrB);
        }

        public bool FindPathPositionWithSpiralLoop(Vector3 position,
                                                   Vector3? secondaryPosition,
                                                   ItemClass.Service service,
                                                   NetInfo.LaneType laneType,
                                                   VehicleInfo.VehicleType vehicleType,
                                                   NetInfo.LaneType otherLaneType,
                                                   VehicleInfo.VehicleType otherVehicleType,
                                                   VehicleInfo.VehicleType stopType,
                                                   bool allowUnderground,
                                                   bool requireConnect,
                                                   float maxDistance,
                                                   out PathUnit.Position pathPosA,
                                                   out PathUnit.Position pathPosB,
                                                   out float distanceSqrA,
                                                   out float distanceSqrB) {
            int iMin = Mathf.Max(
                (int)(((position.z - NetManager.NODEGRID_CELL_SIZE) / NetManager.NODEGRID_CELL_SIZE) +
                      (NetManager.NODEGRID_RESOLUTION / 2f)),
                0);
            int iMax = Mathf.Min(
                (int)(((position.z + NetManager.NODEGRID_CELL_SIZE) / NetManager.NODEGRID_CELL_SIZE) +
                      (NetManager.NODEGRID_RESOLUTION / 2f)),
                NetManager.NODEGRID_RESOLUTION - 1);

            int jMin = Mathf.Max(
                (int)(((position.x - NetManager.NODEGRID_CELL_SIZE) / NetManager.NODEGRID_CELL_SIZE) +
                      (NetManager.NODEGRID_RESOLUTION / 2f)),
                0);
            int jMax = Mathf.Min(
                (int)(((position.x + NetManager.NODEGRID_CELL_SIZE) / NetManager.NODEGRID_CELL_SIZE) +
                      (NetManager.NODEGRID_RESOLUTION / 2f)),
                NetManager.NODEGRID_RESOLUTION - 1);

            int width = iMax - iMin + 1;
            int height = jMax - jMin + 1;

            int centerI = (int)(position.z / NetManager.NODEGRID_CELL_SIZE +
                                NetManager.NODEGRID_RESOLUTION / 2f);
            int centerJ = (int)(position.x / NetManager.NODEGRID_CELL_SIZE +
                                NetManager.NODEGRID_RESOLUTION / 2f);

            int radius = Math.Max(1, (int)(maxDistance / (BuildingManager.BUILDINGGRID_CELL_SIZE / 2f)) + 1);

            NetManager netManager = Singleton<NetManager>.instance;
            /*pathPosA.m_segment = 0;
            pathPosA.m_lane = 0;
            pathPosA.m_offset = 0;*/
            distanceSqrA = 1E+10f;
            /*pathPosB.m_segment = 0;
            pathPosB.m_lane = 0;
            pathPosB.m_offset = 0;*/
            distanceSqrB = 1E+10f;
            float minDist = float.MaxValue;

            PathUnit.Position myPathPosA = default;
            float myDistanceSqrA = float.MaxValue;
            PathUnit.Position myPathPosB = default;
            float myDistanceSqrB = float.MaxValue;

            int lastSpiralDist = 0;
            bool found = false;

            bool FindHelper(int i, int j) {
                if (i < 0 || i >= NetManager.NODEGRID_RESOLUTION
                          || j < 0 || j >= NetManager.NODEGRID_RESOLUTION) {
                    return true;
                }

                int spiralDist = Math.Max(Math.Abs(i - centerI), Math.Abs(j - centerJ)); // maximum norm

                if (found && spiralDist > lastSpiralDist) {
                    // last iteration
                    return false;
                }

                ushort segmentId = netManager.m_segmentGrid[i * NetManager.NODEGRID_RESOLUTION + j];
                int iterations = 0;

                ExtSegmentManager extSegmentManager = ExtSegmentManager.Instance;

                while (segmentId != 0) {
                    ref NetSegment netSegment = ref segmentId.ToSegment();
                    NetInfo segmentInfo = netSegment.Info;

                    if (segmentInfo != null && segmentInfo.m_class.m_service == service &&
                        (netSegment.m_flags & (NetSegment.Flags.Collapsed | NetSegment.Flags.Flooded)) == NetSegment.Flags.None
                        && (allowUnderground || !segmentInfo.m_netAI.IsUnderground()))
                    {
                        bool otherPassed = true;
                        if (otherLaneType != NetInfo.LaneType.None ||
                            otherVehicleType != VehicleInfo.VehicleType.None)
                        {
                            // check if any lane is present that matches the given conditions
                            otherPassed = false;

                            foreach (LaneIdAndIndex laneIdAndIndex in extSegmentManager.GetSegmentLaneIdsAndLaneIndexes(segmentId)) {
                                NetInfo.Lane laneInfo = segmentInfo.m_lanes[laneIdAndIndex.laneIndex];
                                if ((otherLaneType == NetInfo.LaneType.None ||
                                         (laneInfo.m_laneType & otherLaneType) !=
                                         NetInfo.LaneType.None) &&
                                        (otherVehicleType ==
                                         VehicleInfo.VehicleType.None ||
                                         (laneInfo.m_vehicleType & otherVehicleType) !=
                                         VehicleInfo.VehicleType.None)) {
                                    otherPassed = true;
                                    break;
                                }
                            }
                        }

                        if (otherPassed) {
                            if (netSegment.GetClosestLanePosition(
                                position,
                                laneType,
                                vehicleType,
                                stopType,
                                requireConnect,
                                out Vector3 posA,
                                out int laneIndexA,
                                out float laneOffsetA,
                                out Vector3 posB,
                                out int laneIndexB,
                                out float laneOffsetB))
                            {
                                float dist = Vector3.SqrMagnitude(position - posA);
                                if (secondaryPosition != null) {
                                    dist += Vector3.SqrMagnitude((Vector3)secondaryPosition - posA);
                                }

                                if (dist < minDist) {
                                    found = true;

                                    minDist = dist;
                                    myPathPosA.m_segment = segmentId;
                                    myPathPosA.m_lane = (byte)laneIndexA;
                                    myPathPosA.m_offset = (byte)Mathf.Clamp(
                                        Mathf.RoundToInt(laneOffsetA * 255f),
                                        0,
                                        255);
                                    myDistanceSqrA = dist;

                                    dist = Vector3.SqrMagnitude(position - posB);
                                    if (secondaryPosition != null) {
                                        dist += Vector3.SqrMagnitude(
                                            (Vector3)secondaryPosition - posB);
                                    }

                                    if (laneIndexB < 0) {
                                        myPathPosB.m_segment = 0;
                                        myPathPosB.m_lane = 0;
                                        myPathPosB.m_offset = 0;
                                        myDistanceSqrB = float.MaxValue;
                                    } else {
                                        myPathPosB.m_segment = segmentId;
                                        myPathPosB.m_lane = (byte)laneIndexB;
                                        myPathPosB.m_offset = (byte)Mathf.Clamp(
                                            Mathf.RoundToInt(laneOffsetB * 255f),
                                            0,
                                            255);
                                        myDistanceSqrB = dist;
                                    }
                                }
                            } // if GetClosestLanePosition
                        } // if othersPassed
                    } // if

                    segmentId = netSegment.m_nextGridSegment;
                    if (++iterations >= NetManager.MAX_SEGMENT_COUNT) {
                        CODebugBase<LogChannel>.Error(
                            LogChannel.Core,
                            "Invalid list detected!\n" + Environment.StackTrace);
                        break;
                    }
                }

                lastSpiralDist = spiralDist;
                return true;
            }

            var coords = _spiral.GetCoords(radius);
            for (int i = 0; i < radius * radius; i++) {
                if (!FindHelper((int)(centerI + coords[i].x), (int)(centerJ + coords[i].y))) {
                    break;
                }
            }

            pathPosA = myPathPosA;
            distanceSqrA = myDistanceSqrA;
            pathPosB = myPathPosB;
            distanceSqrB = myDistanceSqrB;

            return pathPosA.m_segment != 0;
        }

        /// <summary>
        /// Try recalculating paths of transported cargo trucks
        /// </summary>
        /// <param name="vehicleId">Cargo plane id</param>
        /// <param name="vehicle">Cargo plane vehicle data</param>
        /// <param name="extVehicle">Cargo plane ExtVehicle data</param>
        internal static void RecalculateCargoPlaneCargoTruckPaths(ushort vehicleId, ref Vehicle vehicle, ref ExtVehicle extVehicle) {

            extVehicle.requiresCargoPathRecalculation = false; // reset flag

            VehicleManager instance = VehicleManager.instance;
            ref Building airplaneStand = ref vehicle.m_targetBuilding.ToBuilding();
            int firstCargoVehicleId = vehicle.m_firstCargo;
            int counter = 0;
            uint maxVehicles = instance.m_vehicles.m_size;
            while (firstCargoVehicleId != 0)
            {
                ref Vehicle v = ref instance.m_vehicles.m_buffer[firstCargoVehicleId];
                ushort nextCargo = v.m_nextCargo;
                VehicleInfo info = v.Info;
                if (v.m_path != 0) {
                    if (vehicle.m_targetBuilding != 0 && v.m_targetBuilding != 0 && vehicle.m_targetBuilding != v.m_targetBuilding) {
                            //source building
                            Randomizer randomizer = new Randomizer(firstCargoVehicleId);
                            airplaneStand.Info.m_buildingAI.CalculateSpawnPosition(
                                vehicle.m_targetBuilding,
                                ref airplaneStand,
                                ref randomizer,
                                info,
                                out Vector3 _,
                                out Vector3 dir);
                            //target building
                            ref Building targetBuilding = ref v.m_targetBuilding.ToBuilding();
                            BuildingInfo buildingInfo = targetBuilding.Info;
                            Randomizer randomizer2 = new Randomizer(firstCargoVehicleId);
                            buildingInfo.m_buildingAI.CalculateUnspawnPosition(
                                (ushort)firstCargoVehicleId,
                                ref targetBuilding,
                                ref randomizer2,
                                info,
                                out Vector3 _,
                                out Vector3 dir2);

                            if (FindPathTransportedCargoTruck(dir, dir2, (ushort)firstCargoVehicleId, ref v)) {
                                // Log.Info($"FindPathTransportedCargoTruck success, pos: {pos}, dir: {dir}, target: {dir2} first: {firstCargoVehicleId}");
                            } else {
                                // Log.Info($"FindPathTransportedCargoTruck failure, pos: {pos}, dir: {dir}, target: {dir2} first: {firstCargoVehicleId}");
                            }
                    } else {
                        // Log.Info($"Skipped recalculation {firstCargoVehicleId}: veh_target: {vehicle.m_targetBuilding}, v_target: {v.m_targetBuilding}");
                    }
                } else {
                    // Log.Info($"Attached Cargo Vehicle: {firstCargoVehicleId} to :{vehicleId} had no path...");
                }

                firstCargoVehicleId = nextCargo;
                if (++counter > maxVehicles) {
                    CODebugBase<LogChannel>.Error(
                        LogChannel.Core,
                        $"Invalid list detected! nextCargo: {nextCargo} vehicleId: {vehicleId} vehicleInfo: {vehicle.Info.name} \n" + Environment.StackTrace);
                    break;
                }
            }
        }

        /// <summary>
        /// Slightly modified StartPathFind of CargoTruck
        /// </summary>
        /// <param name="startPos">start position</param>
        /// <param name="endPos">end position</param>
        /// <param name="vehicleID">vehicle id</param>
        /// <param name="vehicleData">vehicle data</param>
        /// <returns>success or fail of swap part of path transported cargo truck</returns>
        private static bool FindPathTransportedCargoTruck(Vector3 startPos,
                                          Vector3 endPos,
                                          ushort vehicleID,
                                          ref Vehicle vehicleData) {
            VehicleInfo info = vehicleData.Info;
            bool allowUnderground = (vehicleData.m_flags & (Vehicle.Flags.Underground | Vehicle.Flags.Transition)) != 0;
            if (PathManager.FindPathPosition(
                    startPos,
                    ItemClass.Service.Road,
                    NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle,
                    info.m_vehicleType,
                    allowUnderground,
                    requireConnect: false,
                    32f,
                    out PathUnit.Position pathPosA,
                    out PathUnit.Position pathPosB,
                    out float distanceSqrA,
                    out float _) &&
                PathManager.FindPathPosition(
                    endPos,
                    ItemClass.Service.Road,
                    NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle,
                    info.m_vehicleType,
                    false,
                    requireConnect: false,
                    32f,
                    out PathUnit.Position pathPosA2,
                    out PathUnit.Position pathPosB2,
                    out float distanceSqrA2,
                    out float _)) {

                if (distanceSqrA < 10f) {
                    pathPosB = default(PathUnit.Position);
                }

                if (distanceSqrA2 < 10f) {
                    pathPosB2 = default(PathUnit.Position);
                }

                if (Singleton<PathManager>.instance.CreatePath(
                    out uint unit,
                    ref Singleton<SimulationManager>.instance.m_randomizer,
                    Singleton<SimulationManager>.instance.m_currentBuildIndex,
                    pathPosA,
                    pathPosB,
                    pathPosA2,
                    pathPosB2,
                    default(PathUnit.Position),
                    NetInfo.LaneType.Vehicle,
                    info.m_vehicleType,
                    20000f,
                    ((CargoTruckAI)info.m_vehicleAI).m_isHeavyVehicle,
                    false,
                    stablePath: false,
                    skipQueue: false,
                    randomParking: false,
                    ignoreFlooded: false,
                    true)) {

                    // get original path unit and "patch it" to jump to new part of the path
                    ref PathUnit originalPathUnit = ref PathManager.instance.m_pathUnits.m_buffer[vehicleData.m_path];
                    int firstIndex = vehicleData.m_pathPositionIndex >> 1;
                    uint nextOriginalPathUnit = originalPathUnit.m_nextPathUnit;
                    if (nextOriginalPathUnit != 0) {
                        // release no longer used, original part of the path (releases all units to the end)
                        PathManager.instance.ReleasePath(nextOriginalPathUnit);
                    }

                    originalPathUnit.m_nextPathUnit = unit;
                    originalPathUnit.m_positionCount = (byte)(firstIndex + 1);
                    return true;
                }
                // Log.Info($"Vehicle: {vehicleID} - CreatePath failed -> pA: {pathPosA} pB: {pathPosB} pA2: {pathPosA2} pB2: {pathPosB2}");
            }

            // Log.Info($"Vehicle: {vehicleID} - FindPathPosition failed -> pA: {pathPosA} pB: {pathPosB} ");
            return false;
        }
    }
}