namespace TrafficManager.Manager.Impl {
    using ColossalFramework;
    using CSUtil.Commons;
    using System.Collections.Generic;
    using System.Linq;
    using System;
    using TrafficManager.API.Manager;
    using TrafficManager.API.Traffic.Data;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.State;
    using TrafficManager.Util;
    using UnityEngine;
    using static TrafficManager.Util.Shortcuts;
    using TrafficManager.Util.Extensions;
    using TrafficManager.Lifecycle;

    public class LaneArrowManager
        : AbstractGeometryObservingManager,
          ICustomDataManager<List<Configuration.LaneArrowData>>,
          ICustomDataManager<string>, ILaneArrowManager
        {
        /// <summary>
        /// lane types for all road vehicles.
        /// </summary>
        public const NetInfo.LaneType LANE_TYPES =
            NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle;

        /// <summary>
        /// vehicle types for all road vehicles
        /// </summary>
        public const VehicleInfo.VehicleType VEHICLE_TYPES = VehicleInfo.VehicleType.Car;

        public NetInfo.LaneType LaneTypes => LANE_TYPES;

        public VehicleInfo.VehicleType VehicleTypes => VEHICLE_TYPES;


        public const ExtVehicleType EXT_VEHICLE_TYPES =
            ExtVehicleType.RoadVehicle & ~ExtVehicleType.Emergency;

        public static readonly LaneArrowManager Instance = new LaneArrowManager();

        protected override void InternalPrintDebugInfo() {
            base.InternalPrintDebugInfo();
            Log.NotImpl("InternalPrintDebugInfo for LaneArrowManager");
        }

        public LaneArrows GetFinalLaneArrows(uint laneId) {
            return Flags.GetFinalLaneArrowFlags(laneId, true);
        }

        /// <summary>
        /// Set the lane arrows to flags. this will remove all default arrows for the lane
        /// and replace it with user arrows.
        /// default arrows may change as user connects or remove more segments to the junction but
        /// the user arrows stay the same no matter what.
        /// </summary>
        public bool SetLaneArrows(uint laneId,
                                  LaneArrows flags,
                                  bool overrideHighwayArrows = false) {
            if (Flags.SetLaneArrowFlags(laneId, flags, overrideHighwayArrows)) {
                OnLaneChange(laneId);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Add arrows to the lane. This will not remove any previously set flags and
        /// will remove and replace default arrows only where flag is set.
        /// default arrows may change as user connects or remove more segments to the junction but
        /// the user arrows stay the same no matter what.
        /// </summary>
        /// <param name="laneId"></param>
        /// <param name="flags"></param>
        /// <param name="overrideHighwayArrows"></param>
        /// <returns></returns>
        public bool AddLaneArrows(uint laneId,
                                  LaneArrows flags,
                                  bool overrideHighwayArrows = false) {

            LaneArrows flags2 = GetFinalLaneArrows(laneId);
            return SetLaneArrows(laneId, flags | flags2, overrideHighwayArrows);
        }

        /// <summary>
        /// remove arrows (user or default) where flag is set.
        /// default arrows may change as user connects or remove more segments to the junction but
        /// the user arrows stay the same no matter what.
        /// </summary>
        public bool RemoveLaneArrows(uint laneId,
                          LaneArrows flags,
                          bool overrideHighwayArrows = false) {
            LaneArrows flags2 = GetFinalLaneArrows(laneId);
            return SetLaneArrows(laneId, ~flags & flags2, overrideHighwayArrows);
        }

        /// <summary>
        /// Toggles a lane arrows (user or default) on and off for the directions where flag is set.
        /// overrides default settings for the arrows that change.
        /// default arrows may change as user connects or remove more segments to the junction but
        /// the user arrows stay the same no matter what.
        /// </summary>
        public bool ToggleLaneArrows(uint laneId,
                                     bool startNode,
                                     LaneArrows flags,
                                     out SetLaneArrow_Result res) {
            if (Flags.ToggleLaneArrowFlags(laneId, startNode, flags, out res)) {
                OnLaneChange(laneId);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Resets lane arrows to their default value for the given segment end.
        /// </summary>
        /// <param name="segmentId">Segment id.</param>
        /// <param name="startNode">Determines the segment end to reset, or both if <c>null</c>.</param>
        public void ResetLaneArrows(ushort segmentId, bool? startNode = null) {

            ref NetSegment segment = ref segmentId.ToSegment();

            var sortedLanes = segment.GetSortedLanes(startNode, LANE_TYPES, VEHICLE_TYPES, sort: false);

            foreach (var lane in sortedLanes)
                ResetLaneArrows(lane.laneId);
        }

        /// <summary>
        /// Resets lane arrows to their default value for the given lane
        /// </summary>
        public void ResetLaneArrows(uint laneId) {
            if (Flags.ResetLaneArrowFlags(laneId)) {
                RecalculateFlags(laneId);
                OnLaneChange(laneId);
            }
        }

        /// <summary>
        /// Resets lane arrows to their default value for the given node
        /// </summary>
        public void ResetNodeLaneArrows(ushort nodeId) {
            ref NetNode node = ref nodeId.ToNode();
            for (int i = 0; i < 8; ++i) {
                ushort segmentId = node.GetSegment(i);
                bool startNode = segmentId.ToSegment().IsStartNode(nodeId);
                LaneArrowManager.Instance.ResetLaneArrows(segmentId, startNode);
            }
        }

        /// <summary>
        /// Updates all road relevant segments so that the dedicated turning lane policy would take effect.
        /// </summary>
        /// <param name="recalculateRoutings">
        /// also recalculate lane transitions in routing manager.
        /// Current car paths will not be recalculated (to save time). New paths will follow the new lane routings.
        /// </param>
        public void UpdateDedicatedTurningLanePolicy(bool recalculateRoutings) {
            Log.Info($"UpdateDedicatedTurningLanePolicy(recalculateRoutings:{recalculateRoutings}) was called.");
            SimulationManager.instance.AddAction(delegate () {
                try {
                    Log._Debug($"Executing UpdateDedicatedTurningLanePolicy() in simulation thread ...");
                    for (ushort segmentId = 1; segmentId < NetManager.MAX_SEGMENT_COUNT; ++segmentId) {
                        ref NetSegment netSegment = ref segmentId.ToSegment();

                        if (!netSegment.IsValid())
                            continue;

                        if (netSegment.Info?.GetAI() is not RoadBaseAI ai)
                            continue;

                        int forward = 0, backward = 0;
                        segmentId.ToSegment().CountLanes(segmentId, LANE_TYPES, VEHICLE_TYPES, VehicleInfo.VehicleCategory.All,  ref forward, ref backward);
                        if (forward == 1 && backward == 1) {
                            // one lane cannot have dedicated turning lanes.
                            continue;
                        }

                        if (netSegment.m_startNode.ToNode().CountSegments() <= 2 &&
                            netSegment.m_endNode.ToNode().CountSegments() <= 2) {
                            // no intersection.
                            continue;
                        }

                        ai.UpdateLanes(segmentId, ref netSegment, true);
                        NetManager.instance.UpdateSegmentRenderer(segmentId, true);
                        if (recalculateRoutings) {
                            RoutingManager.Instance.RequestRecalculation(segmentId, false);
                        }
                    }
                } catch(Exception ex) {
                    ex.LogException();
                }
            });
        }

        private static void RecalculateFlags(uint laneId) {
            ushort segmentId = laneId.ToLane().m_segment;
            ref NetSegment segment = ref segmentId.ToSegment();
            NetAI ai = segment.Info.m_netAI;
#if DEBUGFLAGS
            Log._Debug($"Flags.RecalculateFlags: Recalculating lane arrows of segment {segmentId}.");
#endif
            ai.UpdateLanes(segmentId, ref segment, true);
        }

        private void OnLaneChange(uint laneId) {
            ushort segment = laneId.ToLane().m_segment;
            RoutingManager.Instance.RequestRecalculation(segment);
            if (TMPELifecycle.Instance.MayPublishSegmentChanges()) {
                ExtSegmentManager.Instance.PublishSegmentChanges(segment);
            }
        }

        protected override void HandleInvalidSegment(ref ExtSegment seg) {
            Flags.ResetSegmentArrowFlags(seg.segmentId);
        }

        protected override void HandleValidSegment(ref ExtSegment seg) { }

        private void ApplyFlags() {
            for (uint laneId = 0; laneId < NetManager.MAX_LANE_COUNT; ++laneId) {
                Flags.ApplyLaneArrowFlags(laneId);
            }
        }

        private void ValidateCustomLaneArrows() {
            for (uint laneId = 0; laneId < NetManager.MAX_LANE_COUNT; ++laneId) {
                Flags.ValidateLaneCustomArrows(laneId);
            }
        }

        public override void OnLevelLoading() {
            base.OnLevelLoading();
            if (SavedGameOptions.Instance.DedicatedTurningLanes) {
                // update dedicated turning lanes after patch has been applied.
                UpdateDedicatedTurningLanePolicy(false);
            }
        }

        public override void OnBeforeSaveData() {
            base.OnBeforeSaveData();
            ApplyFlags();
        }

        public override void OnAfterLoadData() {
            base.OnAfterLoadData();
            Flags.ClearHighwayLaneArrows();
            ValidateCustomLaneArrows();
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
                    ref NetLane netLane = ref laneId.ToLane();

                    uint flags = Convert.ToUInt32(split[1]);

                    if (!netLane.IsValidWithSegment())
                        continue;

                    if (flags > ushort.MaxValue)
                        continue;

                    uint laneArrowFlags = flags & Flags.lfr;
#if DEBUGLOAD
                    uint origFlags = (laneId.ToLane().m_flags & Flags.lfr);

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
                    ref NetLane netLane = ref laneArrowData.laneId.ToLane();

                    if (!netLane.IsValidWithSegment()) {
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
                    LaneArrows? laneArrows = Flags.GetLaneArrowFlags(i);

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

        /// <summary>
        /// Used for loading and saving LaneFlags
        /// </summary>
        /// <returns>ICustomDataManager for lane flags as a string</returns>
        public static ICustomDataManager<string> AsLaneFlagsDM() {
            return Instance;
        }

        /// <summary>
        /// Used for loading and saving lane arrows
        /// </summary>
        /// <returns>ICustomDataManager for lane arrows</returns>
        public static ICustomDataManager<List<Configuration.LaneArrowData>> AsLaneArrowsDM() {
            return Instance;
        }
    }
}