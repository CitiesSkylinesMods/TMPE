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

    class PriorityRoad {
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
            seglist.Sort(CompareSegments);

            if (CompareSegments(seglist[1], seglist[2]) == 0) {
                // all roads connected to the junction are equal.
                return;
            }

            // turning allowed when main road is oneway.
            bool ignoreLanes =
                ExtSegmentManager.Instance.CalculateIsOneWay(seglist[0]) ||
                ExtSegmentManager.Instance.CalculateIsOneWay(seglist[1]);
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
                        FixMinorSegmentLanes(seglist[i], nodeId);
                    }
                }
            } //end for
        } // end method



        private static void FixMajorSegmentRules(ushort segmentId, ushort nodeId) {
            bool startNode = (bool)Constants.ServiceFactory.NetService.IsStartNode(segmentId, nodeId);
            JunctionRestrictionsManager.Instance.SetEnteringBlockedJunctionAllowed(segmentId, startNode, true);
            JunctionRestrictionsManager.Instance.SetPedestrianCrossingAllowed(segmentId, startNode, false);
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

            ref NetSegment seg = ref Singleton<NetManager>.instance.m_segments.m_buffer[segmentId];
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
            IExtSegmentEndManager segEndMan = Constants.ManagerFactory.ExtSegmentEndManager;
            ref ExtSegmentEnd segEnd = ref segEndMan.ExtSegmentEnds[segEndMan.GetIndex(segmentId, nodeId)];
            segEndMan.CalculateOutgoingLeftStraightRightSegments(ref segEnd, ref node, out bLeft, out bForward, out bRight);


            //TODO: code for left hand drive
            //TODO: code for bendy avenue.
            // ban left turns and use of FR arrow where applicable.
            for (int i = 0; i < srcLaneCount; ++i) {
                LaneArrowManager.Instance.SetLaneArrows(
                    laneList[i].laneId,
                    LaneArrows.Forward);
            }
            if (srcLaneCount > 0 && bRight) {
                LanePos righMostLane = laneList[laneList.Count - 1];
                LaneArrowManager.Instance.SetLaneArrows(righMostLane.laneId, LaneArrows.ForwardRight);
            }
        }

        private static void FixMinorSegmentLanes(ushort segmentId, ushort nodeId) {
            if (LaneArrowManager.SeparateTurningLanes.CanChangeLanes(segmentId, nodeId) != SetLaneArrowError.Success) {
                Debug.Log("cant change lanes");
                return;
            }
            ref NetSegment seg = ref Singleton<NetManager>.instance.m_segments.m_buffer[segmentId];
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

            IExtSegmentEndManager segEndMan = Constants.ManagerFactory.ExtSegmentEndManager;
            ref ExtSegmentEnd segEnd = ref segEndMan.ExtSegmentEnds[segEndMan.GetIndex(segmentId, nodeId)];


            // TODO: add code for bendy roads
            // TODO: add code for LHD
            // only right turn
            for (int i = 0; i < srcLaneCount; ++i) {
                LaneArrowManager.Instance.SetLaneArrows(
                    laneList[i].laneId,
                    LaneArrows.Right);
            }
        }

        private static int CompareSegments(ushort seg1Id, ushort seg2Id) {
            NetSegment seg1 = Singleton<NetManager>.instance.m_segments.m_buffer[seg1Id];
            NetSegment seg2 = Singleton<NetManager>.instance.m_segments.m_buffer[seg2Id];
            int diff = (int)Math.Ceiling(seg2.Info.m_halfWidth - seg1.Info.m_halfWidth);
            if (diff == 0) {
                diff = CountCarLanes(seg2Id) - CountCarLanes(seg1Id);
            }
            return diff;
        }

        private static int CountCarLanes(ushort segmentId) {
            NetSegment segment = Singleton<NetManager>.instance.m_segments.m_buffer[segmentId];
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
