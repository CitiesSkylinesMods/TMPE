namespace TrafficManager.Manager.Impl {
    using ColossalFramework;
    using CSUtil.Commons;
    using System;
    using System.Collections.Generic;
    using TrafficManager.API.Manager;
    using TrafficManager.API.Traffic.Data;
    using TrafficManager.State.ConfigData;
    using TrafficManager.Util;
    using TrafficManager.Util.Extensions;
    using TrafficManager.Util.Iterators;

    public class ExtSegmentManager
        : AbstractCustomManager,
          IExtSegmentManager
    {
        static ExtSegmentManager() {
            Instance = new ExtSegmentManager();
        }

        private ExtSegmentManager() {
            ExtSegments = new ExtSegment[NetManager.MAX_SEGMENT_COUNT];

            for (uint i = 0; i < ExtSegments.Length; ++i) {
                ExtSegments[i] = new ExtSegment((ushort)i);
            }
        }

        public static ExtSegmentManager Instance { get; }

        /// <summary>
        /// All additional data for buildings
        /// </summary>
        public ExtSegment[] ExtSegments { get; }

        [Obsolete]
        public ushort GetHeadNode(ushort segmentId) => segmentId.ToSegment().GetHeadNode();

        [Obsolete]
        public ushort GetTailNode(ushort segmentId) => segmentId.ToSegment().GetTailNode();

        public void PublishSegmentChanges(ushort segmentId) {
            Log._Debug($"NetService.PublishSegmentChanges({segmentId}) called.");
            SimulationManager simulationManager = Singleton<SimulationManager>.instance;

            ref NetSegment segment = ref segmentId.ToSegment();
            uint currentBuildIndex = simulationManager.m_currentBuildIndex;
            simulationManager.m_currentBuildIndex = currentBuildIndex + 1;
            segment.m_modifiedIndex = currentBuildIndex;
            ++segment.m_buildIndex;
        }

        private void Reset(ref ExtSegment extSegment) {
            extSegment.Reset();
        }

        public void Recalculate(ushort segmentId) {
            Recalculate(ref ExtSegments[segmentId]);
        }

        private void Recalculate(ref ExtSegment extSegment) {
            IExtSegmentEndManager extSegEndMan = Constants.ManagerFactory.ExtSegmentEndManager;
            ushort segmentId = extSegment.segmentId;

#if DEBUG
            bool logGeometry = DebugSwitch.GeometryDebug.Get();
#else
            const bool logGeometry = false;
#endif
            if (logGeometry) {
                Log._Debug($">>> ExtSegmentManager.Recalculate({segmentId}) called.");
            }

            ref NetSegment netSegment = ref segmentId.ToSegment();
            if (!netSegment.IsValid()) {
                if (extSegment.valid) {
                    Reset(ref extSegment);
                    extSegment.valid = false;

                    extSegEndMan.Recalculate(segmentId);
                    Constants.ManagerFactory.GeometryManager.OnUpdateSegment(ref extSegment);
                }

                return;
            }

            if (logGeometry) {
                Log.Info($"Recalculating geometries of segment {segmentId} STARTED");
            }

            Reset(ref extSegment);
            extSegment.valid = true;

            extSegment.oneWay = CalculateIsOneWay(segmentId);
            extSegment.highway = CalculateIsHighway(segmentId);
            extSegment.buslane = CalculateHasBusLane(segmentId);

            extSegEndMan.Recalculate(segmentId);

            if (logGeometry) {
                Log.Info(
                    $"Recalculated ext. segment {segmentId} (flags={segmentId.ToSegment().m_flags}): " +
                    $"{extSegment}");
            }

            Constants.ManagerFactory.GeometryManager.OnUpdateSegment(ref extSegment);
        }

        public bool CalculateIsOneWay(ushort segmentId) {
            ref NetSegment netSegment = ref segmentId.ToSegment();

            if (!netSegment.IsValid()) {
                return false;
            }

            NetManager instance = Singleton<NetManager>.instance;

            NetInfo info = netSegment.Info;

            var hasForward = false;
            var hasBackward = false;

            uint laneId = netSegment.m_lanes;
            var laneIndex = 0;
            while (laneIndex < info.m_lanes.Length && laneId != 0u) {
                bool validLane =
                    (info.m_lanes[laneIndex].m_laneType &
                     (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) !=
                    NetInfo.LaneType.None &&
                    (info.m_lanes[laneIndex].m_vehicleType &
                     (ExtVehicleManager.VEHICLE_TYPES)) != VehicleInfo.VehicleType.None;

                // TODO the lane types and vehicle types should be specified to make it clear which lanes we need to check
                if (validLane) {
                    if ((info.m_lanes[laneIndex].m_direction & NetInfo.Direction.Forward) !=
                        NetInfo.Direction.None) {
                        hasForward = true;
                    }

                    if ((info.m_lanes[laneIndex].m_direction & NetInfo.Direction.Backward) !=
                        NetInfo.Direction.None) {
                        hasBackward = true;
                    }

                    if (hasForward && hasBackward) {
                        return false;
                    }
                }

                laneId = laneId.ToLane().m_nextLane;
                laneIndex++;
            }

            return true;
        }

        public bool CalculateHasBusLane(ushort segmentId) {
            ref NetSegment netSegment = ref segmentId.ToSegment();

            if (!netSegment.IsValid()) {
                return false;
            }

            return CalculateHasBusLane(netSegment.Info);
        }

        /// <summary>
        /// Calculates if the given segment info describes a segment having a bus lane
        /// </summary>
        /// <param name="segmentInfo"></param>
        /// <returns></returns>
        private bool CalculateHasBusLane(NetInfo segmentInfo) {
            foreach (NetInfo.Lane lane in segmentInfo.m_lanes) {
                if (lane.m_laneType == NetInfo.LaneType.TransportVehicle &&
                    (lane.m_vehicleType & VehicleInfo.VehicleType.Car) != VehicleInfo.VehicleType.None) {
                    return true;
                }
            }

            return false;
        }

        public bool CalculateIsHighway(ushort segmentId) {
            ref NetSegment netSegment = ref segmentId.ToSegment();

            if (!netSegment.IsValid()) {
                return false;
            }

            return CalculateIsHighway(netSegment.Info);
        }

        /// <summary>
        /// Calculates if the given segment info describes a highway segment
        /// </summary>
        /// <param name="segmentInfo"></param>
        /// <returns></returns>
        private bool CalculateIsHighway(NetInfo segmentInfo) {
            return segmentInfo.m_netAI is RoadBaseAI
                   && ((RoadBaseAI)segmentInfo.m_netAI).m_highwayRules;
        }

        [Obsolete]
        public GetSegmentLaneIdsEnumerable GetSegmentLaneIdsAndLaneIndexes(ushort segmentId) =>
            segmentId.ToSegment().GetSegmentLaneIdsAndLaneIndexes();

        [Obsolete]
        public IList<LanePos> GetSortedLanes(ushort segmentId,
                                             ref NetSegment segment,
                                             bool? startNode,
                                             NetInfo.LaneType? laneTypeFilter = null,
                                             VehicleInfo.VehicleType? vehicleTypeFilter = null,
                                             bool reverse = false,
                                             bool sort = true) {
            return segment.GetSortedLanes(startNode, laneTypeFilter, vehicleTypeFilter, reverse, sort);
        }

        protected override void InternalPrintDebugInfo() {
            base.InternalPrintDebugInfo();
            Log._Debug($"Extended segment data:");

            for (int i = 0; i < ExtSegments.Length; ++i) {
                ref NetSegment netSegment = ref ((ushort)i).ToSegment();

                if (!netSegment.IsValid()) {
                    continue;
                }

                Log._Debug($"Segment {i}: {ExtSegments[i]}");
            }
        }

        public override void OnLevelUnloading() {
            base.OnLevelUnloading();

            for (int i = 0; i < ExtSegments.Length; ++i) {
                ExtSegments[i].valid = false;
                Reset(ref ExtSegments[i]);
            }
        }

        public override void OnBeforeLoadData() {
            base.OnBeforeLoadData();
            Log._Debug($"ExtSegmentManager.OnBeforeLoadData: Calculating {ExtSegments.Length} " +
                       "extended segments...");

            for (int i = 0; i < ExtSegments.Length; ++i) {
                Recalculate(ref ExtSegments[i]);
            }

            Log._Debug($"ExtSegmentManager.OnBeforeLoadData: Calculation finished.");
        }
    }
}