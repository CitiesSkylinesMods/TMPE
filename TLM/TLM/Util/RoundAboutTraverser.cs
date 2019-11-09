namespace TrafficManager.Util {
    using TrafficManager.UI.SubTools;
    using System.Collections.Generic;
    using ColossalFramework;
    using UnityEngine;
    using API.Manager;
    using API.Traffic.Data;
    using CSUtil.Commons;
    using TrafficManager.Manager.Impl;
    using API.Traffic.Enums;
    using GenericGameBridge.Service;
    using State;

    public class RoundAboutTraverser {
        public static RoundAboutTraverser Instance = new RoundAboutTraverser();
        public RoundAboutTraverser() {
            segmentList = new List<ushort>();
        }

        public List<ushort> segmentList = null;

        public static void FixLanesRAbout(ushort segmentId) {
            if (!Options.rabout_DecicatedExitLanes) {
                return;
            }

            /*ref NetSegment segment = ref Singleton<NetManager>.instance.m_segments.m_buffer[segmentId];
            bool startNode = (segment.m_flags & NetSegment.Flags.Invert) != NetSegment.Flags.None;
            ushort nodeId;
            if (startNode) {
                nodeId = segment.m_startNode;
            } else {
                nodeId = segment.m_endNode;
            }*/
            ushort nodeId = GetHeadNode(segmentId);
            if (LaneArrowManager.SeparateTurningLanes.CanChangeLanes(segmentId, nodeId) != SetLaneArrowError.Success) {
                return;
            }


            ref NetSegment seg = ref Singleton<NetManager>.instance.m_segments.m_buffer[segmentId];
            ref NetNode node = ref NetManager.instance.m_nodes.m_buffer[nodeId];
            bool isJunction = node.CountSegments() >= 3;
            bool startNode = (bool)Constants.ServiceFactory.NetService.IsStartNode(segmentId, nodeId);
            //Debug.Log($"segment={segmentId} node={nodeId} startNode={startNode} isJunction={isJunction}");

            if (!isJunction) {
                if (Options.rabout_NoLaneSwitchingInRabout) {
                    LaneConnectorTool.StayInLane(nodeId, LaneConnectorTool.StayInLaneMode.Both);
                }
                return;
            }

            IList<LanePos> laneList =
                Constants.ServiceFactory.NetService.GetSortedLanes(
                    segmentId,
                    ref seg,
                    startNode,
                    LaneArrowManager.LANE_TYPES,
                    LaneArrowManager.VEHICLE_TYPES,
                    true);
            int n_src = laneList.Count;

            // check for exits.
            bool bLeft, bRight, bForward;
            IExtSegmentEndManager segEndMan = Constants.ManagerFactory.ExtSegmentEndManager;
            ref ExtSegmentEnd segEnd = ref segEndMan.ExtSegmentEnds[segEndMan.GetIndex(segmentId, nodeId)];
            segEndMan.CalculateOutgoingLeftStraightRightSegments(ref segEnd, ref node, out bLeft, out bForward, out bRight);

            //if not a junction then set one dedicated exit lane per exit - if there are enough lanes that is.
            switch (n_src) {
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
                        LaneArrowManager.Instance.SetLaneArrows(laneList[n_src - 1].laneId, LaneArrows.Right);
                    }
                    if (bLeft) {
                        LaneArrowManager.Instance.SetLaneArrows(laneList[0].laneId, LaneArrows.Left);
                    }
                    break;
            }
        }

        private static void FixRulesRAbout(ushort segmentId) {
            foreach (bool startNode in Constants.ALL_BOOL) {
                TrafficPriorityManager.Instance.SetPrioritySign(
                    segmentId,
                    startNode,
                    PriorityType.Main);

                ushort nodeId = Constants.ServiceFactory.NetService.GetSegmentNodeId(
                    segmentId,
                    startNode);

                IExtSegmentEndManager segEndMan = Constants.ManagerFactory.ExtSegmentEndManager;
                ExtSegmentEnd curEnd = segEndMan.ExtSegmentEnds[
                    segEndMan.GetIndex(segmentId, startNode)];

                if (Options.rabout_NoCrossingRAbout) {
                    JunctionRestrictionsManager.Instance.SetPedestrianCrossingAllowed(
                        segmentId,
                        startNode
                        , false);
                }
                JunctionRestrictionsManager.Instance.SetEnteringBlockedJunctionAllowed(
                    segmentId,
                    startNode,
                    true);
            }
        }

        private static void FixRulesMinor(ushort segmentId, ushort nodeId) {
            bool startNode = (bool)Constants.ServiceFactory.NetService.IsStartNode(segmentId, nodeId);
            bool isHighway = ExtNodeManager.IsHighwayJunction(nodeId);

            if (Options.rabout_NoCrossingAtConnections) {
                JunctionRestrictionsManager.Instance.SetPedestrianCrossingAllowed(
                    segmentId,
                    startNode,
                    false);
            }
            TrafficPriorityManager.Instance.SetPrioritySign(
                segmentId,
                startNode,
                PriorityType.Yield);

            if (isHighway || Options.rabout_SwitchLanesAtEntry) {
                //ignore highway rules:
                JunctionRestrictionsManager.Instance.SetLaneChangingAllowedWhenGoingStraight(segmentId, startNode, true);
            } // endif
        }

        private static void FixLanesMinor(ushort segmentId, ushort nodeId) {
            if (!Options.rabout_NoLaneSwitchingNearEntries) {
                return;
            }
            ref NetSegment seg = ref Singleton<NetManager>.instance.m_segments.m_buffer[segmentId];
            Debug.Log($"segment={segmentId} node={nodeId} len={seg.m_averageLength}");
            int shortUnit = 5;
            int MeterPerUnit = 8;
            if ( seg.m_averageLength >= shortUnit * MeterPerUnit) {
                return;
            }
            ushort otherNodeId = seg.GetOtherNode(nodeId);
            Debug.Log($"segment={segmentId} node={nodeId} len={seg.m_averageLength} otherNodeId={otherNodeId}");
            LaneConnectorTool.StayInLane(otherNodeId, LaneConnectorTool.StayInLaneMode.Both);
        }

        private void FixMinor(ushort nodeId) {
            ref NetNode node = ref Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId];
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
            bool isRAbout = Instance.TraverseLoop(initialSegmentId);
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
                FixMinor(GetHeadNode(segId));
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
        public bool TraverseLoop(ushort segmentId) {
            segmentList.Clear();
            if (segmentId == 0 || !ExtSegmentManager.Instance.CalculateIsOneWay(segmentId)) {
                return false;
            }
            return TraverseAroundRecursive(segmentId);
        }
        private bool TraverseAroundRecursive(ushort segmentId) {
            segmentList.Add(segmentId);
            ushort headNodeId = GetHeadNode(segmentId);
            ref NetNode headNode = ref NetManager.instance.m_nodes.m_buffer[headNodeId];

            for (int i = 0; i < 8; ++i) {
                ushort nextSegmentId = headNode.GetSegment(i);
                if(!IsPartofRoundabout(nextSegmentId, segmentId, headNodeId)) {
                    continue;
                }
                if(GetDirection(segmentId, nextSegmentId, headNodeId) == ArrowDirection.Forward) {
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
                } // end if
            } // end for
            segmentList.Remove(segmentId);
            return false;
        }

        private static ArrowDirection GetDirection(ushort segmentId0, ushort segmentId1, ushort nodeId) {
            IExtSegmentEndManager segEndMan = Constants.ManagerFactory.ExtSegmentEndManager;
            bool startNode0 = (bool)Constants.ServiceFactory.NetService.IsStartNode(segmentId0, nodeId);
            ref ExtSegmentEnd segmenEnd0 = ref segEndMan.ExtSegmentEnds[segEndMan.GetIndex(segmentId0, startNode0)];
            ArrowDirection dir = segEndMan.GetDirection(ref segmenEnd0, segmentId1);
            return dir; // == ArrowDirection.Forward;
        }

        private static bool IsPartofRoundabout( ushort segmentId, ushort prevSegmentId, ushort headNodeId) {
            //assuming segmentId is oneway and headNodeId is head of prevSegmentId
            if(segmentId == 0 || segmentId == prevSegmentId) {
                return false;
            }
            if (!ExtSegmentManager.Instance.CalculateIsOneWay(segmentId)) {
                return false;
            }
            ushort tailNodeId = GetTailNode(segmentId);
            return headNodeId == tailNodeId;
        }

        private static ushort GetHeadNode(ushort segmentId) {
            // tail node>-------->head node
            ref NetSegment segment = ref Singleton<NetManager>.instance.m_segments.m_buffer[segmentId];
            bool invert = (segment.m_flags & NetSegment.Flags.Invert) != NetSegment.Flags.None;
            if (invert) {
                return segment.m_startNode;
            } else {
                return segment.m_endNode;
            }
        }

        private static ushort GetTailNode(ushort segmentId) {
            ref NetSegment segment = ref Singleton<NetManager>.instance.m_segments.m_buffer[segmentId];
            bool invert = (segment.m_flags & NetSegment.Flags.Invert) != NetSegment.Flags.None;
            if (!invert) {
                return segment.m_startNode;
            } else {
                return segment.m_endNode;
            }//endif
        }
    } // end class
}//end namespace