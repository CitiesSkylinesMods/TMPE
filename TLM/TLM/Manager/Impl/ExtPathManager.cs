namespace TrafficManager.Manager.Impl {
    using System;
    using ColossalFramework;
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
        private Randomizer _randomizer;

        public static readonly ExtPathManager Instance =
            new ExtPathManager(SingletonLite<Spiral>.instance);

        private ExtPathManager(Spiral spiral) {
            _spiral = spiral ?? throw new ArgumentNullException(nameof(spiral));
            _randomizer = new Randomizer();
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
                    maxDistance: SavedGameOptions.Instance.parkingAI
                                     ? GlobalConfig.Instance.ParkingAI.MaxBuildingToPedestrianLaneDistance
                                     : 32f,
                    excludeLaneWidth: false,
                    checkPedestrianStreet: false,
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
                    SavedGameOptions.Instance.parkingAI
                        ? GlobalConfig.Instance.ParkingAI.MaxBuildingToPedestrianLaneDistance
                        : 32f,
                    false,
                    checkPedestrianStreet: true,
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
                    SavedGameOptions.Instance.parkingAI
                        ? GlobalConfig
                          .Instance.ParkingAI.MaxBuildingToPedestrianLaneDistance
                        : 32f,
                    false,
                    checkPedestrianStreet: true,
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
                                                   bool excludeLaneWidth,
                                                   bool checkPedestrianStreet,
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
                excludeLaneWidth,
                checkPedestrianStreet,
                out pathPos);
        }

        public bool FindPathPositionWithSpiralLoop(Vector3 position,
                                                   ItemClass.Service service,
                                                   NetInfo.LaneType laneType,
                                                   VehicleInfo.VehicleType vehicleType,
                                                   VehicleInfo.VehicleCategory vehicleCategory,
                                                   NetInfo.LaneType otherLaneType,
                                                   VehicleInfo.VehicleType otherVehicleType,
                                                   bool allowUnderground,
                                                   bool requireConnect,
                                                   float maxDistance,
                                                   bool excludeLaneWidth,
                                                   bool checkPedestrianStreet,
                                                   out PathUnit.Position pathPos) {
            return FindPathPositionWithSpiralLoop(
                position,
                null,
                service,
                laneType,
                vehicleType,
                vehicleCategory,
                otherLaneType,
                otherVehicleType,
                allowUnderground,
                requireConnect,
                maxDistance,
                excludeLaneWidth,
                checkPedestrianStreet,
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
                                                   bool excludeLaneWidth,
                                                   bool checkPedestrianStreet,
                                                   out PathUnit.Position pathPos) {
            return FindPathPositionWithSpiralLoop(
                position,
                secondaryPosition,
                service,
                laneType,
                vehicleType,
                VehicleInfo.VehicleCategory.All,
                otherLaneType,
                otherVehicleType,
                VehicleInfo.VehicleType.None,
                allowUnderground,
                requireConnect,
                maxDistance,
                excludeLaneWidth,
                checkPedestrianStreet,
                out pathPos,
                out PathUnit.Position _,
                out float _,
                out float _);
        }

        public bool FindPathPositionWithSpiralLoop(Vector3 position,
                                                   Vector3? secondaryPosition,
                                                   ItemClass.Service service,
                                                   NetInfo.LaneType laneType,
                                                   VehicleInfo.VehicleType vehicleType,
                                                   VehicleInfo.VehicleCategory vehicleCategory,
                                                   NetInfo.LaneType otherLaneType,
                                                   VehicleInfo.VehicleType otherVehicleType,
                                                   bool allowUnderground,
                                                   bool requireConnect,
                                                   float maxDistance,
                                                   bool excludeLaneWidth,
                                                   bool checkPedestrianStreet,
                                                   out PathUnit.Position pathPos
            ) {
            return FindPathPositionWithSpiralLoop(
                position,
                secondaryPosition,
                service,
                laneType,
                vehicleType,
                vehicleCategory,
                otherLaneType,
                otherVehicleType,
                VehicleInfo.VehicleType.None,
                allowUnderground,
                requireConnect,
                maxDistance,
                excludeLaneWidth,
                checkPedestrianStreet,
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
                                                   bool excludeLaneWidth,
                                                   bool checkPedestrianStreet,
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
                excludeLaneWidth,
                checkPedestrianStreet,
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
                                                   bool excludeLaneWidth,
                                                   bool checkPedestrianStreet,
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
                VehicleInfo.VehicleCategory.All,
                otherLaneType,
                otherVehicleType,
                VehicleInfo.VehicleType.None,
                allowUnderground,
                requireConnect,
                maxDistance,
                excludeLaneWidth,
                checkPedestrianStreet,
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
                                                   bool excludeLaneWidth,
                                                   bool checkPedestrianStreet,
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
                VehicleInfo.VehicleCategory.All,
                otherLaneType,
                otherVehicleType,
                stopType,
                allowUnderground,
                requireConnect,
                maxDistance,
                excludeLaneWidth,
                checkPedestrianStreet,
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
                                                   VehicleInfo.VehicleCategory vehicleCategory,
                                                   NetInfo.LaneType otherLaneType,
                                                   VehicleInfo.VehicleType otherVehicleType,
                                                   VehicleInfo.VehicleType stopType,
                                                   bool allowUnderground,
                                                   bool requireConnect,
                                                   float maxDistance,
                                                   bool excludeLaneWidth,
                                                   bool checkPedestrianStreet,
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

                if (found && lastSpiralDist > 0 && spiralDist > lastSpiralDist) {
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
                            // STOCK-CODE START
                            if (checkPedestrianStreet && segmentInfo.IsPedestrianZoneOrPublicTransportRoad())
                            {
                                vehicleCategory &= ~segmentInfo.m_vehicleCategories;
                                if ((laneType & NetInfo.LaneType.Pedestrian) != 0)
                                {
                                    laneType &= ~NetInfo.LaneType.Vehicle;
                                    vehicleType &= ~VehicleInfo.VehicleType.Car;
                                }
                            }
                            // STOCK-CODE END

                            if (netSegment.GetClosestLanePosition(
                                position,
                                laneType,
                                vehicleType,
                                vehicleCategory,
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
                                if (excludeLaneWidth)
                                {
                                    dist = Mathf.Max(0f, Mathf.Sqrt(dist) - segmentInfo.m_lanes[laneIndexA].m_width * 0.5f);
                                    dist *= dist;
                                }
                                if (secondaryPosition.HasValue) {
                                    dist += Vector3.SqrMagnitude(secondaryPosition.Value - posA);
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
                                    if (excludeLaneWidth)
                                    {
                                        dist = Mathf.Max(0f, Mathf.Sqrt(dist) - segmentInfo.m_lanes[laneIndexA].m_width * 0.5f);
                                        dist *= dist;
                                    }
                                    if (secondaryPosition.HasValue) {
                                        dist += Vector3.SqrMagnitude(
                                            secondaryPosition.Value - posB);
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

            var coords = _spiral.GetCoordsRandomDirection(radius, ref _randomizer);
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

        // protected override void InternalPrintDebugInfo() {
        //     base.InternalPrintDebugInfo();
        // }
    }
}