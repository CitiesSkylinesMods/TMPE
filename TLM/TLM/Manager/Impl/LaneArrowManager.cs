namespace TrafficManager.Manager.Impl {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using API.Manager;
    using API.Traffic.Enums;
    using ColossalFramework;
    using CSUtil.Commons;
    using State;
    using Traffic.Data;
    using Traffic.Enums;

    public class LaneArrowManager
        : AbstractGeometryObservingManager,
          ICustomDataManager<List<Configuration.LaneArrowData>>,
          ICustomDataManager<string>, ILaneArrowManager
    {
        public const NetInfo.LaneType LANE_TYPES =
            NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle;

        public const VehicleInfo.VehicleType VEHICLE_TYPES = VehicleInfo.VehicleType.Car;

        public const ExtVehicleType EXT_VEHICLE_TYPES =
            ExtVehicleType.RoadVehicle & ~ExtVehicleType.Emergency;

        public static readonly LaneArrowManager Instance = new LaneArrowManager();

        protected override void InternalPrintDebugInfo() {
            base.InternalPrintDebugInfo();
            Log.NotImpl("InternalPrintDebugInfo for LaneArrowManager");
        }

        public LaneArrows GetFinalLaneArrows(uint laneId) {
            return Flags.getFinalLaneArrowFlags(laneId, true);
        }

        public bool SetLaneArrows(uint laneId,
                                  LaneArrows flags,
                                  bool overrideHighwayArrows = false) {
            if (Flags.setLaneArrowFlags(laneId, flags, overrideHighwayArrows)) {
                OnLaneChange(laneId);
                return true;
            }

            return false;
        }

        public bool ToggleLaneArrows(uint laneId,
                                     bool startNode,
                                     LaneArrows flags,
                                     out SetLaneArrowError res) {
            if (Flags.toggleLaneArrowFlags(laneId, startNode, flags, out res)) {
                OnLaneChange(laneId);
                return true;
            }

            return false;
        }

        private void OnLaneChange(uint laneId) {
            Services.NetService.ProcessLane(
                laneId,
                (uint lId, ref NetLane lane) => {
                    RoutingManager.Instance.RequestRecalculation(lane.m_segment);

                    if (OptionsManager.Instance.MayPublishSegmentChanges()) {
                        Services.NetService.PublishSegmentChanges(lane.m_segment);
                    }

                    return true;
                });
        }

        protected override void HandleInvalidSegment(ref ExtSegment seg) {
            Flags.resetSegmentArrowFlags(seg.segmentId);
        }

        protected override void HandleValidSegment(ref ExtSegment seg) { }

        private void ApplyFlags() {
            for (uint laneId = 0; laneId < NetManager.MAX_LANE_COUNT; ++laneId) {
                Flags.applyLaneArrowFlags(laneId);
            }
        }

        public override void OnBeforeSaveData() {
            base.OnBeforeSaveData();
            ApplyFlags();
        }

        public override void OnAfterLoadData() {
            base.OnAfterLoadData();
            Flags.clearHighwayLaneArrows();
            ApplyFlags();
        }

        [Obsolete]
        public bool LoadData(string data) {
            bool success = true;
            Log.Info($"Loading lane arrow data (old method)");
#if DEBUGLOAD
            Log._Debug($"LaneFlags: {data}");
#endif
            var lanes = data.Split(',');

            if (lanes.Length <= 1) {
                return success;
            }

            foreach (string[] split in lanes.Select(lane => lane.Split(':'))
                                            .Where(split => split.Length > 1)) {
                try {
#if DEBUGLOAD
                    Log._Debug($"Split Data: {split[0]} , {split[1]}");
#endif
                    var laneId = Convert.ToUInt32(split[0]);
                    uint flags = Convert.ToUInt32(split[1]);

                    if (!Services.NetService.IsLaneValid(laneId))
                        continue;

                    if (flags > ushort.MaxValue)
                        continue;

                    uint laneArrowFlags = flags & Flags.lfr;
#if DEBUGLOAD
                    uint origFlags =
                        (Singleton<NetManager>.instance.m_lanes.m_buffer[laneId].m_flags &
                         Flags.lfr);

                    Log._Debug("Setting flags for lane " + laneId + " to " + flags + " (" +
                        ((Flags.LaneArrows)(laneArrowFlags)).ToString() + ")");
                    if ((origFlags | laneArrowFlags) == origFlags) {
                        // only load if setting differs from default
                        Log._Debug("Flags for lane " + laneId + " are original (" +
                            ((NetLane.Flags)(origFlags)).ToString() + ")");
                    }
#endif
                    SetLaneArrows(laneId, (LaneArrows)laneArrowFlags);
                }
                catch (Exception e) {
                    Log.Error(
                        $"Error loading Lane Split data. Length: {split.Length} value: {split}\n" +
                        $"Error: {e}");
                    success = false;
                }
            }

            return success;
        }

        [Obsolete]
        string ICustomDataManager<string>.SaveData(ref bool success) {
            return null;
        }

        public bool LoadData(List<Configuration.LaneArrowData> data) {
            bool success = true;
            Log.Info($"Loading lane arrow data (new method)");

            foreach (Configuration.LaneArrowData laneArrowData in data) {
                try {
                    if (!Services.NetService.IsLaneValid(laneArrowData.laneId)) {
                        continue;
                    }

                    uint laneArrowFlags = laneArrowData.arrows & Flags.lfr;
                    SetLaneArrows(laneArrowData.laneId, (LaneArrows)laneArrowFlags);
                }
                catch (Exception e) {
                    Log.Error(
                        $"Error loading lane arrow data for lane {laneArrowData.laneId}, " +
                        $"arrows={laneArrowData.arrows}: {e}");
                    success = false;
                }
            }

            return success;
        }

        public List<Configuration.LaneArrowData> SaveData(ref bool success) {
            var ret = new List<Configuration.LaneArrowData>();

            for (uint i = 0; i < Singleton<NetManager>.instance.m_lanes.m_buffer.Length; i++) {
                try {
                    LaneArrows? laneArrows = Flags.getLaneArrowFlags(i);

                    if (laneArrows == null) {
                        continue;
                    }

                    uint laneArrowInt = (uint)laneArrows;
#if DEBUGSAVE
                    Log._Debug($"Saving lane arrows for lane {i}, setting to {laneArrows} ({laneArrowInt})");
#endif
                    ret.Add(new Configuration.LaneArrowData(i, laneArrowInt));
                }
                catch (Exception e) {
                    Log.Error($"Exception occurred while saving lane arrows @ {i}: {e}");
                    success = false;
                }
            }

            return ret;
        }
    }
}