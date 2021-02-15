namespace TrafficManager.Util {
    using ColossalFramework.Math;
    using CSUtil.Commons;
    using GenericGameBridge.Service;
    using TrafficManager.Util.Record;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using TrafficManager.API.Traffic.Data;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.Manager.Impl;
    using TrafficManager.State;
    using UnityEngine;
    using static TrafficManager.Util.Shortcuts;
    using static UI.SubTools.LaneConnectorTool;

    public class RoundaboutMassEdit {
        public static RoundaboutMassEdit Instance = new RoundaboutMassEdit();
        public RoundaboutMassEdit() {
            segmentList_ = new List<ushort>();
        }

        private List<ushort> segmentList_;

        private static void FixSegmentRoundabout(ushort segmentId, ushort nextSegmentId) {
            if (OptionsMassEditTab.RoundAboutQuickFix_ParkingBanMainR) {
                ParkingRestrictionsManager.Instance.SetParkingAllowed(segmentId, false);
            }
            if (OptionsMassEditTab.RoundAboutQuickFix_RealisticSpeedLimits) {
                float? targetSpeed = CalculatePreferedSpeed(segmentId)?.GameUnits;
                float defaultSpeed = SpeedLimitManager.Instance.GetCustomNetInfoSpeedLimit(segmentId.ToSegment().Info);
                if (targetSpeed != null && targetSpeed < defaultSpeed) {
                    SpeedLimitManager.Instance.SetSpeedLimit(segmentId, NetInfo.Direction.Forward, targetSpeed);
                    SpeedLimitManager.Instance.SetSpeedLimit(segmentId, NetInfo.Direction.Backward, targetSpeed);
                }
            }
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
                        break; // not enough lanes Use default settings.
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

            if (OptionsMassEditTab.RoundAboutQuickFix_KeepClearYieldR) {
                JunctionRestrictionsManager.Instance.SetEnteringBlockedJunctionAllowed(
                    segmentId,
                    startNode,
                    false);
            }
        }

        private static void FixSegmentMinor(ushort segmentId, ushort nodeId) {
            if (OptionsMassEditTab.RoundAboutQuickFix_ParkingBanYieldR) {
                ParkingRestrictionsManager.Instance.SetParkingAllowed(segmentId, false);
            }
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
                FixSegmentMinor(segmentId, nodeId);
            }//end for
        }

        /// <summary>
        /// Fixes the round about or returns false if it is not a round about.
        /// </summary>
        /// <param name="segmentId"></param>
        /// <returns></returns>
        public bool FixRoundabout(ushort initialSegmentId, out IRecordable record) {
            bool isRoundabout = TraverseLoop(initialSegmentId, out var segList);
            if (!isRoundabout) {
                Log._Debug($"segment {initialSegmentId} not a roundabout.");
                record = null;
                return false;
            }
            int count = segList.Count;
            Log._Debug($"\n segmentId={initialSegmentId} seglist.count={count}\n");

            record = FixRoundabout(segList);
            return true;
        }

        public IRecordable FixRoundabout(List<ushort> segList) {
            if (segList == null)
                return null;
            IRecordable record = RecordRoundabout(segList);
            this.segmentList_ = segList;
            int count = segList.Count;
            for (int i = 0; i < count; ++i) {
                ushort segId = segList[i];
                ushort nextSegId = segList[(i + 1) % count];
                FixSegmentRoundabout(segId, nextSegId);
                FixRulesRoundabout(segId);
                FixMinor(netService.GetHeadNode(segId));
            }
            return record;
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
            if (segmentId == 0 || !segMan.CalculateIsOneWay(segmentId)) {
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
            catch (Exception e) {
                Log.Error(e.ToString());
                return false;
            }
        }

        private bool TraverseAroundRecursive(ushort segmentId) {
            if (segmentList_.Count > 20) {
                return false; // too long. prune
            }
            segmentList_.Add(segmentId);
            var segments = GetSortedSegments(segmentId);

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
            var list0 = GetSortedSegmentsHelper(headNodeId, segmentId, ArrowDirection.Forward, !lht);
            var list1 = GetSortedSegmentsHelper(headNodeId, segmentId, ArrowDirection.Left, lht);
            var list2 = GetSortedSegmentsHelper(headNodeId, segmentId, ArrowDirection.Right, !lht);

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

            foreach (var nodeSegmentId in netService.GetNodeSegmentIds(headNodeId, ClockDirection.CounterClockwise)) {
                if (!IsPartofRoundabout(nodeSegmentId, segmentId, headNodeId)) {
                    continue;
                }
                if (segEndMan.GetDirection(segmentId, nodeSegmentId, headNodeId) == dir) {
                    for (int i = 0; i < sortedSegList.Count; ++i) {
                        if (segEndMan.GetDirection(nodeSegmentId, sortedSegList[i], headNodeId) == preferDir) {
                            sortedSegList.Insert(i, nodeSegmentId);
                            continue;
                        }
                    }
                    sortedSegList.Add(nodeSegmentId);
                }
            }

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
        private static bool IsPartofRoundabout(ushort nextSegmentId, ushort prevSegmentId, ushort headNodeId) {
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
        /// Records the state of traffic rules for all stuff affected by <c>FixRoundabout()</c>
        /// </summary>
        /// <param name="segList"></param>
        /// <returns></returns>
        public static IRecordable RecordRoundabout(List<ushort> segList) {
            TrafficRulesRecord record = new TrafficRulesRecord();
            foreach (ushort segmentId in segList)
                record.AddCompleteSegment(segmentId);

            // Add each minor road.
            foreach (ushort nodeId in record.NodeIDs.ToArray()) {
                ref NetNode node = ref nodeId.ToNode();
                if (node.CountSegments() < 3) continue;
                for (int i = 0; i < 8; ++i) {
                    ushort segmentId = node.GetSegment(i);
                    if (segmentId == 0)
                        continue;
                    if (record.SegmentIDs.Contains(segmentId))
                        continue;

                    record.AddSegmentAndNodes(segmentId);
                }
            }

            record.Record();
            return record;
        }

        /// <summary>
        /// Calculates Raduis of a curved segment assuming it is part of a circle.
        /// </summary>
        internal static float CalculateRadius(ref NetSegment segment) {
            // TDOO: to calculate maximum curviture for eleptical roundabout, cut the bezier in 10 portions
            // and then find the bezier with minimum raduis.
            Vector2 startDir = VectorUtils.XZ(segment.m_startDirection);
            Vector2 endDir = VectorUtils.XZ(segment.m_endDirection);
            Vector2 startPos = VectorUtils.XZ(segment.m_startNode.ToNode().m_position);
            Vector2 endPos = VectorUtils.XZ(segment.m_endNode.ToNode().m_position);
            float dot = Vector2.Dot(startDir, -endDir);
            float len = (startPos - endPos).magnitude;
            float r = len / Mathf.Sqrt(2 - 2 * dot); // see https://github.com/CitiesSkylinesMods/TMPE/issues/793#issuecomment-616351792
            return r;
        }

        /// <summary>
        /// calculates realisitic speed limit of input curved segment assuming it is part of a circle.
        /// minimum speed is 10kmph.
        /// </summary>
        /// <returns>Null if segment is straight, otherwise if successful it retunrs calcualted speed.</returns>
        private static SpeedValue? CalculatePreferedSpeed(ushort segmentId) {
            float r = CalculateRadius(ref segmentId.ToSegment());
            float kmph = 11.3f * Mathf.Sqrt(r); // see https://github.com/CitiesSkylinesMods/TMPE/issues/793#issue-589462235
            Log._Debug($"CalculatePreferedSpeed radius:{r} -> kmph:{kmph}");
            if (float.IsNaN(kmph) || float.IsInfinity(kmph) || kmph < 1f) {
                return null;
            }
            if (kmph < 10f) {
                kmph = 10f;
            }
            return SpeedValue.FromKmph((ushort)kmph);
        }
    } // end class
}//end namespace