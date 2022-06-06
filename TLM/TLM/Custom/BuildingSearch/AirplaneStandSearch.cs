namespace TrafficManager.Custom.BuildingSearch {
    using System.Collections.Generic;
    using CSUtil.Commons;
    using TrafficManager.API.Traffic.Data;
    using TrafficManager.Manager.Impl;
    using TrafficManager.Util.Extensions;

    internal class AirplaneStandSearch {
        private static Queue<ushort> _queue = new Queue<ushort>();

        private static ThreadProfiler profiler = new ThreadProfiler();

        /// <summary>
        /// Tries to find better Airplane Stand for Cargo plane traversing segments forward
        /// starting from vehicle current position
        /// </summary>
        /// <param name="extVehicle">ExtVehicle value</param>
        /// <param name="vehicle">Vanilla vehicle value</param>
        /// <returns>Building ID of better airplane stand otherwise 0(zero)</returns>
        internal static ushort FindBetterCargoPlaneStand(ref ExtVehicle extVehicle, ref Vehicle vehicle) {
            if (vehicle.Info.m_vehicleAI is not CargoPlaneAI) {
                //passenger planes supported yet
                return 0;
            }
#if DEBUG
            profiler.BeginStep();
#endif
            PathManager.instance.m_pathUnits.m_buffer[vehicle.m_path].GetPosition(
                vehicle.m_pathPositionIndex >> 1,
                out PathUnit.Position currentPosition);
            PathManager.instance.m_pathUnits.m_buffer[vehicle.m_path].GetNextPosition(
                vehicle.m_pathPositionIndex >> 1,
                out PathUnit.Position nextPosition);

            ushort nextNodeId = currentPosition.m_segment.ToSegment()
                                               .GetSharedNode(nextPosition.m_segment);
            _queue.Clear();
            _queue.Enqueue(nextNodeId);
            ushort buildingID = 0;
            int counter = 0;
            while (_queue.Count > 0 && buildingID == 0) {
                ushort currentNodeId = _queue.Dequeue();

                ref NetNode node = ref currentNodeId.ToNode();
                for (int i = 0; i < 8; i++) {
                    ushort segmentId = node.GetSegment(i);
                    if (segmentId == 0 || segmentId == currentPosition.m_segment) {
                        continue;
                    }

                    ref NetSegment nextSegment = ref segmentId.ToSegment();

                    if (VehicleRestrictionsManager.Instance.IsRunwayNetInfo(nextSegment.Info)) {
                        continue;
                    }

                    bool isNextStartNodeOfNextSegment = nextSegment.m_startNode == currentNodeId;
                    bool nextSegIsInverted = (nextSegment.m_flags & NetSegment.Flags.Invert) !=
                                             NetSegment.Flags.None;
                    NetInfo.Direction nextDir = isNextStartNodeOfNextSegment
                                                    ? NetInfo.Direction.Forward
                                                    : NetInfo.Direction.Backward;
                    NetInfo.Direction forwardDirection = !nextSegIsInverted
                                                             ? nextDir
                                                             : NetInfo.InvertDirection(nextDir);

                    // check direction (assuming we work with one-way segments for now)
                    if ((forwardDirection & NetInfo.Direction.Forward) == 0) {
                        continue;
                    }

                    // check segment traffic data to skip congested segments,
                    // increases threshold when checking further segments - mean speed may increase over time, if not..vehicle will at least move closer :)
                    if (segmentId != currentPosition.m_segment) {
                        int dirIndex = TrafficMeasurementManager.Instance.GetDirIndex(
                            segmentId: segmentId,
                            dir: NetInfo.Direction.Forward);
                        SegmentDirTrafficData segmentDirTrafficData =
                            TrafficMeasurementManager.Instance.SegmentDirTrafficData[dirIndex];
                        int min = 25 - (counter / 4 * 2);
                        if (segmentDirTrafficData.meanSpeed / 100 < min) {
                            continue;
                        }
                    }

                    // check if current segment is "End" - airplane stand stop segment contains flag End and one of its nodes also
                    if ((nextSegment.m_flags & NetSegment.Flags.End) != 0) {
                        ushort ownerBuildingId = NetSegment.FindOwnerBuilding(segmentId, 32f);
                        if (ownerBuildingId != 0) {
                            ref Building building = ref ownerBuildingId.ToBuilding();
                            // is active and correct vehicle level
                            if ((building.m_flags & Building.Flags.Active) != 0 &&
                                building.Info.m_class.m_level == vehicle.Info.m_class.m_level) {

                                if (vehicle.Info.m_vehicleAI.CanSpawnAt(building.m_position)) {
                                    buildingID = ownerBuildingId;
                                }
                            }
                        }
                    }

                    // check lane vehicle restrictions
                    bool allowedDirectionAndVehicleType = false;
                    uint nextLaneIndex = 0;
                    NetInfo nextSegmentInfo = nextSegment.Info;
                    while (nextLaneIndex < nextSegmentInfo.m_lanes.Length) {
                        NetInfo.Lane nextLaneInfo = nextSegmentInfo.m_lanes[nextLaneIndex];

                        nextLaneIndex++;
                        if ((nextLaneInfo.m_vehicleType & VehicleInfo.VehicleType.Plane) == 0) {
                            continue;
                        }

                        if ((nextLaneInfo.m_finalDirection & forwardDirection) != NetInfo.Direction.None &&
                            VehicleRestrictionsManager.Instance.MayUseLane(
                                extVehicle.vehicleType,
                                segmentId,
                                (byte)nextLaneIndex,
                                nextSegmentInfo)) {
                            allowedDirectionAndVehicleType = true;
                            break;
                        }
                    }

                    if (!allowedDirectionAndVehicleType) {
                        continue;
                    }

                    _queue.Enqueue(nextSegment.GetOtherNode(currentNodeId));
                }

                if (counter++ == 40 /* TODO Configurable node count? */) {
                    break;
                }
            }
#if DEBUG
            profiler.EndStep();
            Log.Info($"[Benchmark(FindBetterCargoPlaneStand)] Time: {(profiler.m_lastStepDuration / 1000f):F2}ms, counter: {counter} b: {buildingID}");
#endif
            return buildingID;
        }
    }
}