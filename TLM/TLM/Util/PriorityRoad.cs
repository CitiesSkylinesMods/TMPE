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

    public static class PriorityRoad {
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
                ushort nodeId = Constants.ServiceFactory.NetService.GetSegmentNodeId(segmentId, startNode);
                //ref NetNode node = ref Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId];
                FixJunction(nodeId);
            }
            return true;
        }

        private static IExtSegmentEndManager segEndMan = Constants.ManagerFactory.ExtSegmentEndManager;

        private static ref NetSegment GetSeg(ushort segmentId) =>
            ref Singleton<NetManager>.instance.m_segments.m_buffer[segmentId];

        private static ref ExtSegmentEnd GetSegEnd(ushort segmentId, ushort nodeId) =>
            ref segEndMan.ExtSegmentEnds[segEndMan.GetIndex(segmentId, nodeId)];

        private static bool IsStraighOneWay(ushort segmentId0, ushort segmentId1, ushort nodeId) {
            ref NetSegment seg0 = ref GetSeg(segmentId0);
            //ref NetSegment seg1 = ref GetSeg(segmentId1);
            bool ret = ExtSegmentManager.Instance.CalculateIsOneWay(segmentId0) &&
                       ExtSegmentManager.Instance.CalculateIsOneWay(segmentId1);
            if(!ret) {
                return false;
            }

            if (RoundaboutMassEdit.GetHeadNode(segmentId0) == RoundaboutMassEdit.GetTailNode(segmentId1)) {
                if( GetDirection(segmentId0, segmentId1, nodeId) == ArrowDirection.Forward ) {
                    return true;
                }
            }else if (RoundaboutMassEdit.GetHeadNode(segmentId0) == RoundaboutMassEdit.GetTailNode(segmentId1)) {
                if (GetDirection(segmentId1, segmentId0, nodeId) == ArrowDirection.Forward) {
                    return true;
                }
            }

            return false;
        }

        public static void FixJunction(ushort nodeId) {
            if (nodeId == 0) {
                return;
            }
            ref NetNode node = ref Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId];

            // a list of segments attached to node arranged by size
            List<ushort> seglist = new List<ushort>();
            for (int i = 0; i < 8; ++i) {
                ushort segId = node.GetSegment(i);
                if (segId != 0) {
                    seglist.Add(segId);
                }
            }
            if (seglist.Count < 3) {
                // this is not a junctiuon
                return;
            }

            // Handle special case of semi-roundabout.
            bool isSemiRoundAbout = false;
            if (seglist.Count == 3) {
                if(IsStraighOneWay(seglist[0], seglist[1], nodeId)) {
                    isSemiRoundAbout = true;
                } else if(IsStraighOneWay(seglist[1], seglist[2], nodeId)) {
                    ushort temp = seglist[0];
                    seglist[0] = seglist[1];
                    seglist[1] = seglist[2];
                    seglist[2] = temp;
                    isSemiRoundAbout = true;
                } else if (IsStraighOneWay(seglist[0], seglist[2], nodeId)) {
                    ushort temp = seglist[1];
                    seglist[1]  = seglist[2];
                    seglist[2]  = temp;
                    isSemiRoundAbout = true;
                }
            }

            if(!isSemiRoundAbout)
            {
                seglist.Sort(CompareSegments);
                if (CompareSegments(seglist[1], seglist[2]) == 0) {
                    // cannot figure out which road should be treaded as the main road.
                    return;
                }
            }

            // "long turn" is allowed when the main road is oneway.
            bool ignoreLanes =
                ExtSegmentManager.Instance.CalculateIsOneWay(seglist[0]) ||
                ExtSegmentManager.Instance.CalculateIsOneWay(seglist[1]);

            // Turning allowed when the main road is agnled.
            ArrowDirection dir = GetDirection(seglist[0], seglist[1], nodeId);
            ignoreLanes &= dir != ArrowDirection.Forward;

            Debug.Log($"ignorelanes={ignoreLanes}");

            for (int i = 0; i < seglist.Count; ++i) {
                if (i < 2) {
                    FixMajorSegmentRules(seglist[i], nodeId);
                    if(!ignoreLanes) {
                        FixMajorSegmentLanes(seglist[i], nodeId);
                    }
                } else {
                    FixMinorSegmentRules(seglist[i], nodeId);
                    if (!ignoreLanes) {
                        FixMinorSegmentLanes(seglist[i], nodeId, ref seglist);
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
            bool startNode = (bool)Constants.ServiceFactory.NetService.IsStartNode(segmentId, nodeId);
            JunctionRestrictionsManager.Instance.SetEnteringBlockedJunctionAllowed(segmentId, startNode, true);
            if(OptionsMassEditTab.avn_NoCrossMainR.Value) {
                JunctionRestrictionsManager.Instance.SetPedestrianCrossingAllowed(segmentId, startNode, false);
            }
            TrafficPriorityManager.Instance.SetPrioritySign(segmentId, startNode, PriorityType.Main);
        }

        private static void FixMinorSegmentRules(ushort segmentId, ushort nodeId) {
            bool startNode = (bool)Constants.ServiceFactory.NetService.IsStartNode(segmentId, nodeId);
            TrafficPriorityManager.Instance.SetPrioritySign(segmentId, startNode, PriorityType.Yield);
        }

        private static void FixMajorSegmentLanes(ushort segmentId, ushort nodeId) {
            if (LaneArrowManager.SeparateTurningLanes.CanChangeLanes(segmentId, nodeId) != SetLaneArrowError.Success) {
                Debug.Log("cant change lanes");
                return;
            }

            ref NetSegment seg = ref GetSeg(segmentId);
            ref NetNode node = ref Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId];
            bool startNode = (bool)Constants.ServiceFactory.NetService.IsStartNode(segmentId, nodeId);



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
            int srcLaneCount = laneList.Count;

            bool bLeft, bRight, bForward;
            ref ExtSegmentEnd segEnd = ref GetSegEnd(segmentId, nodeId);
            segEndMan.CalculateOutgoingLeftStraightRightSegments(ref segEnd, ref node, out bLeft, out bForward, out bRight);


            for (int i = 0; i < srcLaneCount; ++i) {
                LaneArrowManager.Instance.SetLaneArrows(
                    laneList[i].laneId,
                    LaneArrows.Forward);
            }
            bool lhd = LaneArrowManager.Instance.Services.SimulationService.LeftHandDrive;
            if (!lhd) {
                // RHD: ban left turns at avenue. use FR arrow where applicable.
                if (srcLaneCount > 0 && bRight) {
                    LanePos righMostLane = laneList[laneList.Count - 1];
                    LaneArrowManager.Instance.SetLaneArrows(righMostLane.laneId, LaneArrows.ForwardRight);
                }
            } else {
                // LHD: ban right turns at avenue. use LF arrow where applicable.
                if (srcLaneCount > 0 && bLeft) {
                    LanePos leftMostLane = laneList[0];
                    LaneArrowManager.Instance.SetLaneArrows(leftMostLane.laneId, LaneArrows.LeftForward);
                }
            }
        }

        private static void FixMinorSegmentLanes(ushort segmentId, ushort nodeId, ref List<ushort> segList) {
            if (LaneArrowManager.SeparateTurningLanes.CanChangeLanes(segmentId, nodeId) != SetLaneArrowError.Success) {
                Debug.Log("can't change lanes");
                return;
            }
            ref NetSegment seg = ref GetSeg(segmentId);
            ref NetNode node = ref Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId];
            bool startNode = (bool)Constants.ServiceFactory.NetService.IsStartNode(segmentId, nodeId);

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
