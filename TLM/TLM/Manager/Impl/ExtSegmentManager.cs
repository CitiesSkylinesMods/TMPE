namespace TrafficManager.Manager.Impl {
    using ColossalFramework;
    using CSUtil.Commons;
    using TrafficManager.API.Manager;
    using TrafficManager.API.Traffic.Data;
    using TrafficManager.State.ConfigData;
    using TrafficManager.Util;

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

        public bool IsValid(ushort segmentId) {
            return Constants.ServiceFactory.NetService.IsSegmentValid(segmentId);
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

            if (!IsValid(segmentId)) {
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
                NetSegment[] segmentsBuffer = Singleton<NetManager>.instance.m_segments.m_buffer;
                Log.Info(
                    $"Recalculated ext. segment {segmentId} (flags={segmentsBuffer[segmentId].m_flags}): " +
                    $"{extSegment}");
            }

            Constants.ManagerFactory.GeometryManager.OnUpdateSegment(ref extSegment);
        }

        public bool CalculateIsOneWay(ushort segmentId) {
            if (!IsValid(segmentId)) {
                return false;
            }

            NetManager instance = Singleton<NetManager>.instance;

            NetInfo info = instance.m_segments.m_buffer[segmentId].Info;

            var hasForward = false;
            var hasBackward = false;

            uint laneId = instance.m_segments.m_buffer[segmentId].m_lanes;
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

                laneId = instance.m_lanes.m_buffer[laneId].m_nextLane;
                laneIndex++;
            }

            return true;
        }

        public bool CalculateHasBusLane(ushort segmentId) {
            if (!IsValid(segmentId)) {
                return false;
            }

            return CalculateHasBusLane(segmentId.ToSegment().Info);
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
            if (!IsValid(segmentId)) {
                return false;
            }

            return CalculateIsHighway(segmentId.ToSegment().Info);
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

        protected override void InternalPrintDebugInfo() {
            base.InternalPrintDebugInfo();
            Log._Debug($"Extended segment data:");

            for (int i = 0; i < ExtSegments.Length; ++i) {
                if (!IsValid((ushort)i)) {
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