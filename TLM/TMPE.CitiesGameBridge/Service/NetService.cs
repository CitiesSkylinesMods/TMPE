namespace CitiesGameBridge.Service {
    using System;
    using System.Collections.Generic;
    using ColossalFramework;
    using CSUtil.Commons;
    using GenericGameBridge.Service;

    public class NetService : INetService {
        public static readonly INetService Instance = new NetService();

        private NetService() { }

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

        public GetSegmentLaneIdsEnumerable GetSegmentLaneIdsAndLaneIndexes(ushort segmentId) {
            NetManager netManager = Singleton<NetManager>.instance;
            ref NetSegment netSegment = ref netManager.m_segments.m_buffer[segmentId];
            uint initialLaneId = netSegment.m_lanes;
            NetInfo netInfo = netSegment.Info;
            NetLane[] laneBuffer = netManager.m_lanes.m_buffer;
            if (netInfo == null) {
                return new GetSegmentLaneIdsEnumerable(0, 0, laneBuffer);
            }

            return new GetSegmentLaneIdsEnumerable(initialLaneId, netInfo.m_lanes.Length, laneBuffer);
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
    }
}