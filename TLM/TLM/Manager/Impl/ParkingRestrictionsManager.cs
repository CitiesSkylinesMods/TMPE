namespace TrafficManager.Manager.Impl {
    using CSUtil.Commons;
    using System.Collections.Generic;
    using System;
    using TrafficManager.API.Manager;
    using TrafficManager.API.Traffic.Data;
    using TrafficManager.State.ConfigData;
    using TrafficManager.Util;
    using ColossalFramework;
    using TrafficManager.Util.Extensions;

    public class ParkingRestrictionsManager
        : AbstractGeometryObservingManager,
          ICustomDataManager<List<Configuration.ParkingRestriction>>,
          IParkingRestrictionsManager
    {
        public const NetInfo.LaneType LANE_TYPES = NetInfo.LaneType.Parking;
        public const VehicleInfo.VehicleType VEHICLE_TYPES = VehicleInfo.VehicleType.Car;

        public static readonly ParkingRestrictionsManager Instance = new ParkingRestrictionsManager();

        private bool[][] parkingAllowed;

        private ParkingRestrictionsManager() { }

        public bool MayHaveParkingRestriction(ushort segmentId) {
            ref NetSegment segment = ref segmentId.ToSegment();
            if (!segment.IsValid()) {
                return false;
            }

            ItemClass connectionClass = segment.Info.GetConnectionClass();
            return connectionClass.m_service == ItemClass.Service.Road && segment.Info.m_hasParkingSpaces;
        }

        public bool MayHaveParkingRestriction(ushort segmentId, NetInfo.Direction finalDir) {
            ref NetSegment segment = ref segmentId.ToSegment();
            var dir = Shortcuts.RHT ? finalDir : NetInfo.InvertDirection(finalDir);
            bool right = (dir == NetInfo.Direction.Forward) != segment.m_flags.IsFlagSet(NetSegment.Flags.Invert);
            if (right) {
                if (segment.m_flags.IsFlagSet(NetSegment.Flags.StopRight | NetSegment.Flags.StopRight2)) {
                    return false;
                }
            } else {
                if (segment.m_flags.IsFlagSet(NetSegment.Flags.StopLeft | NetSegment.Flags.StopLeft2)) {
                    return false;
                }
            }
            return MayHaveParkingRestriction(segmentId);
        }

        public bool IsParkingAllowed(ushort segmentId, NetInfo.Direction finalDir) {
            return parkingAllowed[segmentId][GetDirIndex(finalDir)];
        }

        public bool ToggleParkingAllowed(ushort segmentId, NetInfo.Direction finalDir) {
            return SetParkingAllowed(segmentId, finalDir, !IsParkingAllowed(segmentId, finalDir));
        }

        /// <summary>
        /// Sets Parking allowed to <paramref name="flag"/> for all supported directions.
        /// </summary>
        /// <returns><c>false</c>if no there are no configurable lanes.
        /// <c>true</c> if any parking rules were applied.</returns>
        public bool SetParkingAllowed(ushort segmentId, bool flag) {
            bool ret = SetParkingAllowed(segmentId, NetInfo.Direction.Forward, flag);
            ret |= SetParkingAllowed(segmentId, NetInfo.Direction.Backward, flag);
            return ret;
        }

        public bool SetParkingAllowed(ushort segmentId, NetInfo.Direction finalDir, bool flag) {
#if DEBUG
            if (DebugSwitch.BasicParkingAILog.Get()) {
                if (finalDir != NetInfo.Direction.Forward &&
                finalDir != NetInfo.Direction.Backward) {
                    Log.Error($"bad parking direction: {finalDir} expected forward or backward");
                }
                foreach(var lane in segmentId.ToSegment().Info.m_lanes) {
                    if (lane.m_laneType.IsFlagSet(NetInfo.LaneType.Parking) &&
                        lane.m_finalDirection != NetInfo.Direction.Forward &&
                        lane.m_finalDirection != NetInfo.Direction.Backward) {
                        Log.Error($"parking lane with bad m_finalDirection:{lane.m_finalDirection}, expected forward or backward");
                    }
                }
            }
#endif

            if (!MayHaveParkingRestriction(segmentId)) {
                return false;
            }

            int dirIndex = GetDirIndex(finalDir);
            parkingAllowed[segmentId][dirIndex] = flag;

            if (!flag || !parkingAllowed[segmentId][1 - dirIndex]) {
                // force relocation of illegally parked vehicles
                ushort tempSegmentId = segmentId;
                Singleton<SimulationManager>.instance.AddAction(
                    () => tempSegmentId.ToSegment().UpdateSegment(tempSegmentId));
            }
            Notifier.Instance.OnSegmentModified(segmentId, this);
            return true;
        }

        protected override void HandleInvalidSegment(ref ExtSegment seg) {
            parkingAllowed[seg.segmentId][0] = true;
            parkingAllowed[seg.segmentId][1] = true;
        }

        protected override void HandleValidSegment(ref ExtSegment seg) {
            if (!MayHaveParkingRestriction(seg.segmentId)) {
                parkingAllowed[seg.segmentId][0] = true;
                parkingAllowed[seg.segmentId][1] = true;
            }
        }

        protected int GetDirIndex(NetInfo.Direction dir) {
            return dir == NetInfo.Direction.Backward ? 1 : 0;
        }

        public override void OnBeforeLoadData() {
            base.OnBeforeLoadData();

            parkingAllowed = new bool[NetManager.MAX_SEGMENT_COUNT][];
            for (uint segmentId = 0; segmentId < parkingAllowed.Length; ++segmentId) {
                parkingAllowed[segmentId] = new bool[2];

                for (var i = 0; i < 2; ++i) {
                    parkingAllowed[segmentId][i] = true;
                }
            }
        }

        public bool LoadData(List<Configuration.ParkingRestriction> data) {
            bool success = true;
            Log.Info($"Loading parking restrictions data. {data.Count} elements");

            foreach (Configuration.ParkingRestriction restr in data) {
                try {
                    Log._Trace(
                        $"Setting forwardParkingAllowed={restr.forwardParkingAllowed}, " +
                        $"backwardParkingAllowed={restr.backwardParkingAllowed} at segment {restr.segmentId}");

                    SetParkingAllowed(
                        restr.segmentId,
                        NetInfo.Direction.Forward,
                        restr.forwardParkingAllowed);

                    SetParkingAllowed(
                        restr.segmentId,
                        NetInfo.Direction.Backward,
                        restr.backwardParkingAllowed);
                }
                catch (Exception e) {
                    // ignore, as it's probably corrupt save data. it'll be culled on next save
                    Log.Warning("Error loading data from parking restrictions: " + e.ToString());
                    success = false;
                }
            }

            return success;
        }

        public List<Configuration.ParkingRestriction> SaveData(ref bool success) {
            var ret = new List<Configuration.ParkingRestriction>();

            for (uint segmentId = 0; segmentId < parkingAllowed.Length; ++segmentId) {
                try {
                    if (parkingAllowed[segmentId][0] && parkingAllowed[segmentId][1]) {
                        continue;
                    }

                    var restr = new Configuration.ParkingRestriction((ushort)segmentId) {
                        forwardParkingAllowed = parkingAllowed[segmentId][0],
                        backwardParkingAllowed = parkingAllowed[segmentId][1],
                    };

                    Log._Trace(
                        $"Saving forwardParkingAllowed={restr.forwardParkingAllowed}, " +
                        $"backwardParkingAllowed={restr.backwardParkingAllowed} at segment {restr.segmentId}");
                    ret.Add(restr);
                }
                catch (Exception ex) {
                    Log.Error(
                        $"Exception occurred while saving parking restrictions @ {segmentId}: {ex.ToString()}");
                    success = false;
                }
            }

            return ret;
        }
    }
}