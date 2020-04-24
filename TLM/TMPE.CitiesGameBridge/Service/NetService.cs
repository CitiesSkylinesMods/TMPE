namespace CitiesGameBridge.Service {
    using System;
    using System.Collections.Generic;
    using ColossalFramework;
    using CSUtil.Commons;
    using GenericGameBridge.Service;

    public class NetService : INetService {
        public static readonly INetService Instance = new NetService();

        private NetService() { }

        public bool IsSegmentValid(ushort segmentId) {
            return CheckSegmentFlags(
                segmentId,
                NetSegment.Flags.Created | NetSegment.Flags.Deleted,
                NetSegment.Flags.Created);
        }

        public void ProcessSegment(ushort segmentId, NetSegmentHandler handler) {
            ProcessSegment(
                segmentId,
                ref Singleton<NetManager>.instance.m_segments.m_buffer[segmentId],
                handler);
        }

        public void ProcessSegment(ushort segmentId,
                                   ref NetSegment segment,
                                   NetSegmentHandler handler) {
            handler(segmentId, ref segment);
        }

        public bool IsNodeValid(ushort nodeId) {
            return CheckNodeFlags(
                nodeId,
                NetNode.Flags.Created | NetNode.Flags.Deleted,
                NetNode.Flags.Created);
        }

        public void ProcessNode(ushort nodeId, NetNodeHandler handler) {
            ProcessNode(
                nodeId,
                ref Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId],
                handler);
        }

        public void ProcessNode(ushort nodeId, ref NetNode node, NetNodeHandler handler) {
            handler(nodeId, ref node);
        }

        [Obsolete]
        bool IsLaneValid(ref NetLane lane) {
            if ((lane.m_flags & (uint)(NetLane.Flags.Created | NetLane.Flags.Deleted)) !=
                (uint)NetLane.Flags.Created) {
                return false;
            }

            return IsSegmentValid(lane.m_segment);
        }

        public bool IsLaneValid(uint laneId) {
            if (!CheckLaneFlags(
                    laneId,
                    NetLane.Flags.Created | NetLane.Flags.Deleted,
                    NetLane.Flags.Created)) {
                return false;
            }

            bool ret = false;
            ProcessLane(
                laneId,
                (uint lId, ref NetLane lane) => {
                    ret = IsSegmentValid(lane.m_segment);
                    return true;
                });
            return ret;
        }

        public void ProcessLane(uint laneId, NetLaneHandler handler) {
            ProcessLane(
                laneId,
                ref Singleton<NetManager>.instance.m_lanes.m_buffer[laneId],
                handler);
        }

        public void ProcessLane(uint laneId, ref NetLane lane, NetLaneHandler handler) {
            handler(laneId, ref lane);
        }

        public ushort GetSegmentNodeId(ushort segmentId, bool startNode) {
            ushort nodeId = 0;
            ProcessSegment(
                segmentId,
                (ushort segId, ref NetSegment segment) => {
                    nodeId = startNode ? segment.m_startNode : segment.m_endNode;
                    return true;
                });
            return nodeId;
        }

        public void IterateNodeSegments(ushort nodeId, NetSegmentHandler handler) {
            IterateNodeSegments(nodeId, ClockDirection.None, handler);
        }

        public void IterateNodeSegments(ushort nodeId,
                                        ClockDirection dir,
                                        NetSegmentHandler handler) {
            NetManager netManager = Singleton<NetManager>.instance;

            bool ProcessFun(ushort nId, ref NetNode node) {
                if (dir == ClockDirection.None) {
                    for (int i = 0; i < 8; ++i) {
                        ushort segmentId = node.GetSegment(i);
                        if (segmentId != 0) {
                            if (!handler(
                                    segmentId,
                                    ref netManager.m_segments.m_buffer[segmentId])) {
                                break;
                            }
                        }
                    }
                } else {
                    ushort segmentId = 0;
                    for (int i = 0; i < 8; ++i) {
                        segmentId = node.GetSegment(i);
                        if (segmentId != 0) {
                            break;
                        }
                    }
                    ushort initSegId = segmentId;

                    while (true) {
                        if (segmentId != 0) {
                            if (!handler(
                                    segmentId,
                                    ref netManager.m_segments.m_buffer[segmentId])) {
                                break;
                            }
                        }

                        switch (dir) {
                            // also: case ClockDirection.Clockwise:
                            default:
                                segmentId = netManager.m_segments.m_buffer[segmentId]
                                                      .GetLeftSegment(nodeId);
                                break;
                            case ClockDirection.CounterClockwise:
                                segmentId = netManager.m_segments.m_buffer[segmentId]
                                                      .GetRightSegment(nodeId);
                                break;
                        }

                        if (segmentId == initSegId || segmentId == 0) {
                            break;
                        }
                    }
                }

                return true;
            }

            ProcessNode(nodeId, ProcessFun);
        }

        public void IterateSegmentLanes(ushort segmentId, NetSegmentLaneHandler handler) {
            IterateSegmentLanes(
                segmentId,
                ref Singleton<NetManager>.instance.m_segments.m_buffer[segmentId],
                handler);
        }

        public void IterateSegmentLanes(ushort segmentId,
                                        ref NetSegment segment,
                                        NetSegmentLaneHandler handler) {
            NetInfo segmentInfo = segment.Info;
            if (segmentInfo == null) {
                return;
            }

            byte laneIndex = 0;
            uint curLaneId = segment.m_lanes;
            while (laneIndex < segmentInfo.m_lanes.Length && curLaneId != 0u) {
                NetInfo.Lane laneInfo = segmentInfo.m_lanes[laneIndex];
                handler(
                    curLaneId,
                    ref Singleton<NetManager>.instance.m_lanes.m_buffer[curLaneId],
                    laneInfo,
                    segmentId,
                    ref segment,
                    laneIndex);

                curLaneId = Singleton<NetManager>.instance.m_lanes.m_buffer[curLaneId].m_nextLane;
                ++laneIndex;
            }
        }

        public NetInfo.Direction GetFinalSegmentEndDirection(ushort segmentId, bool startNode) {
            return GetFinalSegmentEndDirection(
                segmentId,
                ref Singleton<NetManager>.instance.m_segments.m_buffer[segmentId],
                startNode);
        }

        public NetInfo.Direction GetFinalSegmentEndDirection(
            ushort segmentId,
            ref NetSegment segment,
            bool startNode) {
            var dir = startNode ? NetInfo.Direction.Backward : NetInfo.Direction.Forward;

            if ((segment.m_flags & NetSegment.Flags.Invert) !=
                NetSegment.Flags.None /*^ SimulationService.Instance.LeftHandDrive*/) {
                dir = NetInfo.InvertDirection(dir);
            }

            return dir;
        }

        public bool CheckNodeFlags(ushort nodeId,
                                   NetNode.Flags flagMask,
                                   NetNode.Flags? expectedResult = null) {
            bool ret = false;
            ProcessNode(
                nodeId,
                (ushort nId, ref NetNode node) => {
                    ret = LogicUtil.CheckFlags(
                        (uint)node.m_flags,
                        (uint)flagMask,
                        (uint?)expectedResult);
                    return true;
                });
            return ret;
        }

        public bool CheckSegmentFlags(ushort segmentId,
                                      NetSegment.Flags flagMask,
                                      NetSegment.Flags? expectedResult = null) {
            bool ret = false;
            ProcessSegment(
                segmentId,
                (ushort sId, ref NetSegment segment) => {
                    ret = LogicUtil.CheckFlags(
                        (uint)segment.m_flags,
                        (uint)flagMask,
                        (uint?)expectedResult);
                    return true;
                });
            return ret;
        }

        public bool CheckLaneFlags(uint laneId,
                                   NetLane.Flags flagMask,
                                   NetLane.Flags? expectedResult = null) {
            bool ret = false;
            ProcessLane(
                laneId,
                (uint lId, ref NetLane lane) => {
                    ret = LogicUtil.CheckFlags(
                        lane.m_flags,
                        (uint)flagMask,
                        (uint?)expectedResult);
                    return true;
                });
            return ret;
        }

        public IList<LanePos> GetSortedLanes(ushort segmentId,
                                             ref NetSegment segment,
                                             bool? startNode,
                                             NetInfo.LaneType? laneTypeFilter = null,
                                             VehicleInfo.VehicleType? vehicleTypeFilter = null,
                                             bool reverse = false,
                                             bool sort=true) {
            // TODO refactor together with getSegmentNumVehicleLanes, especially the vehicle type and lane type checks
            NetManager netManager = Singleton<NetManager>.instance;
            var laneList = new List<LanePos>();

            bool inverted = ((segment.m_flags & NetSegment.Flags.Invert) != NetSegment.Flags.None);

            NetInfo.Direction? filterDir = null;
            NetInfo.Direction sortDir = NetInfo.Direction.Forward;

            if (startNode != null) {
                filterDir = (bool)startNode
                                ? NetInfo.Direction.Backward
                                : NetInfo.Direction.Forward;
                filterDir = inverted
                                ? NetInfo.InvertDirection((NetInfo.Direction)filterDir)
                                : filterDir;
                sortDir = NetInfo.InvertDirection((NetInfo.Direction)filterDir);
            } else if (inverted) {
                sortDir = NetInfo.Direction.Backward;
            }

            if (reverse) {
                sortDir = NetInfo.InvertDirection(sortDir);
            }

            NetInfo segmentInfo = segment.Info;
            uint curLaneId = segment.m_lanes;
            byte laneIndex = 0;

            while (laneIndex < segmentInfo.m_lanes.Length && curLaneId != 0u) {
                NetInfo.Lane laneInfo = segmentInfo.m_lanes[laneIndex];
                if ((laneTypeFilter == null ||
                     (laneInfo.m_laneType & laneTypeFilter) != NetInfo.LaneType.None) &&
                    (vehicleTypeFilter == null || (laneInfo.m_vehicleType & vehicleTypeFilter) !=
                     VehicleInfo.VehicleType.None) &&
                    (filterDir == null ||
                     segmentInfo.m_lanes[laneIndex].m_finalDirection == filterDir))
                {
                    laneList.Add(
                        new LanePos(
                            curLaneId,
                            laneIndex,
                            segmentInfo.m_lanes[laneIndex].m_position,
                            laneInfo.m_vehicleType,
                            laneInfo.m_laneType));
                }

                curLaneId = netManager.m_lanes.m_buffer[curLaneId].m_nextLane;
                ++laneIndex;
            }

            if (sort) {
                int CompareLanePositionsFun(LanePos x, LanePos y) {
                    bool fwd = sortDir == NetInfo.Direction.Forward;
                    if (Math.Abs(x.position - y.position) < 1e-12) {
                        if (x.position > 0) {
                            // mirror type-bound lanes (e.g. for coherent disply of lane-wise speed limits)
                            fwd = !fwd;
                        }

                        if (x.laneType == y.laneType) {
                            if (x.vehicleType == y.vehicleType) {
                                return 0;
                            }

                            if ((x.vehicleType < y.vehicleType) == fwd) {
                                return -1;
                            }

                            return 1;
                        }

                        if ((x.laneType < y.laneType) == fwd) {
                            return -1;
                        }

                        return 1;
                    }

                    if (x.position < y.position == fwd) {
                        return -1;
                    }

                    return 1;
                }

                laneList.Sort(CompareLanePositionsFun);
            }
            return laneList;
        }

        public void PublishSegmentChanges(ushort segmentId) {
            Log._Debug($"NetService.PublishSegmentChanges({segmentId}) called.");
            ISimulationService simService = SimulationService.Instance;

            ProcessSegment(
                segmentId,
                (ushort sId, ref NetSegment segment) => {
                    uint currentBuildIndex = simService.CurrentBuildIndex;
                    simService.CurrentBuildIndex = currentBuildIndex + 1;
                    segment.m_modifiedIndex = currentBuildIndex;

                    ++segment.m_buildIndex;
                    return true;
                });
        }

        public bool? IsStartNode(ushort segmentId, ushort nodeId) {
            bool? ret = null;
            ProcessSegment(
                segmentId,
                (ushort segId, ref NetSegment seg) => {
                    if (seg.m_startNode == nodeId) {
                        ret = true;
                    } else if (seg.m_endNode == nodeId) {
                        ret = false;
                    }

                    return true;
                });
            return ret;
        }

        public ushort GetHeadNode(ref NetSegment segment) {
            // tail node>-------->head node
            bool invert = (segment.m_flags & NetSegment.Flags.Invert) != NetSegment.Flags.None;
            invert = invert ^ SimulationService.Instance.TrafficDrivesOnLeft;
            if (invert) {
                return segment.m_startNode;
            } else {
                return segment.m_endNode;
            }
        }

        public ushort GetHeadNode(ushort segmentId) =>
            GetHeadNode(ref Singleton<NetManager>.instance.m_segments.m_buffer[segmentId]);

        public ushort GetTailNode(ref NetSegment segment) {
            bool invert = (segment.m_flags & NetSegment.Flags.Invert) != NetSegment.Flags.None;
            invert = invert ^ SimulationService.Instance.TrafficDrivesOnLeft;
            if (!invert) {
                return segment.m_startNode;
            } else {
                return segment.m_endNode;
            }//endif
        }

        public ushort GetTailNode(ushort segmentId) =>
            GetTailNode(ref Singleton<NetManager>.instance.m_segments.m_buffer[segmentId]);
    }
}