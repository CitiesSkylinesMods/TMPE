namespace CitiesGameBridge.Service {
    using System;
    using System.Collections.Generic;
    using ColossalFramework;
    using CSUtil.Commons;
    using GenericGameBridge.Service;

    public class NetService : INetService {
        public static readonly INetService Instance = new NetService();

        private NetService() { }

        // NODE BASICS --------------------------------------------------------------------------------

        /// <summary>
        /// Check if a node id is valid.
        /// </summary>
        ///
        /// <param name="nodeId">The id of the node to check.</param>
        ///
        /// <returns>Returns <c>true</c> if valid, otherwise <c>false</c>.</returns>
        public bool IsNodeValid(ushort nodeId) {
            return CheckNodeFlags(
                nodeId,
                NetNode.Flags.Created | NetNode.Flags.Collapsed | NetNode.Flags.Deleted,
                NetNode.Flags.Created);
        }

        /// <summary>
        /// Check node flags contain at least one of the flags in <paramref name="flagMask"/>.
        /// </summary>
        ///
        /// <param name="nodeId">The id of the node to inspect.</param>
        /// <param name="flagMask">The flags to test.</param>
        /// <param name="expectedResult">If specified, ensure only the expected flags are found.</param>
        ///
        /// <returns>Returns <c>true</c> if the test passes, otherwise <c>false</c>.</returns>
        public bool CheckNodeFlags(ushort nodeId,
                                   NetNode.Flags flagMask,
                                   NetNode.Flags? expectedResult = null) {

            NetNode.Flags result = Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId].m_flags & flagMask;

            return expectedResult == null ? result != 0 : result == expectedResult;
        }

        // SEGMENT BASICS --------------------------------------------------------------------------------

        /// <summary>
        /// Check if a segment id is valid.
        /// </summary>
        ///
        /// <param name="segmentId">The id of the segment to check.</param>
        ///
        /// <returns>Returns <c>true</c> if valid, otherwise <c>false</c>.</returns>
        public bool IsSegmentValid(ushort segmentId) {
            return CheckSegmentFlags(
                segmentId,
                NetSegment.Flags.Created | NetSegment.Flags.Collapsed | NetSegment.Flags.Deleted,
                NetSegment.Flags.Created);
        }

        /// <summary>
        /// Check segment flags contain at least one of the flags in <paramref name="flagMask"/>.
        /// </summary>
        ///
        /// <param name="segmentId">The id of the segment to inspect.</param>
        /// <param name="flagMask">The flags to test.</param>
        /// <param name="expectedResult">If specified, ensure only the expected flags are found.</param>
        ///
        /// <returns>Returns <c>true</c> if the test passes, otherwise <c>false</c>.</returns>
        public bool CheckSegmentFlags(ushort segmentId,
                                      NetSegment.Flags flagMask,
                                      NetSegment.Flags? expectedResult = null) {

            NetSegment.Flags result = Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].m_flags & flagMask;

            return expectedResult == null ? result != 0 : result == expectedResult;
        }

        // LANE BASICS --------------------------------------------------------------------------------

        /// <summary>
        /// Check if a lane id is valid, optionally also checking validity of parent segment.
        /// </summary>
        ///
        /// <param name="laneId">The id of the lane to check.</param>
        /// <param name="checkSegment">If <c>true</c>, validity of parent segment will also be checked.</param>
        ///
        /// <returns>Returns <c>true</c> if valid, otherwise <c>false</c>.</returns>
        public bool IsLaneValid(uint laneId, bool checkSegment) {

            if (checkSegment &&
                !IsSegmentValid(Singleton<NetManager>.instance.m_lanes.m_buffer[laneId].m_segment)) {

                return false;
            }

            return CheckLaneFlags(
                laneId,
                NetLane.Flags.Created | NetLane.Flags.Deleted,
                NetLane.Flags.Created);
        }

        /// <summary>
        /// Check if a lane id and its parent segment are valid.
        /// </summary>
        ///
        /// <param name="laneId">The id of the lane to check.</param>
        ///
        /// <returns>Returns <c>true</c> if both lane and segment are valid, otherwise <c>false</c>.</returns>
        [Obsolete("Use IsLaneValid(uint, bool) instead; ideally only check parent segment validity once per collection of lanes.")]
        public bool IsLaneAndItsSegmentValid(uint laneId) {
            return IsLaneValid(laneId, true);
        }

        /// <summary>
        /// Check lane flags contain at least one of the flags in <paramref name="flagMask"/>.
        /// </summary>
        ///
        /// <param name="laneId">The id of the lane to inspect.</param>
        /// <param name="flagMask">The flags to test for.</param>
        /// <param name="expectedResult">If specified, ensure only the expected flags are found.</param>
        ///
        /// <returns>Returns <c>true</c> if the test passes, otherwise <c>false</c>.</returns>
        public bool CheckLaneFlags(uint laneId,
                                   NetLane.Flags flagMask,
                                   NetLane.Flags? expectedResult = null) {

            uint result = Singleton<NetManager>.instance.m_lanes.m_buffer[laneId].m_flags & (uint)flagMask;

            return expectedResult == null ? result != 0 : result == (uint)expectedResult;
        }

        // OTHER STUFF --------------------------------------------------------------------------------

        public ushort GetSegmentNodeId(ushort segmentId, bool startNode) {
            ref NetSegment segment = ref Singleton<NetManager>.instance.m_segments.m_buffer[segmentId];
            return startNode
                ? segment.m_startNode
                : segment.m_endNode;
        }

        public GetNodeSegmentIdsEnumerable GetNodeSegmentIds(ushort nodeId, ClockDirection clockDirection) {
            var initialSegmentId = GetInitialSegment(ref Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId]);
            var segmentBuffer = Singleton<NetManager>.instance.m_segments.m_buffer;
            return new GetNodeSegmentIdsEnumerable(nodeId, initialSegmentId, clockDirection, segmentBuffer);
        }

        /// <summary>
        /// Gets the initial segment.
        /// </summary>
        /// <param name="node">The node with the segments.</param>
        /// <returns>First non 0 segmentId.</returns>
        public ushort GetInitialSegment(ref NetNode node) {
            for (int i = 0; i < 8; ++i) {
                var segmentId = node.GetSegment(i);
                if (segmentId != 0) {
                    return segmentId;
                }
            }

            return 0;
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

        public IList<LanePos> GetSortedLanes(ushort segmentId,
                                             ref NetSegment segment,
                                             bool? startNode,
                                             NetInfo.LaneType? laneTypeFilter = null,
                                             VehicleInfo.VehicleType? vehicleTypeFilter = null,
                                             bool reverse = false,
                                             bool sort = true) {
            // TODO refactor together with getSegmentNumVehicleLanes, especially the vehicle type and lane type checks
            NetManager netManager = Singleton<NetManager>.instance;
            var laneList = new List<LanePos>();

            bool inverted = (segment.m_flags & NetSegment.Flags.Invert) != NetSegment.Flags.None;

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

            ref NetSegment segment = ref Singleton<NetManager>.instance.m_segments.m_buffer[segmentId];
            uint currentBuildIndex = simService.CurrentBuildIndex;
            simService.CurrentBuildIndex = currentBuildIndex + 1;
            segment.m_modifiedIndex = currentBuildIndex;
            ++segment.m_buildIndex;
        }

        public bool? IsStartNode(ushort segmentId, ushort nodeId) {
            ref NetSegment segment = ref Singleton<NetManager>.instance.m_segments.m_buffer[segmentId];
            if (segment.m_startNode == nodeId) {
                return true;
            } else if (segment.m_endNode == nodeId) {
                return false;
            } else {
                return null;
            }
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