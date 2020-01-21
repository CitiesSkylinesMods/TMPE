namespace TrafficManager.Util {
    using System.Collections.Generic;
    using ColossalFramework;
    using API.Manager;
    using API.Traffic.Data;
    using TrafficManager.Manager.Impl;
    using API.Traffic.Enums;
    using System;
    using GenericGameBridge.Service;
    using UnityEngine;
    using CSUtil.Commons;
    using static TrafficManager.Util.SegmentTraverser;
    using State;
    using System.Linq;
    using static Util.Shortcuts;

    public static class PriorityRoad {

        private static void Swap(List<ushort> list, int i1, int i2) {
            ushort temp = list[i1];
            list[i1] = list[i2];
            list[i2] = temp;
        }

        private static LaneArrows ToLaneArrows(ArrowDirection dir) {
            switch (dir) {
                case ArrowDirection.Forward:
                    return LaneArrows.Forward;
                case ArrowDirection.Left:
                    return LaneArrows.Left;
                case ArrowDirection.Right:
                    return LaneArrows.Right;
                default:
                    return LaneArrows.None;
            }
        }

        public static void FixRoad(ushort initialSegmentId) {
            SegmentTraverser.Traverse(
                initialSegmentId,
                TraverseDirection.AnyDirection,
                TraverseSide.Straight,
                SegmentStopCriterion.None,
                VisitorFunc);
        }

        private static bool VisitorFunc(SegmentVisitData data) {
            ushort segmentId = data.CurSeg.segmentId;
            foreach (bool startNode in Constants.ALL_BOOL) {
                ushort nodeId = netService.GetSegmentNodeId(segmentId, startNode);
                FixJunction(nodeId);
            }
            return true;
        }

        private static bool IsStraighOneWay(ushort segmentId0, ushort segmentId1, ushort nodeId) {
            ref NetSegment seg0 = ref GetSeg(segmentId0);
            //ref NetSegment seg1 = ref GetSeg(segmentId1);
            bool ret = segMan.CalculateIsOneWay(segmentId0) &&
                       segMan.CalculateIsOneWay(segmentId1);
            if(!ret) {
                return false;
            }

            if (netService.GetHeadNode(segmentId0) == netService.GetTailNode(segmentId1)) {
                if( GetDirection(segmentId0, segmentId1, nodeId) == ArrowDirection.Forward ) {
                    return true;
                }
            }else if (netService.GetHeadNode(segmentId0) == netService.GetTailNode(segmentId1)) {
                if (GetDirection(segmentId1, segmentId0, nodeId) == ArrowDirection.Forward) {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// If this is the case of a semi roundabout puts the roundabout roads in the
        /// first two elements of the intput list.
        /// </summary>
        /// <param name="segmentList"></param>
        /// <param name="nodeId"></param>
        /// <returns>true if it is a semi roundabout</returns>
        private static bool ArrangeSemiRabout(List<ushort> segmentList, ushort nodeId) {
            if (segmentList.Count != 3) {
                return false;
            } else if (IsStraighOneWay(segmentList[0], segmentList[1], nodeId)) {
                return true;
            } else if (IsStraighOneWay(segmentList[1], segmentList[2], nodeId)) {
                ushort temp = segmentList[0];
                segmentList[0] = segmentList[1];
                segmentList[1] = segmentList[2];
                segmentList[2] = temp;
                return true;
            } else if (IsStraighOneWay(segmentList[0], segmentList[2], nodeId)) {
                ushort temp = segmentList[1];
                segmentList[1] = segmentList[2];
                segmentList[2] = temp;
                return true;
            }
            return false;
        }

        //TODO move to util or extensions
        public static IList<T> Clone<T>(this IList<T> listToClone) where T : ICloneable {
            return listToClone.Select(item => (T)item.Clone()).ToList();
        }

        /// <summary>
        /// if this is a case of split avenue, arranges the input segment list as follows:
        /// slot 0: incomming oneway road.
        /// slot 1: outgoing oneway road.
        /// slot 2: 2 way raod.
        /// Note: the arrangement of segmentList might be altered regardless of whether this is
        /// a case of SplitAvenue.
        /// </summary>
        /// <param name="segmentList"></param>
        /// <param name="nodeId"></param>
        /// <returns>true if this is a case of split avenue</returns>
        private static bool ArrangeSplitAvenue(List<ushort> segmentList, ushort nodeId) {
            if (segmentList.Count != 3) {
                return false;
            }
            bool oneway0 = segMan.CalculateIsOneWay(segmentList[0]);
            bool oneway1 = segMan.CalculateIsOneWay(segmentList[1]);
            bool oneway2 = segMan.CalculateIsOneWay(segmentList[1]);
            int sum = Int(oneway0) + Int(oneway1) + Int(oneway2);

            // put the avenue in slot 2.
            if (sum != 2) {
                return false;
            } else if (!oneway0) {
                Swap(segmentList, 0, 2);
            } else if (!oneway1) {
                Swap(segmentList, 1, 2);
            }

            // slot 0: incomming road.
            // slot 1: outgoing road.
            if (netService.GetHeadNode(segmentList[1]) == netService.GetTailNode(segmentList[0])) {
                Swap(segmentList, 0, 1);
            }

            return netService.GetHeadNode(segmentList[0]) == netService.GetTailNode(segmentList[1]);
        }

        private static void HandleSplitAvenue(List<ushort> segmentList, ushort nodeId) {
            void SetArrows(ushort segmentIdSrc, ushort segmentIdDst, ushort nodeId) {
                LaneArrows arrow = ToLaneArrows(GetDirection(segmentIdSrc, segmentIdDst, nodeId));
                IList<LanePos> lanes = netService.GetSortedLanes(
                                segmentIdSrc,
                                ref GetSeg(segmentIdSrc),
                                netService.IsStartNode(segmentIdSrc, nodeId),
                                LaneArrowManager.LANE_TYPES,
                                LaneArrowManager.VEHICLE_TYPES,
                                true);

                foreach (LanePos lane in lanes) {
                    LaneArrowManager.Instance.SetLaneArrows(lane.laneId, arrow, true);
                }
            }

            SetArrows(segmentList[0], segmentList[2], nodeId);
            SetArrows(segmentList[2], segmentList[1], nodeId);
            foreach(ushort segmentId in segmentList) {
                FixMajorSegmentRules(segmentId, nodeId);
            }
        }

        public static void FixJunction(ushort nodeId) {
            if (nodeId == 0) {
                return;
            }

            List<ushort> segmentList = new List<ushort>();
            for (int i = 0; i < 8; ++i) {
                ushort segId = GetNode(nodeId).GetSegment(i);
                if (segId != 0) {
                    segmentList.Add(segId);
                }
            }

            if (segmentList.Count < 3) {
                // this is not a junctiuon
                return;
            }

            if (ArrangeSplitAvenue(segmentList, nodeId)) {
                HandleSplitAvenue(segmentList, nodeId);
                return;
            }

            bool isSemiRabout = ArrangeSemiRabout(segmentList, nodeId);

            if(!isSemiRabout) {
                segmentList.Sort(CompareSegments);
                if (CompareSegments(segmentList[1], segmentList[2]) == 0) {
                    // cannot figure out which road should be treaded as the main road.
                    return;
                }
            }

            // "long turn" is allowed when the main road is oneway.
            bool ignoreLanes =
                segMan.CalculateIsOneWay(segmentList[0]) ||
                segMan.CalculateIsOneWay(segmentList[1]);

            // Turning allowed when the main road is agnled.
            ArrowDirection dir = GetDirection(segmentList[0], segmentList[1], nodeId);
            ignoreLanes &= dir != ArrowDirection.Forward;

            //Debug.Log($"ignorelanes={ignoreLanes}");

            for (int i = 0; i < segmentList.Count; ++i) {
                if (i < 2) {
                    FixMajorSegmentRules(segmentList[i], nodeId);
                    if(!ignoreLanes) {
                        FixMajorSegmentLanes(segmentList[i], nodeId);
                    }
                } else {
                    FixMinorSegmentRules(segmentList, segmentList[i], nodeId);
                    if (!ignoreLanes) {
                        FixMinorSegmentLanes(segmentList[i], nodeId, ref segmentList);
                    }
                }
            } //end for
        } // end method

        private static ArrowDirection GetDirection(ushort segmentId, ushort otherSegmentId, ushort nodeId) {
            ref ExtSegmentEnd segEnd = ref GetSegEnd(segmentId, nodeId);
            ArrowDirection dir = segEndMan.GetDirection(ref segEnd, otherSegmentId);
            return dir;
        }

        private static void FixMajorSegmentRules(ushort segmentId, ushort nodeId) {
            bool startNode = (bool)netService.IsStartNode(segmentId, nodeId);
            JunctionRestrictionsManager.Instance.SetEnteringBlockedJunctionAllowed(segmentId, startNode, true);
            if(OptionsMassEditTab.PriorityRoad_NoCrossMainR) {
                JunctionRestrictionsManager.Instance.SetPedestrianCrossingAllowed(segmentId, startNode, false);
            }
            TrafficPriorityManager.Instance.SetPrioritySign(segmentId, startNode, PriorityType.Main);
        }


        private static void FixMinorSegmentRules(List<ushort> segmentList, ushort segmentId, ushort nodeId) {
            bool startNode = (bool)netService.IsStartNode(segmentId, nodeId);
            if(HasAccelerationLane( segmentList, segmentId, nodeId)) {
                JunctionRestrictionsManager.Instance.SetEnteringBlockedJunctionAllowed(segmentId, startNode, true);
            } else {
                TrafficPriorityManager.Instance.SetPrioritySign(segmentId, startNode, PriorityType.Yield);
            }
        }

        //TODO move to ExtSegmentManager
        private static int CountLanes(ushort segmentId, ushort nodeId, bool outgoing = true) {
            return netService.GetSortedLanes(
                                segmentId,
                                ref GetSeg(segmentId),
                                netService.IsStartNode(segmentId, nodeId) ^ (!outgoing),
                                LaneArrowManager.LANE_TYPES,
                                LaneArrowManager.VEHICLE_TYPES,
                                true
                                ).Count;
        }
        private static int CountOutgoingLanes(ushort segmentId, ushort nodeId) => CountLanes(segmentId, nodeId, true);
        private static int CountIncomingLanes(ushort segmentId, ushort nodeId) => CountLanes(segmentId, nodeId, false);


        private static bool HasAccelerationLane(List<ushort> segmentList, ushort segmentId, ushort nodeId) {
            bool lhd = LaneArrowManager.Instance.Services.SimulationService.LeftHandDrive;
            if (!segMan.CalculateIsOneWay(segmentId)) {
                return false;
            }
            bool IsMain(ushort segId) {
                return segId == segmentList[0] || segId == segmentList[1];
            }
            ref NetSegment seg = ref GetSeg(segmentId);

            ushort mainIn, mainOut;
            if (lhd) {
                mainOut = seg.GetLeftSegment(nodeId);
                mainIn = seg.GetRightSegment(nodeId);
            } else {
                mainOut = seg.GetRightSegment(nodeId);
                mainIn = seg.GetLeftSegment(nodeId);
            }

            //Debug.Log($"segmentId:{segmentId} mainOut={mainOut} mainIn={mainIn} ");
            if (IsMain(mainOut) && IsMain(mainIn) ) {
                int Oy = CountOutgoingLanes(segmentId, nodeId);
                int Mo = CountOutgoingLanes(mainOut, nodeId);
                int Mi = CountIncomingLanes(mainIn, nodeId);
                bool ret = Oy > 0 && Oy == Mo - Mi;
                //Debug.Log($"Oy={Oy} Mo={Mo} Mi={Mi} ret={ret} = Oy == Mo - Mi ");
                return ret;
            }

            return false;
        }

        private static void FixMajorSegmentLanes(ushort segmentId, ushort nodeId) {
            if (LaneArrowManager.SeparateTurningLanes.CanChangeLanes(segmentId, nodeId) != SetLaneArrowError.Success) {
                Debug.Log("cant change lanes");
                return;
            }

            ref NetSegment seg = ref GetSeg(segmentId);
            ref NetNode node = ref GetNode(nodeId);
            bool startNode = (bool)netService.IsStartNode(segmentId, nodeId);

            //list of outgoing lanes from current segment to current node.
            IList<LanePos> laneList =
                netService.GetSortedLanes(
                    segmentId,
                    ref seg,
                    startNode,
                    LaneArrowManager.LANE_TYPES,
                    LaneArrowManager.VEHICLE_TYPES,
                    true
                    );
            int srcLaneCount = laneList.Count;

            bool bLeft, bRight, bForward;
            ref ExtSegmentEnd segEnd = ref GetSegEnd(segmentId, nodeId);
            segEndMan.CalculateOutgoingLeftStraightRightSegments(ref segEnd, ref node, out bLeft, out bForward, out bRight);

            bool lhd = LaneArrowManager.Instance.Services.SimulationService.LeftHandDrive;
            LaneArrows arrowShort = lhd ? LaneArrows.Left : LaneArrows.Right;
            LaneArrows arrowLong = lhd ? LaneArrows.Right : LaneArrows.Left;
            for (int i = 0; i < srcLaneCount; ++i) {
                uint laneId = laneList[i].laneId;
                LaneArrows arrows = LaneArrowManager.Instance.GetFinalLaneArrows(laneId);
                LaneArrowManager.Instance.RemoveLaneArrows(
                    laneId,
                    arrowLong);

                if (arrows != arrowShort) {
                    LaneArrowManager.Instance.SetLaneArrows(
                        laneList[i].laneId,
                        LaneArrows.Forward);
                }
            }

            bool bShort = lhd ? bLeft : bRight;
            if (srcLaneCount > 0 && bShort) {
                //TODO LHD righMostLane
                LanePos righMostLane = laneList[laneList.Count - 1];
                LaneArrowManager.Instance.AddLaneArrows(righMostLane.laneId, arrowShort);
            }

        }

        private static void FixMinorSegmentLanes(ushort segmentId, ushort nodeId, ref List<ushort> segList) {
            if (LaneArrowManager.SeparateTurningLanes.CanChangeLanes(segmentId, nodeId) != SetLaneArrowError.Success) {
                Debug.Log("can't change lanes");
                return;
            }
            ref NetSegment seg = ref GetSeg(segmentId);
            ref NetNode node = ref GetNode(nodeId);
            bool startNode = (bool)netService.IsStartNode(segmentId, nodeId);

            //list of outgoing lanes from current segment to current node.
            IList<LanePos> laneList =
                netService.GetSortedLanes(
                    segmentId,
                    ref seg,
                    startNode,
                    LaneArrowManager.LANE_TYPES,
                    LaneArrowManager.VEHICLE_TYPES,
                    true
                    );
            int srcLaneCount = laneList.Count;

            bool bLeft, bRight, bForward;
            ref ExtSegmentEnd segEnd = ref GetSegEnd(segmentId, nodeId);
            segEndMan.CalculateOutgoingLeftStraightRightSegments(ref segEnd, ref node, out bLeft, out bForward, out bRight);

            // LHD vs RHD variables.
            bool lhd = LaneArrowManager.Instance.Services.SimulationService.LeftHandDrive;
            ArrowDirection nearDir = lhd ? ArrowDirection.Left : ArrowDirection.Right;
            LaneArrows nearArrow   = lhd ? LaneArrows.Left     : LaneArrows.Right;
            bool             bnear = lhd ? bLeft               : bRight;
            int sideLaneIndex      = lhd ? srcLaneCount - 1    : 0;

            LaneArrows turnArrow = nearArrow;
            {
                // Check for slight turn into the main road.
                ArrowDirection dir0 = segEndMan.GetDirection(ref segEnd, segList[0]);
                ArrowDirection dir1 = segEndMan.GetDirection(ref segEnd, segList[1]);
                Debug.Assert(dir1 != dir0); // Assume main road is not angled: then dir1 != dir0
                if (dir0 != nearDir && dir1 != nearDir) {
                    turnArrow = LaneArrows.Forward; //slight turn uses forward arrow.
                }
            }

            // only take the near turn into main road.
            for (int i = 0; i < srcLaneCount; ++i) {
                LaneArrowManager.Instance.SetLaneArrows(laneList[i].laneId, turnArrow);
            }

            /* in case there are multiple minor roads attached to the priority road at the same side
             * and the main road is straigh, then add a turn arrow into the other minor roads.
             */
            if(srcLaneCount > 0 && bnear && turnArrow == LaneArrows.Forward) {
                LaneArrowManager.Instance.AddLaneArrows( //TODO test
                    laneList[sideLaneIndex].laneId,
                    nearArrow);
            }
        }

        private static int CompareSegments(ushort seg1Id, ushort seg2Id) {
            ref NetSegment seg1 = ref GetSeg(seg1Id);
            ref NetSegment seg2 = ref GetSeg(seg2Id);
            int diff = (int)Math.Ceiling(seg2.Info.m_halfWidth - seg1.Info.m_halfWidth);
            if (diff == 0) {
                diff = CountCarLanes(seg2Id) - CountCarLanes(seg1Id);
            }
            return diff;
        }

        private static int CountCarLanes(ushort segmentId) {
            ref NetSegment segment = ref GetSeg(segmentId);
            int forward = 0, backward = 0;
            segment.CountLanes(
                segmentId,
                        NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle,
                        VehicleInfo.VehicleType.Car,
                        ref forward,
                        ref backward);
            return forward + backward;
        }
    } //end class
}
