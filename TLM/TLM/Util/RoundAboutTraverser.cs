namespace TrafficManager.Util {
    using System.Collections.Generic;
    using ColossalFramework;
    using UnityEngine;
    using API.Manager;
    using API.Traffic.Data;
    using CSUtil.Commons;
    using TrafficManager.Manager.Impl;
    using API.Traffic.Enums;
    using GenericGameBridge.Service;

    public class RoundAboutTraverser {
        public List<ushort> segmentList = null;

        public static RoundAboutTraverser Instance = new RoundAboutTraverser();
        public RoundAboutTraverser() {
            segmentList = new List<ushort>();
        }

        private static void FixRules(ushort segmentId) {
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

                JunctionRestrictionsManager.Instance.SetPedestrianCrossingAllowed(
                    segmentId,
                    startNode
                    , false);
                JunctionRestrictionsManager.Instance.SetEnteringBlockedJunctionAllowed(
                    segmentId,
                    startNode,
                    true);

                bool isHighway = ExtNodeManager.IsHighwayJunction(nodeId);
                ref NetNode node = ref Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId];
                for (int i = 0; i < 8; ++i) {
                    ushort otherSegmentId = node.GetSegment(i);

                    if (otherSegmentId == 0 || otherSegmentId == segmentId) {
                        continue;
                    }

                    ArrowDirection dir = segEndMan.GetDirection(
                        ref curEnd,
                        otherSegmentId);

                    if (dir != ArrowDirection.Forward) {
                        bool startNode2 = (bool)Constants.ServiceFactory.NetService.IsStartNode(otherSegmentId, nodeId);
                        TrafficPriorityManager.Instance.SetPrioritySign(
                            otherSegmentId,
                            startNode2,
                            PriorityType.Yield);
                        if (isHighway) {
                            //ignore highway rules:
                            JunctionRestrictionsManager.Instance.SetLaneChangingAllowedWhenGoingStraight(otherSegmentId, startNode2, true);
                        } // endif
                    }// end if
                } // end for
            }//end foreach
            return;
        }

        public static void FixLanes(ushort segmentId) {
            ref NetSegment segment = ref Singleton<NetManager>.instance.m_segments.m_buffer[segmentId];
            bool startNode = (segment.m_flags & NetSegment.Flags.Invert) != NetSegment.Flags.None;
            ushort nodeId;
            if (startNode) {
                nodeId = segment.m_startNode;
            } else {
                nodeId = segment.m_endNode;
            }
            if (LaneArrowManager.SeparateTurningLanes.CanChangeLanes(segmentId, nodeId) != SetLaneArrowError.Success) {
                return;
            }

            ref NetNode node = ref NetManager.instance.m_nodes.m_buffer[nodeId];
            bool isJunction = node.CountSegments() >= 3;
            Debug.Log($"segment={segmentId} node={nodeId} startNode={startNode} isJunction={isJunction}");

            //Fix turning lanes:
            // direction of target lanes

            ref NetSegment seg = ref Singleton<NetManager>.instance.m_segments.m_buffer[segmentId];
            IList<LanePos> laneList =
                Constants.ServiceFactory.NetService.GetSortedLanes(
                    segmentId,
                    ref seg,
                    startNode,
                    LaneArrowManager.LANE_TYPES,
                    LaneArrowManager.VEHICLE_TYPES,
                    true);
            int n_src = laneList.Count;

            if (!isJunction) {
                ushort otherSegmentId = 0;
                for (int i = 0; i < 8; ++i) {
                    otherSegmentId = node.GetSegment(i);
                    if (otherSegmentId != 0 && otherSegmentId != segmentId)
                        break;
                }
                ref NetSegment otherSegment = ref NetManager.instance.m_segments.m_buffer[otherSegmentId];
                bool startNode2 = (otherSegment.m_flags & NetSegment.Flags.Invert) != NetSegment.Flags.None;
                IList<LanePos> targetLaneList =
                    Constants.ServiceFactory.NetService.GetSortedLanes(
                        otherSegmentId,
                        ref otherSegment,
                        startNode2,
                        LaneArrowManager.LANE_TYPES,
                        LaneArrowManager.VEHICLE_TYPES,
                        true);

                Debug.Log($"segmentId={segmentId} otherSegmentId={otherSegmentId} startNode2={startNode2} n_src={n_src} n_trg={targetLaneList.Count}");
                if (n_src == targetLaneList.Count) {
                    // Connect lanes
                    for (int i = 0; i < n_src; ++i) {
                        LaneConnectionManager.Instance.AddLaneConnection(
                            laneList[i].laneId,
                            targetLaneList[i].laneId,
                            startNode);
                    }
                }
            }

            bool bLeft, bRight, bForward;
            IExtSegmentEndManager segEndMan = Constants.ManagerFactory.ExtSegmentEndManager;
            ref ExtSegmentEnd segEnd = ref segEndMan.ExtSegmentEnds[segEndMan.GetIndex(segmentId, nodeId)];
            segEndMan.CalculateOutgoingLeftStraightRightSegments(ref segEnd, ref node, out bLeft, out bForward, out bRight);

            switch (n_src) {
                case 0:
                    break;
                case 1:
                    LaneArrows arrows = LaneArrows.Forward;
                    if (bRight) {
                        arrows |= LaneArrows.Right;
                    }
                    if (bLeft) {
                        arrows |= LaneArrows.Left;
                    }
                    LaneArrowManager.Instance.SetLaneArrows(laneList[0].laneId, arrows);
                    break;
                case 2:
                    if (bRight && bLeft) {
                        LaneArrowManager.Instance.SetLaneArrows(laneList[0].laneId, LaneArrows.Forward | LaneArrows.Left);
                        LaneArrowManager.Instance.SetLaneArrows(laneList[1].laneId, LaneArrows.Forward | LaneArrows.Right);

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

        public static void FixRabout(ushort segmentId) {
            bool isRAbout = RoundAboutTraverser.Instance.TraverseLoop(segmentId);
            if (!isRAbout) {
                return;
            }
            ref List<ushort> seglist = ref RoundAboutTraverser.Instance.segmentList;
            int count = seglist.Count;
            string m = $"\n segmentId={segmentId} seglist.count={count}\n";
            for (int i = 0; i < count; ++i)
                m += $"[{i}]:[{seglist[i]}]";
            Debug.Log(m);

            foreach (ushort segId in RoundAboutTraverser.Instance.segmentList) {
                FixRules(segId);
                FixLanes(segId);
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
                if(IsPartofRoundabout(nextSegmentId, segmentId,headNodeId)) {
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

        private static bool IsPartofRoundabout( ushort segmentId, ushort prevSegmentId, ushort headNodeId) {
            //assuming segmentId is oneway and headNodeId is head of prevSegmentId

            if (!ExtSegmentManager.Instance.CalculateIsOneWay(segmentId)) {
                return false;
            }
            ushort tailNodeId = GetTailNode(segmentId);
            if (headNodeId != tailNodeId) {
                return false;
            }

            IExtSegmentEndManager segEndMan = Constants.ManagerFactory.ExtSegmentEndManager;
            bool startNode0 = (bool)Constants.ServiceFactory.NetService.IsStartNode(prevSegmentId, headNodeId);
            ref ExtSegmentEnd prevSegEnd = ref segEndMan.ExtSegmentEnds[segEndMan.GetIndex(prevSegmentId, startNode0)];
            ArrowDirection dir = segEndMan.GetDirection(ref prevSegEnd, segmentId);
            return dir == ArrowDirection.Forward;
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