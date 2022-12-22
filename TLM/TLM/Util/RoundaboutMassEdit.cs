namespace TrafficManager.Util {
    using ColossalFramework.Math;
    using CSUtil.Commons;
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
    using TrafficManager.Util.Extensions;

    public class RoundaboutMassEdit {
        public static RoundaboutMassEdit Instance = new ();
        public RoundaboutMassEdit() {
            segmentList_ = new List<ushort>();
        }

        private List<ushort> segmentList_;

        private static void FixSegmentRoundabout(ushort segmentId, ushort nextSegmentId) {
            if (SavedGameOptions.Instance.RoundAboutQuickFix_ParkingBanMainR) {
                ParkingRestrictionsManager.Instance.SetParkingAllowed(segmentId, false);
            }

            if (SavedGameOptions.Instance.RoundAboutQuickFix_RealisticSpeedLimits) {
                SpeedValue? targetSpeed = CalculatePreferedSpeed(segmentId);
                float defaultSpeed = SpeedLimitManager.Instance.CalculateCustomNetinfoSpeedLimit(segmentId.ToSegment().Info);

                if (targetSpeed != null && targetSpeed.Value.GetKmph() < defaultSpeed) {
                    var action = SetSpeedLimitAction.SetOverride(targetSpeed.Value);
                    SpeedLimitManager.Instance.SetSegmentSpeedLimit(segmentId, action);
                }
            }

            ref NetSegment segment = ref segmentId.ToSegment();
            ushort nodeId = segment.GetHeadNode();
            ref NetNode node = ref nodeId.ToNode();
            bool isJunction = node.IsJunction();

            if (SavedGameOptions.Instance.RoundAboutQuickFix_StayInLaneMainR && !isJunction) {
                StayInLane(nodeId, StayInLaneMode.Both);
            }

            // allocation of dedicated exit lanes is supported only when the roundabout is round
            // in which case the next segment should be straight ahead.
            bool isStraight = segEndMan.GetDirection(segmentId, nextSegmentId, nodeId) == ArrowDirection.Forward;

            if (SavedGameOptions.Instance.RoundAboutQuickFix_DedicatedExitLanes &&
                isJunction &&
                SeparateTurningLanesUtil.CanChangeLanes(
                    segmentId, nodeId) == SetLaneArrow_Result.Success &&
                    isStraight) {

                var laneList = segment.GetSortedLanes(
                    segment.IsStartNode(nodeId),
                    LaneArrowManager.LANE_TYPES,
                    LaneArrowManager.VEHICLE_TYPES,
                    reverse: true);

                int nSrc = laneList.Count;

                // check for exits.
                segEndMan.CalculateOutgoingLeftStraightRightSegments(
                    ref GetSegEnd(segmentId, nodeId),
                    ref node,
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
            FixRulesRoundabout(segmentId, false);
            FixRulesRoundabout(segmentId, true);
        }

        private static void FixRulesRoundabout(ushort segmentId, bool startNode) {
            if (SavedGameOptions.Instance.RoundAboutQuickFix_PrioritySigns) {
                TrafficPriorityManager.Instance.SetPrioritySign(
                    segmentId,
                    startNode,
                    PriorityType.Main);
            }

            if (SavedGameOptions.Instance.RoundAboutQuickFix_NoCrossMainR) {
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

        internal static void FixRulesMinor(ushort segmentId, ushort nodeId) {
            bool startNode = segmentId.ToSegment().IsStartNode(nodeId);
            bool isHighway = ExtNodeManager.JunctionHasOnlyHighwayRoads(nodeId);

            if (SavedGameOptions.Instance.RoundAboutQuickFix_NoCrossYieldR) {
                JunctionRestrictionsManager.Instance.SetPedestrianCrossingAllowed(
                    segmentId,
                    startNode,
                    false);
            }
            if (SavedGameOptions.Instance.RoundAboutQuickFix_PrioritySigns) {
                TrafficPriorityManager.Instance.SetPrioritySign(
                    segmentId,
                    startNode,
                    PriorityType.Yield);
            }

            if (isHighway) {
                //ignore highway rules: //TODO remove as part of issue #569
                JunctionRestrictionsManager.Instance.SetLaneChangingAllowedWhenGoingStraight(segmentId, startNode, true);
            } // endif

            if (SavedGameOptions.Instance.RoundAboutQuickFix_KeepClearYieldR) {
                JunctionRestrictionsManager.Instance.SetEnteringBlockedJunctionAllowed(
                    segmentId,
                    startNode,
                    false);
            }
        }

        private static void FixSegmentMinor(ushort segmentId, ushort nodeId) {
            if (SavedGameOptions.Instance.RoundAboutQuickFix_ParkingBanYieldR) {
                ParkingRestrictionsManager.Instance.SetParkingAllowed(segmentId, false);
            }
            int shortUnit = 4;
            int meterPerUnit = 8;
            ref NetSegment seg = ref segmentId.ToSegment();
            ushort otherNodeId = seg.GetOtherNode(nodeId);
            if (SavedGameOptions.Instance.RoundAboutQuickFix_StayInLaneNearRabout &&
                !HasJunctionFlag(otherNodeId) &&
                seg.m_averageLength < shortUnit * meterPerUnit) {
                StayInLane(otherNodeId, StayInLaneMode.Both);
            }
        }

        private void FixMinor(ushort nodeId) {
            ref NetNode node = ref nodeId.ToNode();
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
                FixMinor(ExtSegmentManager.Instance.GetHeadNode(segId));
            }
            return record;
        }

        /// <summary>
        /// Traverses around a roundabout.
        /// </summary>
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
                ExtSegmentManager extSegmentManager = ExtSegmentManager.Instance;
                for (int i = 0; i < lastN; ++i) {
                    ushort prevSegmentID = segList[i];
                    ushort nextSegmentID = segList[(i + 1) % n];
                    ushort headNodeID = extSegmentManager.GetHeadNode(prevSegmentID);
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
        /// in RHT preferring forward then left then right.
        /// if there are multiple forwards then prefer the leftmost ones.
        /// if there are multiple lefts then prefer the rightmost ones.
        /// if there are multiple rights then prefer the leftmost ones
        /// LHT is the opposite.
        /// </summary>
        private static List<ushort> GetSortedSegments(ushort segmentId) {
            ushort headNodeId = ExtSegmentManager.Instance.GetHeadNode(segmentId);
            var list0 = GetSortedSegmentsHelper(headNodeId, segmentId, ArrowDirection.Forward);
            var list1 = GetSortedSegmentsHelper(headNodeId, segmentId, ArrowDirection.Left);
            var list2 = GetSortedSegmentsHelper(headNodeId, segmentId, ArrowDirection.Right);

            if (LHT) {
                list0.AddRange(list2);
                list0.AddRange(list1);
            } else {
                list0.AddRange(list1);
                list0.AddRange(list2);
            }
            return list0;
        }

        /// <summary>
        /// get segments that can be part of the round about and match the given direction.
        /// segments are sorted by angle.
        /// </summary>
        private static List<ushort> GetSortedSegmentsHelper(
            ushort headNodeId,
            ushort segmentId,
            ArrowDirection dir) {
            ExtNodeManager extNodeManager = ExtNodeManager.Instance;

            var segmentList =
                extNodeManager.GetNodeSegmentIds(headNodeId, ClockDirection.CounterClockwise)
                .Where(_segmentId =>
                    IsPartofRoundabout(_segmentId, segmentId, headNodeId) &&
                    segEndMan.GetDirection(segmentId, _segmentId, headNodeId) == dir);

            Vector3 endDir = segmentId.ToSegment().GetDirection(headNodeId);

            return segmentList.OrderBy(Dot).ToList();

            // more negative dot-product => angle is closer to 180
            float Dot(ushort _segmentID) {
                Vector3 _endDir = _segmentID.ToSegment().GetDirection(headNodeId);
                return VectorUtils.DotXZ(_endDir, endDir);
            }
        }

        /// <summary>
        /// Checks whether the next segmentId looks like to be part of a roundabout.
        /// Assumes prevSegmentId is oneway
        /// </summary>
        /// <param name="nextSegmentId"></param>
        /// <param name="prevSegmentId"></param>
        /// <param name="headNodeId">head node for prevSegmentId</param>
        /// <returns></returns>
        private static bool IsPartofRoundabout(ushort nextSegmentId, ushort prevSegmentId, ushort headNodeId) {
            bool ret = nextSegmentId != 0 && nextSegmentId != prevSegmentId;
            ret &= segMan.CalculateIsOneWay(nextSegmentId);
            ret &= headNodeId == ExtSegmentManager.Instance.GetTailNode(nextSegmentId);
            return ret;
        }

        /// <summary>
        /// returns true if the given segment is attached to the middle of the
        /// path of segmentList_ by checking for duplicate nodes.
        /// </summary>
        private bool Contains(ushort segmentId) {
            ExtSegmentManager extSegmentManager = ExtSegmentManager.Instance;
            ushort nodeId = extSegmentManager.GetHeadNode(segmentId);
            foreach (ushort segId in segmentList_) {
                if (extSegmentManager.GetHeadNode(segId) == nodeId) {
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
        /// Calculates Radius of a curved segment assuming it is part of a circle.
        /// </summary>
        internal static float CalculateRadius(ref NetSegment segment) {
            // TDOO: to calculate maximum curvature for elliptical roundabout, cut the bezier in 10 portions
            // and then find the bezier with minimum radius.
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
        /// calculates realistic speed limit of input curved segment assuming it is part of a circle.
        /// minimum speed is 10kmph.
        /// </summary>
        /// <returns>Null if segment is straight, otherwise if successful it returns calculated speed.</returns>
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