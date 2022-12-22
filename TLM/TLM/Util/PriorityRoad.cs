namespace TrafficManager.Util {
    using CSUtil.Commons;
    using System;
    using System.Collections.Generic;
    using TrafficManager.API.Manager;
    using TrafficManager.API.Traffic.Data;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.Manager.Impl;
    using TrafficManager.Manager.Impl.LaneConnection;
    using TrafficManager.State;
    using TrafficManager.UI.SubTools.PrioritySigns;
    using TrafficManager.Util.Extensions;
    using TrafficManager.Util.Record;
    using UnityEngine;
    using static TrafficManager.Util.SegmentTraverser;
    using static TrafficManager.Util.Shortcuts;

    /// <summary>
    /// Utility for mass edit of priority roads.
    /// </summary>
    public static class PriorityRoad {
        public static IRecordable FixPrioritySigns(
            PrioritySignsTool.PrioritySignsMassEditMode massEditMode,
            List<ushort> segmentList) {
            if (segmentList == null || segmentList.Count == 0) {
                return null;
            }

            IRecordable record = RecordRoad(segmentList);

            var primaryPrioType = PriorityType.None;
            var secondaryPrioType = PriorityType.None;

            switch (massEditMode) {
                case PrioritySignsTool.PrioritySignsMassEditMode.MainYield: {
                        primaryPrioType = PriorityType.Main;
                        secondaryPrioType = PriorityType.Yield;
                        break;
                    }

                case PrioritySignsTool.PrioritySignsMassEditMode.MainStop: {
                        primaryPrioType = PriorityType.Main;
                        secondaryPrioType = PriorityType.Stop;
                        break;
                    }

                case PrioritySignsTool.PrioritySignsMassEditMode.YieldMain: {
                        primaryPrioType = PriorityType.Yield;
                        secondaryPrioType = PriorityType.Main;
                        break;
                    }

                case PrioritySignsTool.PrioritySignsMassEditMode.StopMain: {
                        primaryPrioType = PriorityType.Stop;
                        secondaryPrioType = PriorityType.Main;
                        break;
                    }
            }

            IExtSegmentEndManager segEndMan = Constants.ManagerFactory.ExtSegmentEndManager;

            void ApplyPrioritySigns(ushort segmentId, bool startNode) {

                ref NetSegment netSegment = ref segmentId.ToSegment();
                ushort nodeId = startNode ? netSegment.m_startNode : netSegment.m_endNode;

                TrafficPriorityManager.Instance.SetPrioritySign(
                    segmentId,
                    startNode,
                    primaryPrioType);

                ExtSegmentManager extSegmentManager = ExtSegmentManager.Instance;

                for (int i = 0; i < 8; ++i) {
                    ushort otherSegmentId = nodeId.ToNode().GetSegment(i);
                    if (otherSegmentId == 0 ||
                        otherSegmentId == segmentId ||
                        segmentList.Contains(otherSegmentId)) {
                        continue;
                    }

                    TrafficPriorityManager.Instance.SetPrioritySign(
                        otherSegmentId,
                        otherSegmentId.ToSegment().IsStartNode(nodeId),
                        secondaryPrioType);
                }
            }

            // TODO avoid setting up the same node two times.
            foreach (ushort segId in segmentList) {
                ApplyPrioritySigns(segId, true);
                ApplyPrioritySigns(segId, false);
            }

            return record;
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

        /// <summary>
        /// Quick-setups as priority junction: for every junctions on the road containing
        /// the input segment traversing straight.
        /// </summary>
        public static IRecordable FixRoad(ushort initialSegmentId) {
            // Create segment list such that the first and last segments are at path end.
            List<ushort> segmentList = new List<ushort>(40);
            SegmentTraverser.Traverse(
                    initialSegmentId,
                    TraverseDirection.AnyDirection,
                    TraverseSide.Straight,
                    SegmentStopCriterion.None,
                    data => {
                        if (data.ViaInitialStartNode)
                            segmentList.Add(data.CurSeg.segmentId);
                        else
                            segmentList.Insert(0, data.CurSeg.segmentId);
                        return true;
                    });

            return FixRoad(segmentList);
        }

        /// <returns>the node of <paramref name="segmentId"/> that is not shared
        /// with <paramref name="otherSegmentId"/> .</returns>
        private static ushort GetSharedOrOtherNode(ushort segmentId, ushort otherSegmentId, out ushort sharedNodeId) {
            ref NetSegment segment = ref segmentId.ToSegment();
            sharedNodeId = segment.GetSharedNode(otherSegmentId);
            if (sharedNodeId == 0)
                return 0;
            return segment.GetOtherNode(sharedNodeId);
        }

        /// <summary>
        /// Quick-setups as priority junction: for every junctions on the road containing
        /// the input segment traversing straight.
        /// </summary>
        public static IRecordable FixRoad(List<ushort> segmentList) {
            if (segmentList == null || segmentList.Count == 0)
                return null;
            IRecordable record = RecordRoad(segmentList);

            ushort firstNodeId = GetSharedOrOtherNode(segmentList[0], segmentList[1], out _);
            int last = segmentList.Count - 1;
            ushort lastNodeId = GetSharedOrOtherNode(segmentList[last], segmentList[last - 1], out _);
            if (firstNodeId == lastNodeId) {
                firstNodeId = lastNodeId = 0;
            }

            foreach (ushort segmentId in segmentList) {
                FixHighPriorityJunction(segmentId, firstNodeId, lastNodeId, segmentList, false);
                FixHighPriorityJunction(segmentId, firstNodeId, lastNodeId, segmentList, true);
            }

            return record;
        }

        private static void FixHighPriorityJunction(ushort segmentId, ushort firstNodeId, ushort lastNodeId, List<ushort> segmentList, bool startNode) {
            ref NetSegment netSegment = ref segmentId.ToSegment();
            ushort nodeId = startNode ? netSegment.m_startNode : netSegment.m_endNode;
            bool isEndNode = nodeId == firstNodeId || nodeId == lastNodeId;
            if (isEndNode) {
                FixHighPriorityJunction(nodeId);
            } else {
                FixHighPriorityJunction(nodeId, segmentList);
            }
        }

        private static bool IsStraighOneWay(ushort segmentId0, ushort segmentId1) {
            ref NetSegment seg0 = ref segmentId0.ToSegment();
            bool oneway = segMan.CalculateIsOneWay(segmentId0) &&
                          segMan.CalculateIsOneWay(segmentId1);
            if (!oneway) {
                return false;
            }

            ExtSegmentManager extSegmentManager = ExtSegmentManager.Instance;
            ushort nodeId;
            if ((nodeId = extSegmentManager.GetHeadNode(segmentId0)) == extSegmentManager.GetTailNode(segmentId1)) {
                if (GetDirection(segmentId0, segmentId1, nodeId) == ArrowDirection.Forward) {
                    return true;
                }
            } else if ((nodeId = extSegmentManager.GetHeadNode(segmentId1)) == extSegmentManager.GetTailNode(segmentId0)) {
                if (GetDirection(segmentId1, segmentId0, nodeId) == ArrowDirection.Forward) {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// if the roads form a T shape then try to arrange them like this (if possible)
        /// slot 0: incoming oneway road.
        /// slot 1: outgoing oneway road.
        /// slot 2: 2 way road.
        /// Post Condition: the arrangement of <paramref name="segmentList"/> might be
        /// altered regardless of return value.
        /// </summary>
        /// <returns>true on successful arrangement, false otherwise</returns>
        internal static bool ArrangeT(List<ushort> segmentList) {
            if (segmentList.Count != 3) {
                return false;
            }
            bool oneway0 = segMan.CalculateIsOneWay(segmentList[0]);
            bool oneway1 = segMan.CalculateIsOneWay(segmentList[1]);
            bool oneway2 = segMan.CalculateIsOneWay(segmentList[2]);
            int sum = Int(oneway0) + Int(oneway1) + Int(oneway2); // number of one way roads

            // put the two-way road in slot 2.
            if (sum != 2) {
                // expected a one way road and 2 two-way roads.
                return false;
            } else if (!oneway0) {
                segmentList.Swap(0, 2);
            } else if (!oneway1) {
                segmentList.Swap(1, 2);
            }

            ExtSegmentManager extSegmentManager = ExtSegmentManager.Instance;
            // slot 0: incoming road.
            // slot 1: outgoing road.
            if (extSegmentManager.GetHeadNode(segmentList[1]) == extSegmentManager.GetTailNode(segmentList[0])) {
                segmentList.Swap(0, 1);
                return true;
            }

            return extSegmentManager.GetHeadNode(segmentList[0]) == extSegmentManager.GetTailNode(segmentList[1]);
        }

        private static void HandleSplitAvenue(List<ushort> segmentList, ushort nodeId) {
            Log._Debug($"HandleSplitAvenue(segmentList, {nodeId}) was called");

            SetArrows(segmentList[0], segmentList[2], nodeId);
            SetArrows(segmentList[2], segmentList[1], nodeId);
            foreach (ushort segmentId in segmentList) {
                FixMajorSegmentRules(segmentId, nodeId);
            }
        }

        private static void SetArrows(ushort segmentIdSrc, ushort segmentIdDst, ushort nodeId) {
            LaneArrows arrow = ToLaneArrows(GetDirection(segmentIdSrc, segmentIdDst, nodeId));

            ref NetSegment segmentSrc = ref segmentIdSrc.ToSegment();

            bool startNode = segmentSrc.IsStartNode(nodeId);

            var lanes = segmentSrc.GetSortedLanes(
                startNode,
                LaneArrowManager.LANE_TYPES,
                LaneArrowManager.VEHICLE_TYPES,
                reverse: true);

            foreach (LanePos lane in lanes) {
                LaneArrowManager.Instance.SetLaneArrows(lane.laneId, arrow, true);
            }
        }

        internal static List<ushort> GetNodeSegments(ushort nodeId) {
            List<ushort> segmentList = new List<ushort>();
            for (int i = 0; i < 8; ++i) {
                ushort segId = nodeId.ToNode().GetSegment(i);
                if (segId != 0) {
                    segmentList.Add(segId);
                }
            }
            return segmentList;
        }

        /// <summary>
        /// Quick-setups the given junction as priority junction.
        /// The roads on the segmentList are considered as main road.
        /// All other roads are considered as minor road.
        /// Also detects:
        ///  - semi-roundabout
        ///  - split avenue into 2 oneway roads (only applicable to first or last node).
        /// </summary>
        public static void FixHighPriorityJunction(ushort nodeId, List<ushort> segmentList) {
            if (nodeId == 0) {
                return;
            }

            List<ushort> nodeSegments = new List<ushort>();
            for (int i = 0; i < 8; ++i) {
                ushort segId = nodeId.ToNode().GetSegment(i);
                if (segId != 0) {
                    bool main = segmentList.Contains(segId);
                    if (main) {
                        nodeSegments.Insert(0, segId);
                    } else {
                        nodeSegments.Add(segId);
                    }
                }
            }

            if (nodeSegments.Count < 3) {
                Log._Debug("FixJunction: This is not a junction. nodeID=" + nodeId);
                return;
            }

            FixHighPriorityJunctionHelper(nodeId, nodeSegments);
        } // end method

        /// <summary>
        /// Quick-setups the given junction as priority junction.
        /// The two biggest roads are considerer priority road.
        /// all other roads are considered minor road.
        /// Also detects:
        ///  - split avenue into 2 oneway roads
        ///  - semi-roundabout
        /// </summary>
        public static void FixHighPriorityJunction(ushort nodeId) {
            if (nodeId == 0) {
                return;
            }

            var nodeSegments = GetNodeSegments(nodeId);

            if (nodeSegments.Count < 3) {
                Log._Debug("FixJunction: This is not a junction. nodeID=" + nodeId);
                return;
            }

            if (ArrangeT(nodeSegments)) {
                bool isSemiRoundabout = GetDirection(nodeSegments[0], nodeSegments[1], nodeId) == ArrowDirection.Forward;
                // isSemiRoundabout if one of these shapes: |- \- /-
                // split avenue if one of these shapes: >-  <-
                // they are all T shaped the difference is the angle.
                if (!isSemiRoundabout) {
                    HandleSplitAvenue(nodeSegments, nodeId);
                    return;
                }
            } else {
                nodeSegments.Sort(CompareSegments);
            }

            if (CompareSegments(nodeSegments[1], nodeSegments[2]) == 0) {
                Log._Debug("FixJunction: cannot determine which road should be treaded as the main road.\n" +
                    "segmentList=" + nodeSegments.ToSTR());
                return;
            }

            FixHighPriorityJunctionHelper(nodeId, nodeSegments);
        }

        /// <summary>
        /// apply high priority junction rules
        /// - supports semi-roundabout.
        /// - no support for road splitting.
        /// </summary>
        /// <param name="nodeId">Junction to apply rules</param>
        /// <param name="nodeSegments">list of segments. The first two elements are main/roundabout,
        /// all other segments are minor</param>
        private static void FixHighPriorityJunctionHelper(ushort nodeId, List<ushort> nodeSegments) {
            bool isSemiRoundabout =
                nodeSegments.Count == 3 &&
                IsStraighOneWay(nodeSegments[0], nodeSegments[1]);

            // "far turn" is allowed when the main road is oneway.
            bool ignoreLanes =
                segMan.CalculateIsOneWay(nodeSegments[0]) ||
                segMan.CalculateIsOneWay(nodeSegments[1]);

            // Turning allowed when the main road is angled.
            ArrowDirection dir = GetDirection(nodeSegments[0], nodeSegments[1], nodeId);
            ignoreLanes |= dir != ArrowDirection.Forward;
            ignoreLanes |= SavedGameOptions.Instance.PriorityRoad_AllowLeftTurns;

            //Log._Debug($"ignorelanes={ignoreLanes} isSemiRoundabout={isSemiRoundabout}\n" +
            //            "segmentList=" + nodeSegments.ToSTR());

            for (int i = 0; i < nodeSegments.Count; ++i) {
                ushort segmentId = nodeSegments[i];
                if (i < 2) {
                    if (isSemiRoundabout) {
                        RoundaboutMassEdit.FixRulesRoundabout(segmentId);
                    } else {
                        FixMajorSegmentRules(segmentId, nodeId);
                    }
                    if (!ignoreLanes) {
                        FixMajorSegmentLanes(segmentId, nodeId);
                    }
                } else {
                    if (isSemiRoundabout) {
                        RoundaboutMassEdit.FixRulesMinor(segmentId, nodeId);
                    } else {
                        FixMinorSegmentRules(segmentId, nodeId, nodeSegments);
                    }
                    if (!ignoreLanes) {
                        FixMinorSegmentLanes(segmentId, nodeId, nodeSegments);
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
            Log._Debug($"FixMajorSegmentRules({segmentId}, {nodeId}) was called");
            bool startNode = segmentId.ToSegment().IsStartNode(nodeId);
            JunctionRestrictionsManager.Instance.SetEnteringBlockedJunctionAllowed(segmentId, startNode, true);
            if (!SavedGameOptions.Instance.PriorityRoad_CrossMainR) {
                JunctionRestrictionsManager.Instance.SetPedestrianCrossingAllowed(segmentId, startNode, false);
            }
            TrafficPriorityManager.Instance.SetPrioritySign(segmentId, startNode, PriorityType.Main);
        }

        private static void FixMinorSegmentRules(ushort segmentId, ushort nodeId, List<ushort> segmentList) {
            Log._Debug($"FixMinorSegmentRules({segmentId}, {nodeId}, segmentList) was called");
            bool startNode = segmentId.ToSegment().IsStartNode(nodeId);
            if (SavedGameOptions.Instance.PriorityRoad_EnterBlockedYeild) {
                JunctionRestrictionsManager.Instance.SetEnteringBlockedJunctionAllowed(segmentId, startNode, true);
            }
            if (HasAccelerationLane(segmentList, segmentId, nodeId)) {
                JunctionRestrictionsManager.Instance.SetEnteringBlockedJunctionAllowed(segmentId, startNode, true);
            } else if (SavedGameOptions.Instance.PriorityRoad_StopAtEntry) {
                TrafficPriorityManager.Instance.SetPrioritySign(segmentId, startNode, PriorityType.Stop);
            } else {
                TrafficPriorityManager.Instance.SetPrioritySign(segmentId, startNode, PriorityType.Yield);
            }
        }

        /// <summary>
        /// Count number of applicable lanes entering or leaving a segment via specific node.
        /// </summary>
        /// <param name="segmentId">The id of the segment to inspect.</param>
        /// <param name="nodeId">The id of node where lanes should be counted.</param>
        /// <param name="incoming">
        /// If <c>true</c>, count lanes entering the segment from the junction.
        /// If <c>false</c>, count lanes going from the segment to the junction.
        /// </param>
        /// <returns>Returns number of lanes matching the specified criteria.</returns>
        private static int CountLanes(ushort segmentId, ushort nodeId, bool incoming) =>
            segmentId.ToSegment().CountLanes(
                nodeId,
                LaneArrowManager.LANE_TYPES,
                LaneArrowManager.VEHICLE_TYPES,
                incoming);

        internal static int CountLanesTowardJunction(ushort segmentId, ushort nodeId)
            => CountLanes(segmentId, nodeId, false);
        internal static int CountLanesAgainstJunction(ushort segmentId, ushort nodeId)
            => CountLanes(segmentId, nodeId, true);

        internal static bool HasAccelerationLane(List<ushort> segmentList, ushort segmentId, ushort nodeId) {
            if (!segMan.CalculateIsOneWay(segmentId)) {
                return false;
            }
            bool IsMain(ushort segId) {
                return segId == segmentList[0] || segId == segmentList[1];
            }
            ref NetSegment seg = ref segmentId.ToSegment();

            ushort MainAgainst, MainToward;
            if (LHT) {
                MainAgainst = seg.GetLeftSegment(nodeId);
                MainToward = seg.GetRightSegment(nodeId);
            } else {
                MainAgainst = seg.GetRightSegment(nodeId);
                MainToward = seg.GetLeftSegment(nodeId);
            }

            Log._Debug($"HasAccelerationLane: segmentId:{segmentId} MainToward={MainToward} MainAgainst={MainAgainst} ");
            if (IsMain(MainToward) && IsMain(MainAgainst)) {
                int Yt = CountLanesTowardJunction(segmentId, nodeId); // Yield Toward.
                int Mt = CountLanesTowardJunction(MainToward, nodeId); // Main Toward.
                int Ma = CountLanesAgainstJunction(MainAgainst, nodeId); // Main Against.
                bool ret = Yt > 0 && Yt + Mt <= Ma;
                Log._Debug($"HasAccelerationLane: Yt={Yt}  Mt={Mt} Ma={Ma} ret={ret} : Yt + Mt <= Ma ");
                return ret;
            }

            return false;
        }

        private static void FixMajorSegmentLanes(ushort segmentId, ushort nodeId) {
            Log._Debug($"FixMajorSegmentLanes({segmentId}, {nodeId}) was called");

            if (SeparateTurningLanesUtil.CanChangeLanes(segmentId, nodeId) != SetLaneArrow_Result.Success) {
                Log._Debug("FixMajorSegmentLanes: can't change lanes");
                return;
            }

            ref NetSegment seg = ref segmentId.ToSegment();
            ref NetNode node = ref nodeId.ToNode();
            bool startNode = seg.IsStartNode(nodeId);
            bool lht = LHT;

            //list of outgoing lanes from current segment to current node.
            var laneList = seg.GetSortedLanes(
                startNode,
                LaneArrowManager.LANE_TYPES,
                LaneArrowManager.VEHICLE_TYPES,
                reverse: !lht);

            int srcLaneCount = laneList.Count;
            Log._Debug($"FixMajorSegmentLanes: segment:{segmentId} laneList:" + laneList.ToSTR());

            bool bLeft, bRight, bForward;
            ref ExtSegmentEnd segEnd = ref GetSegEnd(segmentId, nodeId);
            segEndMan.CalculateOutgoingLeftStraightRightSegments(ref segEnd, ref node, out bLeft, out bForward, out bRight);

            LaneArrows arrowShort = lht ? LaneArrows.Left : LaneArrows.Right;
            LaneArrows arrowFar = lht ? LaneArrows.Right : LaneArrows.Left;
            for (int i = 0; i < srcLaneCount; ++i) {
                uint laneId = laneList[i].laneId;
                LaneArrows arrows = LaneArrowManager.Instance.GetFinalLaneArrows(laneId);
                LaneArrowManager.Instance.RemoveLaneArrows(
                    laneId,
                    arrowFar);

                if (arrows != arrowShort) {
                    LaneArrowManager.Instance.SetLaneArrows(
                        laneList[i].laneId,
                        LaneArrows.Forward);
                }
            }

            bool bShort = lht ? bLeft : bRight;
            if (srcLaneCount > 0 && bShort) {
                LanePos outerMostLane = laneList[laneList.Count - 1];
                LaneArrowManager.Instance.AddLaneArrows(outerMostLane.laneId, arrowShort);
            }
        }

        private static void FixMinorSegmentLanes(ushort segmentId, ushort nodeId, List<ushort> segmentList) {
            Log._Debug($"FixMinorSegmentLanes({segmentId}, {nodeId}, segmentList) was called");
            if (SeparateTurningLanesUtil.CanChangeLanes(segmentId, nodeId) != SetLaneArrow_Result.Success) {
                Debug.Log("FixMinorSegmentLanes(): can't change lanes");
                return;
            }
            ref NetSegment seg = ref segmentId.ToSegment();
            ref NetNode node = ref nodeId.ToNode();
            bool startNode = seg.IsStartNode(nodeId);

            //list of outgoing lanes from current segment to current node.
            var laneList = seg.GetSortedLanes(
                startNode,
                LaneArrowManager.LANE_TYPES,
                LaneArrowManager.VEHICLE_TYPES,
                reverse: true); // should this be `!lht`?

            int srcLaneCount = laneList.Count;

            bool bLeft, bRight, bForward;
            ref ExtSegmentEnd segEnd = ref GetSegEnd(segmentId, nodeId);
            segEndMan.CalculateOutgoingLeftStraightRightSegments(ref segEnd, ref node, out bLeft, out bForward, out bRight);

            // LHD vs RHD variables.
            bool lht = Shortcuts.LHT;
            ArrowDirection nearDir = lht ? ArrowDirection.Left : ArrowDirection.Right;
            LaneArrows nearArrow = lht ? LaneArrows.Left : LaneArrows.Right;
            bool bnear = lht ? bLeft : bRight;
            int sideLaneIndex = lht ? srcLaneCount - 1 : 0;

            LaneArrows turnArrow = nearArrow;
            {
                // Check for slight turn into the main road.
                ArrowDirection dir0 = segEndMan.GetDirection(ref segEnd, segmentList[0]);
                ArrowDirection dir1 = segEndMan.GetDirection(ref segEnd, segmentList[1]);
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
             * and the main road is straight, then add a turn arrow into the other minor roads.
             */
            if (srcLaneCount > 0 && bnear && turnArrow == LaneArrows.Forward) {
                LaneArrowManager.Instance.AddLaneArrows(
                    laneList[sideLaneIndex].laneId,
                    nearArrow);
            }
        }

        /// <summary>
        /// returns a positive value if seg1Id < seg2Id
        /// </summary>
        internal static int CompareSegments(ushort seg1Id, ushort seg2Id) {
            ref NetSegment seg1 = ref seg1Id.ToSegment();
            ref NetSegment seg2 = ref seg2Id.ToSegment();

            int diff = -seg1.Info.GetClassLevel().CompareTo(seg2.Info.GetClassLevel());
            if (diff != 0) {
                return diff;
            }

            diff = CountRoadVehicleLanes(seg2Id) - CountRoadVehicleLanes(seg1Id);
            if (diff != 0) {
                return diff;
            }

            diff = Mathf.RoundToInt(seg2.Info.m_halfWidth - seg1.Info.m_halfWidth);
            return diff;
        }

        private static int CountRoadVehicleLanes(ushort segmentId) {
            ref NetSegment segment = ref segmentId.ToSegment();
            int forward = 0, backward = 0;
            segment.CountLanes(
                segmentId,
                NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle,
                VehicleInfo.VehicleType.Car,
                VehicleInfo.VehicleCategory.All,
                ref forward,
                ref backward);
            return forward + backward;
        }

        /// <summary>
        /// Clears all rules put by PriorityRoad.FixJunction()
        /// </summary>
        /// <param name="segmentList"></param>
        public static void ClearNode(ushort nodeId) {
            LaneConnectionManager.Instance.RemoveLaneConnectionsFromNode(nodeId);

            ref NetNode node = ref nodeId.ToNode();

            for (int segmentIndex = 0; segmentIndex < Constants.MAX_SEGMENTS_OF_NODE; ++segmentIndex) {
                ushort segmentId = node.GetSegment(segmentIndex);
                if (segmentId != 0) {
                    bool startNode = segmentId.ToSegment().IsStartNode(nodeId);
                    TrafficPriorityManager.Instance.SetPrioritySign(segmentId, startNode, PriorityType.None);
                    JunctionRestrictionsManager.Instance.ClearSegmentEnd(segmentId, startNode);
                    LaneArrowManager.Instance.ResetLaneArrows(segmentId, startNode);
                }
            }
        }

        /// <summary>
        /// Clears all rules traffic rules across given segment list.
        /// Clears segment ends of connected branches as well.
        /// </summary>
        public static IRecordable ClearRoad(List<ushort> segmentList) {
            if (segmentList == null || segmentList.Count == 0)
                return null;
            IRecordable record = RecordRoad(segmentList);
            foreach (ushort segmentId in segmentList) {
                ref NetSegment segment = ref segmentId.ToSegment();

                ParkingRestrictionsManager.Instance.SetParkingAllowed(segmentId, true);

                SpeedLimitManager.Instance.SetSegmentSpeedLimit(
                    segmentId,
                    SetSpeedLimitAction.ResetToDefault());

                VehicleRestrictionsManager.Instance.ClearVehicleRestrictions(segmentId);

                ClearNode(segment.m_startNode);
                ClearNode(segment.m_endNode);
            }
            return record;
        }

        /// <summary>
        /// records traffic rules state of everything affected by <c>FixRoad()</c> or <c>FixPrioritySigns()</c>
        /// </summary>
        public static IRecordable RecordRoad(List<ushort> segmentList) {
            TrafficRulesRecord record = new TrafficRulesRecord();
            foreach (ushort segmetnId in segmentList)
                record.AddCompleteSegment(segmetnId);
            record.Record();
            return record;
        }

        public static void EraseAllTrafficRoadsForNode(ushort nodeId) {
            try {
                TrafficLightManager.Instance.ResetTrafficLightAndPrioritySignsFromNode(nodeId);
                LaneConnectionManager.Instance.RemoveLaneConnectionsFromNode(nodeId);
                LaneArrowManager.Instance.ResetNodeLaneArrows(nodeId);
                JunctionRestrictionsManager.Instance.ClearNode(nodeId);
            } catch (Exception ex) {
                ex.LogException();
            }
        }
    } //end class
}
