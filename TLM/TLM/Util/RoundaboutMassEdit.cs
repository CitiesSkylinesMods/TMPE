namespace TrafficManager.Util {
    using TrafficManager.UI.SubTools;
    using System.Collections.Generic;
    using UnityEngine;
    using API.Traffic.Data;
    using CSUtil.Commons;
    using TrafficManager.Manager.Impl;
    using API.Traffic.Enums;
    using GenericGameBridge.Service;
    using State;
    using static Util.Shortcuts;

    public class RoundaboutMassEdit {
        public static RoundaboutMassEdit Instance = new RoundaboutMassEdit();
        public RoundaboutMassEdit() {
            segmentList = new List<ushort>();
        }

        private List<ushort> segmentList = null;

        public static void FixLanesRAbout(ushort segmentId) {
            ushort nodeId = netService.GetHeadNode(segmentId);

            if (OptionsMassEditTab.rabout_StayInLaneMainR && !HasJunctionFlag(nodeId) ) {
                LaneConnectorTool.StayInLane(nodeId, LaneConnectorTool.StayInLaneMode.Both);
            }

            if (OptionsMassEditTab.rabout_DedicatedExitLanes &&
                HasJunctionFlag(nodeId) &&
                LaneArrowManager.SeparateTurningLanes.CanChangeLanes(segmentId, nodeId) == SetLaneArrowError.Success) {
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

        private static void FixRulesRAbout(ushort segmentId) {
            foreach (bool startNode in Constants.ALL_BOOL) {
                if (OptionsMassEditTab.rabout_PrioritySigns) {
                    TrafficPriorityManager.Instance.SetPrioritySign(
                        segmentId,
                        startNode,
                        PriorityType.Main);
                }

                ushort nodeId = netService.GetSegmentNodeId(
                    segmentId,
                    startNode);

                ExtSegmentEnd curEnd = GetSegEnd(segmentId, startNode);

                if (OptionsMassEditTab.rabout_NoCrossMainR) {
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

        private static void FixRulesMinor(ushort segmentId, ushort nodeId) {
            bool startNode = (bool)netService.IsStartNode(segmentId, nodeId);
            bool isHighway = ExtNodeManager.IsHighwayJunction(nodeId);

            if (OptionsMassEditTab.rabout_NoCrossYeildR) {
                JunctionRestrictionsManager.Instance.SetPedestrianCrossingAllowed(
                    segmentId,
                    startNode,
                    false);
            }
            if (OptionsMassEditTab.rabout_PrioritySigns) {
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
            //Debug.Log($"segment={segmentId} node={nodeId} len={seg.m_averageLength}");

            if (OptionsMassEditTab.rabout_StayInLaneNearRabout &&
                !HasJunctionFlag(otherNodeId) &&
                seg.m_averageLength < shortUnit * meterPerUnit) {
                //Debug.Log($"segment={segmentId} node={nodeId} len={seg.m_averageLength} otherNodeId={otherNodeId}");
                LaneConnectorTool.StayInLane(otherNodeId, LaneConnectorTool.StayInLaneMode.Both);
            }
        }

        private void FixMinor(ushort nodeId) {
            ref NetNode node = ref GetNode(nodeId);
            for (int i = 0; i < 8; ++i) {
                //find connected segments.
                ushort segmentId = node.GetSegment(i);
                if (segmentId == 0 || segmentList.Contains(segmentId)) {
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
        public bool FixRabout(ushort initialSegmentId) {
            bool isRAbout = TraverseLoop(initialSegmentId, out _);
            if (!isRAbout) {
                Debug.Log($"segment {initialSegmentId} not a roundabout.");
                return false;
            }
            int count = segmentList.Count;
            string m = $"\n segmentId={initialSegmentId} seglist.count={count}\n";

            for (int i = 0; i < count; ++i) {
                ushort segId = segmentList[i];
                //Debug.Log($"{i} : {segId}");
                FixLanesRAbout(segId);
                FixRulesRAbout(segId);
                FixMinor(netService.GetHeadNode(segId));
            }
            return true;
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
            this.segmentList.Clear();
            bool ret;
            if (segmentId == 0 || ! segMan.CalculateIsOneWay(segmentId)) {
                ret = false;
            } else {
                ret = TraverseAroundRecursive(segmentId);
            }
            segList = this.segmentList;
            return ret;
        }

        private bool TraverseAroundRecursive(ushort segmentId) {
            if (segmentList.Count > 20) {
                return false; // too long. prune
            }
            segmentList.Add(segmentId);
            //Debug.Log($"\nTraverseAroundRecursive({segmentId}) ");
            var segments = GetSortedSegments( segmentId);

            foreach (var nextSegmentId in segments) {
                bool isRAbout = false;
                if (nextSegmentId == segmentList[0]) {
                    isRAbout = true;
                } else if (segmentList.Contains(nextSegmentId)) {
                    isRAbout = false;
                } else {
                    isRAbout = TraverseAroundRecursive(nextSegmentId);
                }
                if (isRAbout) {
                    return true;
                } //end if
            }// end foreach
            segmentList.Remove(segmentId);
            return false;
        }

        private static List<ushort> GetSortedSegments(ushort segmentId) {
            ushort headNodeId = netService.GetHeadNode(segmentId);
            bool lhd = LaneArrowManager.Instance.Services.SimulationService.LeftHandDrive;
            var list0 = GetSortedSegmentsHelper( headNodeId, segmentId, ArrowDirection.Forward, !lhd);
            var list1 = GetSortedSegmentsHelper( headNodeId, segmentId, ArrowDirection.Left   ,  lhd);
            var list2 = GetSortedSegmentsHelper( headNodeId, segmentId, ArrowDirection.Right  , !lhd);

            list0.AddRange(list1);
            list0.AddRange(list2);
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
                    if (GetDirection(segmentId, NextSegmentId, headNodeId) == dir) {
                        Debug.Log($"segmentId={segmentId} NextSegmentId={NextSegmentId} headNodeId={headNodeId} dir={dir}");
                        for (int i = 0; i < sortedSegList.Count; ++i) {
                            if (GetDirection(NextSegmentId, sortedSegList[i], headNodeId) == preferDir) {
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

        private static ArrowDirection GetDirection(ushort segmentId0, ushort segmentId1, ushort nodeId) {
            bool startNode0 = (bool)netService.IsStartNode(segmentId0, nodeId);
            ref ExtSegmentEnd segmenEnd0 = ref GetSegEnd(segmentId0, startNode0);
            ArrowDirection dir = segEndMan.GetDirection(ref segmenEnd0, segmentId1);
            return dir; // == ArrowDirection.Forward;
        }

        private static bool IsPartofRoundabout( ushort segmentId, ushort prevSegmentId, ushort headNodeId) {
            //Assuming segmentId is oneway and headNodeId is head of prevSegmentId
            if(segmentId == 0 || segmentId == prevSegmentId) {
                return false;
            }
            if (!segMan.CalculateIsOneWay(segmentId)) {
                return false;
            }
            ushort tailNodeId = netService.GetTailNode(segmentId);
            return headNodeId == tailNodeId;
        }
    } // end class
}//end namespace