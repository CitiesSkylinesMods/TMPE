namespace TrafficManager.Util {
    using CSUtil.Commons;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using TrafficManager.API.Manager;
    using TrafficManager.API.Traffic.Data;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.Manager.Impl;
    using TrafficManager.Manager.Impl.LaneConnection;
    using TrafficManager.State;
    using TrafficManager.Util.Extensions;
    using UnityEngine;
    using static TrafficManager.Util.Shortcuts;

    public static class SeparateTurningLanesUtil {
        /// <summary>
        /// returns the number of all target lanes from input segment toward the specified direction.
        /// </summary>
        private static int CountTargetLanesTowardDirection(ushort segmentId, ushort nodeId, ArrowDirection dir) {
            int count = 0;
            bool startNode = segmentId.ToSegment().m_startNode == nodeId;
            IExtSegmentEndManager segEndMan = Constants.ManagerFactory.ExtSegmentEndManager;
            ExtSegmentEnd segEnd = segEndMan.ExtSegmentEnds[segEndMan.GetIndex(segmentId, startNode)];

            ref NetNode node = ref nodeId.ToNode();

            for (int segmentIndex = 0; segmentIndex < Constants.MAX_SEGMENTS_OF_NODE; ++segmentIndex) {
                ushort otherSegmentId = node.GetSegment(segmentIndex);
                ref NetSegment otherSeg = ref otherSegmentId.ToSegment();
                if (segmentId != 0) {
                    ArrowDirection dir2 = segEndMan.GetDirection(ref segEnd, otherSegmentId);
                    if (dir == dir2) {
                        int forward = 0, backward = 0;
                        otherSeg.CountLanes(
                            otherSegmentId,
                            LaneArrowManager.LANE_TYPES,
                            LaneArrowManager.VEHICLE_TYPES,
                            VehicleInfo.VehicleCategory.All,
                            ref forward,
                            ref backward);
                        bool startNode2 = otherSeg.m_startNode == nodeId;
                        //xor because inverting 2 times is redundant.
                        if (startNode2) {
                            count += forward;
                        } else {
                            count += backward;
                        }
                    }
                }
            }

            return count;
        }

        private static IList<LanePos> GetBusLanes(ushort segmentId, ushort nodeId) {
            ref NetSegment segment = ref segmentId.ToSegment();

            return segment.GetSortedLanes(
                segment.IsStartNode(nodeId),
                NetInfo.LaneType.TransportVehicle,
                LaneArrowManager.VEHICLE_TYPES,
                sort: false);
        }

        private static LaneArrows Arrows(this LanePos lanePos) =>
            (LaneArrows)lanePos.laneId.ToLane().m_flags & LaneArrows.LeftForwardRight;

        /// <summary>
        /// separates turning lanes for all segments attached to nodeId,
        /// </summary>
        public static void SeparateNode(ushort nodeId, out SetLaneArrow_Result res, bool alternativeMode) {
            if (nodeId == 0) {
                res = SetLaneArrow_Result.Invalid;
                return;
            }

            ref NetNode netNode = ref nodeId.ToNode();
            if ((netNode.m_flags & NetNode.Flags.Created) == NetNode.Flags.None) {
                res = SetLaneArrow_Result.Invalid;
                return;
            }

            if (LaneConnectionManager.Instance.Road.HasNodeConnections(nodeId)) {
                res = SetLaneArrow_Result.LaneConnection;
                return;
            }

            if (SavedGameOptions.Instance.highwayRules && ExtNodeManager.JunctionHasHighwayRules(nodeId)) {
                res = SetLaneArrow_Result.HighwayArrows;
                return;
            }

            res = SetLaneArrow_Result.Success;

            for (int i = 0; i < 8; i++) {
                ushort segmentId = netNode.GetSegment(i);
                if (segmentId == 0) {
                    continue;
                }
                SeparateSegmentLanes(segmentId, nodeId, out res, alternativeMode);
            }

            Debug.Assert(res == SetLaneArrow_Result.Success);
        }

        public static void SeparateSegmentLanes(
            ushort segmentId,
            ushort nodeId,
            out SetLaneArrow_Result res,
            bool alternativeMode = true) {
            bool hasBus = GetBusLanes(segmentId, nodeId).Count > 0;
            SeparateSegmentLanes(
                segmentId,
                nodeId,
                out res,
                builtIn: false,
                alt2: alternativeMode,
                alt3: alternativeMode,
                altBus: !SavedGameOptions.Instance.banRegularTrafficOnBusLanes);
        }

        internal static void SeparateSegmentLanesBuiltIn(
            ushort segmentId,
            ushort nodeId) {
            SeparateSegmentLanes(
                segmentId,
                nodeId,
                out _,
                builtIn: true,
                alt2: true,
                alt3: true,
                altBus: !SavedGameOptions.Instance.banRegularTrafficOnBusLanes);
        }

        /// <summary>
        /// separates turning lanes for the input segment on the input node.
        /// </summary>
        /// <paramref name="builtIn">determines whether to change default or forced lane arrows</paramref>
        /// <param name="alt2">alternative mode for two lanes(true => dedicated far turn)</param>
        /// <param name="alt3">alternative mode for 3+ lanes(true => favor forward)</param>
        /// <param name="altBus">false => treat bus lanes differently</param>
        public static void SeparateSegmentLanes(
            ushort segmentId,
            ushort nodeId,
            out SetLaneArrow_Result res,
            bool builtIn,
            bool alt2,
            bool alt3,
            bool altBus) {

            if (!builtIn) {
                bool startNode = segmentId.ToSegment().IsStartNode(nodeId);
                LaneArrowManager.Instance.ResetLaneArrows(segmentId, startNode);
            }

            var busLanes = GetBusLanes(segmentId, nodeId);
            if (altBus) {
                SeparateSegmentLanes(
                    segmentId,
                    nodeId,
                    out res,
                    LaneArrowManager.LANE_TYPES,
                    LaneArrowManager.VEHICLE_TYPES,
                    builtIn: builtIn,
                    alt2: alt2,
                    alt3: alt3);
            } else {
                if (busLanes.Count == 1 && busLanes[0].Arrows() == LaneArrows.Forward) {
                    // edge case: this optional piece of code improves the lane arrows a little bit in some edge cases.
                    // see https://github.com/CitiesSkylinesMods/TMPE/pull/1104#issuecomment-834145850
                    // no need to have separate code for bus and car. bus lane is already a dedicated forward lane.
                    SeparateSegmentLanes(
                        segmentId,
                        nodeId,
                        out res,
                        LaneArrowManager.LANE_TYPES,
                        LaneArrowManager.VEHICLE_TYPES,
                        builtIn: builtIn,
                        alt2: alt2,
                        alt3: alt3);
                } else {
                    // normal case
                    SeparateSegmentLanes(
                        segmentId,
                        nodeId,
                        out res,
                        NetInfo.LaneType.Vehicle,
                        LaneArrowManager.VEHICLE_TYPES,
                        builtIn: builtIn,
                        alt2: alt2,
                        alt3: alt3);
                    // add forward arrow to bus lanes:
                    AddForwardToLanes(
                            segmentId,
                            nodeId,
                            NetInfo.LaneType.TransportVehicle,
                            LaneArrowManager.VEHICLE_TYPES,
                            builtIn: builtIn);
                }
            }
        }

        /// <summary>
        /// separates turning lanes for the input segment on the input node.
        /// <paramref name="builtIn">determines whether to change default or forced lane arrows</paramref>
        /// <param name="alt2">alternative mode for two lanes(true => dedicated far turn)</param>
        /// <param name="alt3">alternative mode for 3+ lanes(true => favour forward)</param>
        /// </summary>
        private static void SeparateSegmentLanes(
            ushort segmentId,
            ushort nodeId,
            out SetLaneArrow_Result res,
            NetInfo.LaneType laneType,
            VehicleInfo.VehicleType vehicleType,
            bool builtIn,
            bool alt2,
            bool alt3) {
            res = CanChangeLanes(segmentId, nodeId, builtIn);
            if (res != SetLaneArrow_Result.Success) {
                return;
            }

            ref NetSegment seg = ref segmentId.ToSegment();
            bool startNode = seg.m_startNode == nodeId;

            //list of outgoing lanes from current segment to current node.
            var laneList = seg.GetSortedLanes(
                startNode,
                laneType,
                vehicleType,
                reverse: LHT,
                sort: true);

            int srcLaneCount = laneList.Count();
            if (srcLaneCount <= 1) {
                return;
            }

            int nearLanesCount = CountTargetLanesTowardDirection(segmentId, nodeId, ArrowDirection_Near);
            int farLanesCount = CountTargetLanesTowardDirection(segmentId, nodeId, ArrowDirection_Far);
            int forwardLanesCount = CountTargetLanesTowardDirection(segmentId, nodeId, ArrowDirection.Forward);
            int totalLaneCount = nearLanesCount + forwardLanesCount + farLanesCount;
            int numdirs = Convert.ToInt32(nearLanesCount > 0) + Convert.ToInt32(farLanesCount > 0) + Convert.ToInt32(forwardLanesCount > 0);

            Log._Debug($"SeparateSegmentLanes: laneType={laneType} totalLaneCount {totalLaneCount} | numdirs = {numdirs} | outgoingLaneCount = {srcLaneCount}");

            if (numdirs < 2) {
                return; // no junction
            }

            if (srcLaneCount == 2 && numdirs == 3) {
                if (alt2) {
                    SetLaneArrows(laneList[0].laneId, LaneArrows_NearForward, builtIn: builtIn);
                    SetLaneArrows(laneList[1].laneId, LaneArrows_Far, builtIn: builtIn);
                } else {
                    SetLaneArrows(laneList[1].laneId, LaneArrows_FarForward, builtIn: builtIn);
                    SetLaneArrows(laneList[0].laneId, LaneArrows_Near, builtIn: builtIn);
                }
                return;
            }

            int near, forward, far;
            if (!alt3) {
                DistributeLanes(
                    srcLaneCount,
                    farLanesCount,
                    forwardLanesCount,
                    nearLanesCount,
                    out far,
                    out forward,
                    out near);
            } else {
                DistributeLanes(
                    srcLaneCount,
                    forwardLanesCount,
                    farLanesCount,
                    nearLanesCount,
                    out forward,
                    out far,
                    out near);
            }

            Log._Debug($"near=${near} forward={forward} far={far}");
            for (var i = 0; i < laneList.Count; i++) {
                LaneArrows arrow;
                if (i < near) {
                    arrow = LaneArrows_Near;
                } else if (near <= i && i < near + forward) {
                    arrow = LaneArrows.Forward;
                } else {
                    arrow = LaneArrows_Far;
                }
                SetLaneArrows(laneList[i].laneId, arrow, builtIn: builtIn);
            }
        }

        /// <summary>
        /// Adds forward flag to lanes where applicable.
        /// <paramref name="builtIn">determines whether to change default or forced lane arrows</paramref>
        /// </summary>
        private static void AddForwardToLanes(
            ushort segmentId,
            ushort nodeId,
            NetInfo.LaneType laneType,
            VehicleInfo.VehicleType vehicleType,
            bool builtIn) {
            var res = CanChangeLanes(segmentId, nodeId, builtIn);
            if (res != SetLaneArrow_Result.Success) {
                return;
            }

            ref NetSegment seg = ref segmentId.ToSegment();
            bool startNode = seg.m_startNode == nodeId;

            //list of outgoing lanes from current segment to current node.
            var laneList = seg.GetSortedLanes(
                startNode,
                laneType,
                vehicleType,
                reverse: LHT,
                sort: false);

            int forwardLanesCount = CountTargetLanesTowardDirection(segmentId, nodeId, ArrowDirection.Forward);
            if (forwardLanesCount == 0)
                return; // can't go forward.

            foreach(var lanePos in laneList) {
                AppendLaneArrows(lanePos.laneId, LaneArrows.Forward, builtIn);
            }
        }

        private static void SetLaneArrows(uint laneId, LaneArrows arrows, bool builtIn) {
            if (!builtIn)
                LaneArrowManager.Instance.SetLaneArrows(laneId, arrows);
            else {
                NetLane.Flags flags = (NetLane.Flags)laneId.ToLane().m_flags;
                flags &= ~NetLane.Flags.LeftForwardRight; // clear arrows
                flags |= (NetLane.Flags)arrows; // apply flags
                laneId.ToLane().m_flags = (ushort)flags;
            }
        }

        private static void AppendLaneArrows(uint laneId, LaneArrows arrows, bool builtIn) {
            if (!builtIn)
                LaneArrowManager.Instance.AddLaneArrows(laneId, arrows);
            else {
                NetLane.Flags flags = (NetLane.Flags)laneId.ToLane().m_flags;
                flags |= (NetLane.Flags)arrows; // apply flags
                laneId.ToLane().m_flags = (ushort)flags;
            }
        }

        /// <summary>
        /// calculates number of lanes in each direction such that the number of
        /// turning lanes are in proportion to the size of the roads they are turning into
        /// x is favoured over y. y is favoured over z.
        /// This mixes DistributeLanes2 and DistributeLanes3
        /// </summary>
        /// <param name="total"> number of source lanes</param>
        /// <param name="a">number of target lanes in the favoured direction </param>
        /// <param name="b">number of target lanes in the second favourite direction</param>
        /// <param name="c">number of target lanes in the least favourite direction</param>
        /// <param name="x">number of source lanes toward the direction of <paramref name="a"/>.</param>
        /// <param name="y">number of source lanes toward the direction of <paramref name="b"/>.</param>
        /// <param name="z">number of source lanes toward the direction of <paramref name="c"/>.</param>
        private static void DistributeLanes(int total, int a, int b, int c, out int x, out int y, out int z) {
            int numdirs = Convert.ToInt32(a > 0) + Convert.ToInt32(b > 0) + Convert.ToInt32(c > 0);
            if (numdirs == 2) {
                if (a == 0) {
                    DistributeLanes2(total, b, c, out y, out z);
                    x = 0;
                } else if (c == 0) {
                    DistributeLanes2(total, a, b, out x, out y);
                    z = 0;
                } else { //b == 0
                    DistributeLanes2(total, a, c, out x, out z);
                    y = 0;
                }
            } else {
                Debug.Assert(numdirs == 3 && total >= 3);
                DistributeLanes3(total, a, b, c, out x, out y, out z);
            }
            Log._Debug($"DistributeLanes (by priority): {a}=>{x} | {b}=>{y} | {c}=>{z}");
        }

        /// <summary>
        /// calculates number of lanes in each direction such that the number of
        /// turning lanes are in proportion to the size of the roads they are turning into
        /// in other words calculate x and y such that a/b = x/y and x+y=total and x>=1 and y>=1.
        /// x is favoured over y (may get more lanes)
        /// </summary>
        /// <param name="total"> number of source lanes</param>
        /// <param name="a">number of target lanes in one direction</param>
        /// <param name="b">number of target lanes in the other direction</param>
        /// <param name="x">number of source lanes turning toward the direction of <paramref name="a"/></param>
        /// <param name="y">number of the source lanes turning toward the direction of <paramref name="b"/></param>
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
        /// calculates number of lanes in each direction such that the number of
        /// turning lanes are in proportion to the size of the roads they are turning into
        /// x is favoured over y. y is favoured over z.
        /// </summary>
        /// <param name="total"> number of source lanes</param>
        /// <param name="a">number of target lanes in the favoured direction </param>
        /// <param name="b">number of target lanes in the second favourite direction</param>
        /// <param name="c">number of target lanes in the least favourite direction</param>
        /// <param name="x">number of source lanes toward the direction of <paramref name="a"/>.</param>
        /// <param name="y">number of source lanes toward the direction of <paramref name="b"/>.</param>
        /// <param name="z">number of source lanes toward the direction of <paramref name="c"/>.</param>
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

        public static SetLaneArrow_Result CanChangeLanes(ushort segmentId, ushort nodeId, bool builtIn = false) {
            if (segmentId == 0 || nodeId == 0) {
                return SetLaneArrow_Result.Invalid;
            }

            if (builtIn) {
                return SetLaneArrow_Result.Success;
            }

            if (SavedGameOptions.Instance.highwayRules && ExtNodeManager.JunctionHasHighwayRules(nodeId)) {
                return SetLaneArrow_Result.HighwayArrows;
            }

            ref NetSegment netSegment = ref segmentId.ToSegment();
            bool startNode = netSegment.m_startNode == nodeId;

            //list of outgoing lanes from current segment to current node.
            var laneList = netSegment.GetSortedLanes(
                startNode,
                LaneArrowManager.LANE_TYPES,
                LaneArrowManager.VEHICLE_TYPES,
                sort: false);

            int srcLaneCount = laneList.Count();
            for (int i = 0; i < srcLaneCount; ++i) {
                if (LaneConnectionManager.Instance.Road.HasOutgoingConnections(laneList[i].laneId, startNode)) {
                    return SetLaneArrow_Result.LaneConnection;
                }
            }

            return SetLaneArrow_Result.Success;
        }
    }
}
