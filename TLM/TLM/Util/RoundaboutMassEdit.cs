namespace TrafficManager.Util {
    using CSUtil.Commons;
    using GenericGameBridge.Service;
    using static UI.SubTools.LaneConnectorTool;
    using static TrafficManager.Util.Shortcuts;
    using System.Collections.Generic;
    using System;
    using TrafficManager.API.Traffic.Data;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.Manager.Impl;
    using TrafficManager.State;
    using UnityEngine;

    public class RoundaboutMassEdit {
        public static RoundaboutMassEdit Instance = new RoundaboutMassEdit();
        public RoundaboutMassEdit() {
            segmentList_ = new List<ushort>();
        }

        private List<ushort> segmentList_;

        private static void FixLanesRoundabout(ushort segmentId, ushort nextSegmentId) {
            ushort nodeId = netService.GetHeadNode(segmentId);

            if (OptionsMassEditTab.RoundAboutQuickFix_StayInLaneMainR && !HasJunctionFlag(nodeId)) {
                StayInLane(nodeId, StayInLaneMode.Both);
            }

            // allocation of dedicated exit lanes is supported only when the roundabout is round
            // in which case the next segment should be straigh ahead.
            bool isStraight = segEndMan.GetDirection(segmentId, nextSegmentId, nodeId) == ArrowDirection.Forward;

            if (OptionsMassEditTab.RoundAboutQuickFix_DedicatedExitLanes &&
                HasJunctionFlag(nodeId) &&
                SeparateTurningLanesUtil.CanChangeLanes(
                    segmentId, nodeId) == SetLaneArrow_Result.Success &&
                    isStraight) {

                bool startNode = (bool)netService.IsStartNode(segmentId, nodeId);
                IList<LanePos> laneList =
                    netService.GetSortedLanes(
                        segmentId,
                        ref GetSeg(segmentId),
                        startNode,
                        LaneArrowManager.LANE_TYPES,
                        LaneArrowManager.VEHICLE_TYPES,
                        true);
                int nSrc = laneList.Count;

                // check for exits.
                segEndMan.CalculateOutgoingLeftStraightRightSegments(
                    ref GetSegEnd(segmentId, nodeId),
                    ref GetNode(nodeId),
                    out bool bLeft,
                    out bool bForward,
                    out bool bRight);

                //Set one dedicated exit lane per exit - if there are enough lanes that is.
                switch (nSrc) {
                    case 0:
                        Debug.LogAssertion("The road is the wrong way around.");
                        break;
                    case 1:
                        break;// not enough lanes Use default settings.
                    case 2:
                        if (bRight && bLeft) {
                            // not enough lanes, use default settings
                        } else if (bRight) {
                            LaneArrowManager.Instance.SetLaneArrows(laneList[0].laneId, LaneArrows.Forward);
                            LaneArrowManager.Instance.SetLaneArrows(laneList[1].laneId, LaneArrows.Right);
                        } else if (bLeft) {
                            LaneArrowManager.Instance.SetLaneArrows(laneList[0].laneId, LaneArrows.Left);
                            LaneArrowManager.Instance.SetLaneArrows(laneList[1].laneId, LaneArrows.Forward);
                        } else {
                            LaneArrowManager.Instance.SetLaneArrows(laneList[0].laneId, LaneArrows.Forward);
                            LaneArrowManager.Instance.SetLaneArrows(laneList[1].laneId, LaneArrows.Forward);
                        }
                        break;
                    default:
                        for (int i = 0; i < laneList.Count; ++i) {
                            LaneArrowManager.Instance.SetLaneArrows(laneList[i].laneId, LaneArrows.Forward);
                        }
                        if (bRight) {
                            LaneArrowManager.Instance.SetLaneArrows(laneList[nSrc - 1].laneId, LaneArrows.Right);
                        }
                        if (bLeft) {
                            LaneArrowManager.Instance.SetLaneArrows(laneList[0].laneId, LaneArrows.Left);
                        }
                        break;
                } // end switch
            } // end if
        }

        internal static void FixRulesRoundabout(ushort segmentId) {
            foreach (bool startNode in Constants.ALL_BOOL) {
                if (OptionsMassEditTab.RoundAboutQuickFix_PrioritySigns) {
                    TrafficPriorityManager.Instance.SetPrioritySign(
                        segmentId,
                        startNode,
                        PriorityType.Main);
                }

                ushort nodeId = netService.GetSegmentNodeId(
                    segmentId,
                    startNode);

                ExtSegmentEnd curEnd = GetSegEnd(segmentId, startNode);

                if (OptionsMassEditTab.RoundAboutQuickFix_NoCrossMainR) {
                    JunctionRestrictionsManager.Instance.SetPedestrianCrossingAllowed(
                        segmentId,
                        startNode,
                        false);
                }
                JunctionRestrictionsManager.Instance.SetEnteringBlockedJunctionAllowed(
                    segmentId,
                    startNode,
                    true);
            }
        }

        internal static void FixRulesMinor(ushort segmentId, ushort nodeId) {
            bool startNode = (bool)netService.IsStartNode(segmentId, nodeId);
            bool isHighway = ExtNodeManager.JunctionHasOnlyHighwayRoads(nodeId);

            if (OptionsMassEditTab.RoundAboutQuickFix_NoCrossYieldR) {
                JunctionRestrictionsManager.Instance.SetPedestrianCrossingAllowed(
                    segmentId,
                    startNode,
                    false);
            }
            if (OptionsMassEditTab.RoundAboutQuickFix_PrioritySigns) {
                TrafficPriorityManager.Instance.SetPrioritySign(
                    segmentId,
                    startNode,
                    PriorityType.Yield);
            }

            if (isHighway) {
                //ignore highway rules: //TODO remove as part of issue #569
                JunctionRestrictionsManager.Instance.SetLaneChangingAllowedWhenGoingStraight(segmentId, startNode, true);
            } // endif
        }

        private static void FixLanesMinor(ushort segmentId, ushort nodeId) {
            int shortUnit = 4;
            int meterPerUnit = 8;
            ref NetSegment seg = ref GetSeg(segmentId);
            ushort otherNodeId = seg.GetOtherNode(nodeId);

            if (OptionsMassEditTab.RoundAboutQuickFix_StayInLaneNearRabout &&
                !HasJunctionFlag(otherNodeId) &&
                seg.m_averageLength < shortUnit * meterPerUnit) {
                StayInLane(otherNodeId, StayInLaneMode.Both);
            }
        }

        private void FixMinor(ushort nodeId) {
            ref NetNode node = ref GetNode(nodeId);
            for (int i = 0; i < 8; ++i) {
                //find connected segments.
                ushort segmentId = node.GetSegment(i);
                if (segmentId == 0 || segmentList_.Contains(segmentId)) {
                    continue; // continue if it is part of roundabout
                } // end if

                FixRulesMinor(segmentId, nodeId);
                FixLanesMinor(segmentId, nodeId);
            }//end for
        }

        /// <summary>
        /// Fixes the round about or returns false if it is not a round about.
        /// </summary>
        /// <param name="segmentId"></param>
        /// <returns></returns>
        public bool FixRoundabout(ushort initialSegmentId) {
            bool isRoundabout = TraverseLoop(initialSegmentId, out var segList);
            if (!isRoundabout) {
                Log._Debug($"segment {initialSegmentId} not a roundabout.");
                return false;
            }
            int count = segList.Count;
            Log._Debug($"\n segmentId={initialSegmentId} seglist.count={count}\n");

            FixRoundabout(segList);
            return true;
        }

        public void FixRoundabout(List<ushort> segList) {
            if (segList == null)
                return;
            this.segmentList_ = segList;
            int count = segList.Count;
            for (int i = 0; i < count; ++i) {
                ushort segId = segList[i];
                ushort nextSegId = segList[(i + 1) % count];
                FixLanesRoundabout(segId, nextSegId);
                FixRulesRoundabout(segId);
                FixMinor(netService.GetHeadNode(segId));
            }
        }

        /// <summary>
        /// Traverses around a roundabout. At each
        /// traversed segment, the given `visitor` is notified.
        /// </summary>
        /// <param name="initialSegmentGeometry">Specifies the segment at which the traversal
        ///     should start.</param>
        /// <param name="visitorFun">Specifies the stateful visitor that should be notified as soon as
        ///     a traversable segment (which has not been traversed before) is found.
        /// pass null if you are trying to see if segment is part of a round about.
        /// </param>
        /// <returns>true if its a roundabout</returns>
        public bool TraverseLoop(ushort segmentId, out List<ushort> segList) {
            if (segmentList_ != null) {
                this.segmentList_.Clear();
            } else {
                this.segmentList_ = new List<ushort>();
            }
            bool ret;
            if (segmentId == 0 || ! segMan.CalculateIsOneWay(segmentId)) {
                ret = false;
            } else {
                ret = TraverseAroundRecursive(segmentId);
            }
            segList = this.segmentList_;
            return ret;
        }

        public static bool IsRoundabout(List<ushort> segList, bool semi = false) {
            try {
                int n = segList?.Count ?? 0;
                if (n <= 1)
                    return false;
                int lastN = semi ? n - 1 : n;
                for (int i = 0; i < lastN; ++i) {
                    ushort prevSegmentID = segList[i];
                    ushort nextSegmentID = segList[(i + 1) % n];
                    ushort headNodeID = netService.GetHeadNode(prevSegmentID);
                    bool isRoundabout = IsPartofRoundabout(nextSegmentID, prevSegmentID, headNodeID);
                    if (!isRoundabout) {
                        //Log._Debug($"segments {prevSegmentID} and {nextSegmentID} with node:{headNodeID} are not part of a roundabout");
                        return false;
                    }
                }
                return true;
            }
            catch (Exception e){
                Log.Error(e.ToString());
                return false;
            }
        }

        private bool TraverseAroundRecursive(ushort segmentId) {
            if (segmentList_.Count > 20) {
                return false; // too long. prune
            }
            segmentList_.Add(segmentId);
            var segments = GetSortedSegments( segmentId);

            foreach (var nextSegmentId in segments) {
                bool isRoundabout;
                if (nextSegmentId == segmentList_[0]) {
                    isRoundabout = true;
                } else if (Contains(nextSegmentId)) {
                    isRoundabout = false; // try another segment.
                } else {
                    isRoundabout = TraverseAroundRecursive(nextSegmentId);
                }
                if (isRoundabout) {
                    return true;
                } //end if
            }// end foreach
            segmentList_.Remove(segmentId);
            return false;
        }

        /// <summary>
        /// in RHT prefering forward then left then right.
        /// if there are multiple forwards then prefer the leftmost ones.
        /// if there are mutiple lefts then prefer the rightmost ones.
        /// if there are multiple rights then prefer the leftmost ones
        /// LHT is the oposite.
        /// </summary>
        private static List<ushort> GetSortedSegments(ushort segmentId) {
            ushort headNodeId = netService.GetHeadNode(segmentId);
            bool lht = LaneArrowManager.Instance.Services.SimulationService.TrafficDrivesOnLeft;
            var list0 = GetSortedSegmentsHelper( headNodeId, segmentId, ArrowDirection.Forward, !lht);
            var list1 = GetSortedSegmentsHelper( headNodeId, segmentId, ArrowDirection.Left   ,  lht);
            var list2 = GetSortedSegmentsHelper( headNodeId, segmentId, ArrowDirection.Right  , !lht);

            if (lht) {
                list0.AddRange(list1);
                list0.AddRange(list2);
            } else {
                list0.AddRange(list1);
                list0.AddRange(list2);
            }
            return list0;
        }

        private static List<ushort> GetSortedSegmentsHelper(
            ushort headNodeId,
            ushort segmentId,
            ArrowDirection dir,
            bool preferLeft) {
            ArrowDirection preferDir = preferLeft ? ArrowDirection.Left : ArrowDirection.Right;
            List<ushort> sortedSegList = new List<ushort>();

            netService.IterateNodeSegments(
                headNodeId,
                ClockDirection.CounterClockwise,
                (ushort NextSegmentId, ref NetSegment _) => {
                    if (!IsPartofRoundabout(NextSegmentId, segmentId, headNodeId)) {
                        return true;
                    }
                    if (segEndMan.GetDirection(segmentId, NextSegmentId, headNodeId) == dir) {
                        for (int i = 0; i < sortedSegList.Count; ++i) {
                            if (segEndMan.GetDirection(NextSegmentId, sortedSegList[i], headNodeId) == preferDir) {
                                sortedSegList.Insert(i, NextSegmentId);
                                return true;
                            }
                        }
                        sortedSegList.Add(NextSegmentId);
                    }
                    return true;
                });
            return sortedSegList;
        }

        /// <summary>
        /// Checks wheather the next segmentId looks like to be part of a roundabout.
        /// Assumes prevSegmentId is oneway
        /// </summary>
        /// <param name="nextSegmentId"></param>
        /// <param name="prevSegmentId"></param>
        /// <param name="headNodeId">head node for prevSegmentId</param>
        /// <returns></returns>
        private static bool IsPartofRoundabout( ushort nextSegmentId, ushort prevSegmentId, ushort headNodeId) {
            bool ret = nextSegmentId != 0 && nextSegmentId != prevSegmentId;
            ret &= segMan.CalculateIsOneWay(nextSegmentId);
            ret &= headNodeId == netService.GetTailNode(nextSegmentId);
            return ret;
        }

        /// <summary>
        /// returns true if the given segment is attached to the middle of the
        /// path of segmentList_ by checking for duplicate nodes.
        /// </summary>
        private bool Contains(ushort segmentId) {
            ushort nodeId = netService.GetHeadNode(segmentId);
            foreach (ushort segId in segmentList_) {
                if (netService.GetHeadNode(segId) == nodeId) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Clears all rules put by RoundAboutMassEdit.FixRoundabout()
        /// </summary>
        public void ClearNode(ushort nodeId) {
            PriorityRoad.ClearNode(nodeId);
            netService.IterateNodeSegments(nodeId, (ushort segmentId, ref NetSegment seg) => {
                if (!HasJunctionFlag(nodeId)) {
                    // clear stay in lane.
                    LaneConnectionManager.Instance.RemoveLaneConnectionsFromNode(nodeId);
                }
                return true;
            });
        }

        /// <summary>
        /// Clears all rules put by RoundAboutMassEdit.FixRoundabout()
        /// </summary>
        /// <param name="segList"></param>
        public void ClearRoundabout(List<ushort> segList) {
            foreach (ushort segmentId in segList) {
                foreach (bool startNode in Constants.ALL_BOOL) {
                    ushort nodeId = netService.GetSegmentNodeId(segmentId, startNode);
                    ClearNode(nodeId);
                }
            }
        }
    } // end class
}//end namespace