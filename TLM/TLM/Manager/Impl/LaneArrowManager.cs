namespace TrafficManager.Manager.Impl {
    using ColossalFramework;
    using CSUtil.Commons;
    using GenericGameBridge.Service;
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
        /// vehcile types for all road vehicles
        /// </summary>
        public const VehicleInfo.VehicleType VEHICLE_TYPES = VehicleInfo.VehicleType.Car;

        public const ExtVehicleType EXT_VEHICLE_TYPES =
            ExtVehicleType.RoadVehicle & ~ExtVehicleType.Emergency;

        public static readonly LaneArrowManager Instance = new LaneArrowManager();

        protected override void InternalPrintDebugInfo() {
            base.InternalPrintDebugInfo();
            Log.NotImpl("InternalPrintDebugInfo for LaneArrowManager");
        }

        /// <summary>
        /// Get the final lane arrows considering both the default lane arrows and user modifications.
        /// </summary>
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
        /// Add arrows to the lane. This will not remove any prevously set flags and
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
                                     out SetLaneArrowError res) {
            if (Flags.ToggleLaneArrowFlags(laneId, startNode, flags, out res)) {
                OnLaneChange(laneId);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Resets lane arrows to their default value for the given segment end.
        /// </summary>
        /// <param name="segmentId">segment to reset</param>
        /// <param name="startNode">determines the segment end to reset. if <c>null</c>
        /// both ends are reset</param>
        public void ResetLaneArrows(ushort segmentId, bool? startNode = null) {
            foreach (var lane in netService.GetSortedLanes(
                segmentId,
                ref GetSeg(segmentId),
                startNode,
                LANE_TYPES,
                VEHICLE_TYPES)) {
                ResetLaneArrows(lane.laneId);
            }
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

        private static void RecalculateFlags(uint laneId) {
            NetLane[] laneBuffer = NetManager.instance.m_lanes.m_buffer;
            ushort segmentId = laneBuffer[laneId].m_segment;
            NetAI ai = GetSeg(segmentId).Info.m_netAI;
#if DEBUGFLAGS
            Log._Debug($"Flags.RecalculateFlags: Recalculateing lane arrows of segment {segmentId}.");
#endif
            ai.UpdateLanes(segmentId, ref GetSeg(segmentId), true);
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
            Flags.ResetSegmentArrowFlags(seg.segmentId);
        }

        protected override void HandleValidSegment(ref ExtSegment seg) { }

        private void ApplyFlags() {
            for (uint laneId = 0; laneId < NetManager.MAX_LANE_COUNT; ++laneId) {
                Flags.ApplyLaneArrowFlags(laneId);
            }
        }

        public override void OnBeforeSaveData() {
            base.OnBeforeSaveData();
            ApplyFlags();
        }

        public override void OnAfterLoadData() {
            base.OnAfterLoadData();
            Flags.ClearHighwayLaneArrows();
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

        /// <summary>
        /// returns the number of all target lanes from input segment toward the secified direction.
        /// </summary>
        private static int CountTargetLanesTowardDirection(ushort segmentId, ushort nodeId, ArrowDirection dir) {
            int count = 0;
            ref NetSegment seg = ref Singleton<NetManager>.instance.m_segments.m_buffer[segmentId];
            bool startNode = seg.m_startNode == nodeId;
            IExtSegmentEndManager segEndMan = Constants.ManagerFactory.ExtSegmentEndManager;
            ExtSegmentEnd segEnd = segEndMan.ExtSegmentEnds[segEndMan.GetIndex(segmentId, startNode)];

            LaneArrowManager.Instance.Services.NetService.IterateNodeSegments(
                nodeId,
                (ushort otherSegmentId, ref NetSegment otherSeg) => {
                    ArrowDirection dir2 = segEndMan.GetDirection(ref segEnd, otherSegmentId);
                    if (dir == dir2) {
                        int forward = 0, backward = 0;
                        otherSeg.CountLanes(
                            otherSegmentId,
                            NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle,
                            VehicleInfo.VehicleType.Car,
                            ref forward,
                            ref backward);
                        bool startNode2 = otherSeg.m_startNode == nodeId;
                            //xor because inverting 2 times is redundant.
                            if (startNode2) {
                            count += forward;
                        } else {
                            count += backward;
                        }
                        Log._Debug(
                            $"dir={dir} startNode={startNode} segmentId={segmentId}\n" +
                            $"startNode2={startNode2} forward={forward} backward={backward} count={count}");
                    }
                    return true;
                });
            return count;
        }

        public static class SeparateTurningLanes{
            /// <summary>
            /// separates turning lanes for all segments attached to nodeId
            /// </summary>
            public static void SeparateNode(ushort nodeId, out SetLaneArrowError res ) {
                NetNode node = Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId];
                if (nodeId == 0) {
                    res = SetLaneArrowError.Invalid;
                    return;
                }
                if ((node.m_flags & NetNode.Flags.Created) == NetNode.Flags.None) {
                    res = SetLaneArrowError.Invalid;
                    return;
                }
                if (LaneConnectionManager.Instance.HasNodeConnections(nodeId)) {
                    res = SetLaneArrowError.LaneConnection;
                    return;
                }
                if (Options.highwayRules && ExtNodeManager.JunctionHasHighwayRules(nodeId)) {
                    res = SetLaneArrowError.HighwayArrows;
                    return;
                }
                res = SetLaneArrowError.Success;

                for (int i = 0; i < 8; i++) {
                    ushort segmentId = Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId].GetSegment(i);
                    if (segmentId == 0) {
                        continue;
                    }

                    SeparateSegmentLanes(segmentId, nodeId, out res);
                }
                Debug.Assert(res == SetLaneArrowError.Success);
            }

            public static SetLaneArrowError CanChangeLanes(ushort segmentId, ushort nodeId) {
                if (segmentId == 0 || nodeId == 0) {
                    return SetLaneArrowError.Invalid;
                }
                if (Options.highwayRules && ExtNodeManager.JunctionHasHighwayRules(nodeId)) {
                    return SetLaneArrowError.HighwayArrows;
                }

                ref NetSegment seg = ref Singleton<NetManager>.instance.m_segments.m_buffer[segmentId];
                bool startNode = seg.m_startNode == nodeId;
                //list of outgoing lanes from current segment to current node.
                IList<LanePos> laneList =
                    Constants.ServiceFactory.NetService.GetSortedLanes(
                        segmentId,
                        ref seg,
                        startNode,
                        LaneArrowManager.LANE_TYPES,
                        LaneArrowManager.VEHICLE_TYPES,
                        true
                        );
                int srcLaneCount = laneList.Count();
                for (int i = 0; i < srcLaneCount; ++i) {
                    if (LaneConnectionManager.Instance.HasConnections(laneList[i].laneId, startNode)) {
                        return SetLaneArrowError.LaneConnection; ;
                    }
                }

                return SetLaneArrowError.Success;
            }

            /// <summary>
            /// separates turning lanes for the input segment on the input node.
            /// </summary>
            public static void SeparateSegmentLanes(ushort segmentId, ushort nodeId, out SetLaneArrowError res) {
                res = CanChangeLanes(segmentId,nodeId);
                if(res != SetLaneArrowError.Success) {
                    return;
                }
                res = SetLaneArrowError.Success;

                ref NetSegment seg = ref Singleton<NetManager>.instance.m_segments.m_buffer[segmentId];
                bool startNode = seg.m_startNode == nodeId;

                //list of outgoing lanes from current segment to current node.
                IList<LanePos> laneList =
                    Constants.ServiceFactory.NetService.GetSortedLanes(
                        segmentId,
                        ref seg,
                        startNode,
                        LaneArrowManager.LANE_TYPES,
                        LaneArrowManager.VEHICLE_TYPES,
                        true
                        );
                int srcLaneCount = laneList.Count();
                if (srcLaneCount <= 1) {
                    return;
                }

                int leftLanesCount = LaneArrowManager.CountTargetLanesTowardDirection(segmentId, nodeId, ArrowDirection.Left);
                int rightLanesCount = LaneArrowManager.CountTargetLanesTowardDirection(segmentId, nodeId, ArrowDirection.Right);
                int forwardLanesCount = LaneArrowManager.CountTargetLanesTowardDirection(segmentId, nodeId, ArrowDirection.Forward);
                int totalLaneCount = leftLanesCount + forwardLanesCount + rightLanesCount;
                int numdirs = Convert.ToInt32(leftLanesCount > 0) + Convert.ToInt32(rightLanesCount > 0) + Convert.ToInt32(forwardLanesCount > 0);

                Log._Debug($"SeparateSegmentLanes: totalLaneCount {totalLaneCount} | numdirs = {numdirs} | outgoingLaneCount = {srcLaneCount}");

                if (numdirs < 2) {
                    return; // no junction
                }
                bool lht = LaneArrowManager.Instance.Services.SimulationService.TrafficDrivesOnLeft;

                if (srcLaneCount == 2 && numdirs == 3) {
                    if (!lht) {
                        LaneArrowManager.Instance.SetLaneArrows(laneList[0].laneId, LaneArrows.LeftForward);
                        LaneArrowManager.Instance.SetLaneArrows(laneList[1].laneId, LaneArrows.Right);
                    }else {
                        LaneArrowManager.Instance.SetLaneArrows(laneList[1].laneId, LaneArrows.ForwardRight);
                        LaneArrowManager.Instance.SetLaneArrows(laneList[0].laneId, LaneArrows.Left);
                    }
                    return;
                }

                int l = 0, f = 0, r = 0;
                if (numdirs == 2) {
                    if (!lht) {
                        //if traffic drives on right, then favour the more difficult left turns.
                        if (leftLanesCount == 0) {
                            DistributeLanes2(srcLaneCount, forwardLanesCount, rightLanesCount, out f, out r);
                        } else if (rightLanesCount == 0) {
                            DistributeLanes2(srcLaneCount, leftLanesCount, forwardLanesCount, out l, out f);
                        } else {
                            //forwarLanesCount == 0
                            DistributeLanes2(srcLaneCount, leftLanesCount, rightLanesCount, out l, out r);
                        }
                    } else {
                        //if traffic drives on left, then favour the more difficult right turns.
                        if (leftLanesCount == 0) {
                            DistributeLanes2(srcLaneCount, rightLanesCount, forwardLanesCount, out r, out f);
                        } else if (rightLanesCount == 0) {
                            DistributeLanes2(srcLaneCount, forwardLanesCount, leftLanesCount, out f, out l);
                        } else {
                            //forwarLanesCount == 0
                            DistributeLanes2(srcLaneCount, rightLanesCount, leftLanesCount, out r, out l);
                        }
                    }
                } else {
                    Debug.Assert(numdirs == 3 && srcLaneCount >= 3);
                    if (!lht) {
                        DistributeLanes3(srcLaneCount, leftLanesCount, forwardLanesCount, rightLanesCount, out l, out f, out r);
                    } else {
                        DistributeLanes3(srcLaneCount, rightLanesCount, forwardLanesCount, leftLanesCount, out r, out f, out l);
                    }

                }
                //assign lanes
                Log._Debug($"SeparateSegmentLanes: leftLanesCount {leftLanesCount} | forwardLanesCount {forwardLanesCount} | rightLanesCount {rightLanesCount}");
                Log._Debug($"SeparateSegmentLanes: l {l} | f {f} | r {r}");

                for (var i = 0; i < laneList.Count; i++) {
                    var flags = (NetLane.Flags)Singleton<NetManager>.instance.m_lanes.m_buffer[laneList[i].laneId].m_flags;

                    LaneArrows arrow = LaneArrows.None;
                    if (i < l) {
                        arrow = LaneArrows.Left;
                    } else if (l <= i && i < l + f) {
                        arrow = LaneArrows.Forward;
                    } else {
                        arrow = LaneArrows.Right;
                    }
                    LaneArrowManager.Instance.SetLaneArrows(laneList[i].laneId, arrow);
                }
            }

            /// <summary>
            /// calculates number of lanes in each direction such that the number of
            /// turning lanes are in porportion to the size of the roads they are turning into
            /// in other words calculate x and y such that a/b = x/y and x+y=total and x>=1 and y>=1.
            /// x is favoured over y (may get more lanes)
            /// </summary>
            /// <param name="total"> number of source lanes</param>
            /// <param name="a">number of target lanes in one direction</param>
            /// <param name="b">number of target lanes in the other direction</param>
            /// <param name="x">number of source lanes turning toward the first direction. </param>
            /// <param name="y">number of the source lanes turning toward the second direction</param>
            private static void DistributeLanes2(int total, int a, int b, out int x, out int y) {
                /* x+y = total
                 * a/b = x/y
                 * y = total*b/(a+b)
                 * x = total - y
                 */
                y = (total * b) / (a + b); //floor y to favour x
                if (y == 0) {
                    y = 1;
                }
                x = total - y;
            }

            /// <summary>
            /// helper function to makes sure at least one source lane is assigned to every direction.
            /// if x has zero lanes, it borrows lanes from y and z such that x+y+z remains the same.
            /// </summary>
            private static int AvoidZero3_Helper(int x, ref int y, ref int z) {
                if (x == 0) {
                    x = 1;
                    if (y > z) {
                        --y;
                    } else {
                        --z;
                    }
                }
                return x;
            }

            /// <summary>
            /// calculates number of lanes in each direction such that the number of
            /// turning lanes are in porportion to the size of the roads they are turning into
            /// x is favoured over y. y is favoured over z.
            /// </summary>
            /// <param name="total"> number of source lanes</param>
            /// <param name="a">number of target lanes leftward </param>
            /// <param name="b">number of target lanes forward</param>
            /// <param name="c">number of target lanes right-ward</param>
            /// <param name="x">number of source lanes leftward. </param>
            /// <param name="y">number of the source lanes forward</param>
            /// <param name="z">number of the source lanes right-ward</param>
            private static void DistributeLanes3(int total, int a, int b, int c, out int x, out int y, out int z) {
                //favour: x then y
                float div = (float)(a + b + c) / (float)total;
                x = (int)Math.Floor((float)a / div);
                y = (int)Math.Floor((float)b / div);
                z = (int)Math.Floor((float)c / div);
                int rem = total - x - y - z;
                switch (rem) {
                    case 3:
                        z++;
                        y++;
                        x++;
                        break;
                    case 2:
                        y++;
                        x++;
                        break;
                    case 1:
                        x++;
                        break;
                    case 0:
                        break;
                    default:
                        Log.Error($"rem = {rem} : expected rem <= 3");
                        break;
                }
                x = AvoidZero3_Helper(x, ref y, ref z);
                y = AvoidZero3_Helper(y, ref x, ref z);
                z = AvoidZero3_Helper(z, ref x, ref y);
            }
        }
    }
}