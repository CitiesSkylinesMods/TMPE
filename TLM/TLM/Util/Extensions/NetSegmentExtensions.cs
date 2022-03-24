namespace TrafficManager.Util.Extensions {
    using ColossalFramework;
    using System;
    using System.Collections.Generic;
    using TrafficManager.Manager.Impl;
    using TrafficManager.Util.Iterators;
    using static Shortcuts;

    public static class NetSegmentExtensions {
        private static NetSegment[] _segBuffer = Singleton<NetManager>.instance.m_segments.m_buffer;

        public static ref NetSegment ToSegment(this ushort segmentId) => ref _segBuffer[segmentId];

        public static ushort GetNodeId(this ref NetSegment segment, bool startNode) =>
            startNode ? segment.m_startNode : segment.m_endNode;

        public static ushort GetHeadNode(this ref NetSegment netSegment) {
            // tail node>-------->head node
            bool invert = netSegment.m_flags.IsFlagSet(NetSegment.Flags.Invert) ^ LHT;
            if (invert) {
                return netSegment.m_startNode;
            } else {
                return netSegment.m_endNode;
            }
        }

        public static ushort GetTailNode(this ref NetSegment netSegment) {
            bool invert = netSegment.m_flags.IsFlagSet(NetSegment.Flags.Invert) ^ LHT;
            if (!invert) {
                return netSegment.m_startNode;
            } else {
                return netSegment.m_endNode;
            }//endif
        }

        public static bool? IsStartNode(this ref NetSegment netSegment, ushort nodeId) {
            if (netSegment.m_startNode == nodeId) {
                return true;
            } else if (netSegment.m_endNode == nodeId) {
                return false;
            } else {
                return null;
            }
        }

        /// <returns><c>true</c> if nodeId is start node.
        /// <c>false</c> if nodeId is end node.
        /// Undetermined if segment does not have nodeId</returns>
        public static bool IsStartnode(this ref NetSegment netSegment, ushort nodeId) =>
            netSegment.m_startNode == nodeId;

        /// <summary>
        /// Checks if the netSegment is Created, but neither Collapsed nor Deleted.
        /// </summary>
        /// <param name="netSegment">netSegment</param>
        /// <returns>True if the netSegment is valid, otherwise false.</returns>
        public static bool IsValid(this ref NetSegment netSegment) =>
            netSegment.m_flags.CheckFlags(
                required: NetSegment.Flags.Created,
                forbidden: NetSegment.Flags.Collapsed | NetSegment.Flags.Deleted);

        public static NetInfo.Lane GetLaneInfo(this ref NetSegment netSegment, int laneIndex) =>
            netSegment.Info?.m_lanes?[laneIndex];

        public static GetSegmentLaneIdsEnumerable GetSegmentLaneIdsAndLaneIndexes(this ref NetSegment netSegment) {
            NetInfo netInfo = netSegment.Info;
            uint initialLaneId = netSegment.m_lanes;
            NetLane[] laneBuffer = NetManager.instance.m_lanes.m_buffer;
            if (netInfo == null) {
                return new GetSegmentLaneIdsEnumerable(0, 0, laneBuffer);
            }

            return new GetSegmentLaneIdsEnumerable(initialLaneId, netInfo.m_lanes.Length, laneBuffer);
        }

        /// <summary>
        /// Assembles a geometrically sorted list of lanes for the given segment.
        /// If the <paramref name="startNode"/> parameter is set only lanes supporting traffic to flow towards the given node are added to the list, otherwise all matched lanes are added.
        /// </summary>
        /// <param name="netSegment">segment data</param>
        /// <param name="startNode">reference node (optional)</param>
        /// <param name="laneTypeFilter">lane type filter, lanes must match this filter mask</param>
        /// <param name="vehicleTypeFilter">vehicle type filter, lanes must match this filter mask</param>
        /// <param name="reverse">if true, lanes are ordered from right to left (relative to the
        ///     segment's start node / the given node), otherwise from left to right</param>
        /// <param name="sort">if false, no sorting takes place
        ///     regardless of <paramref name="reverse"/></param>
        /// <returns>sorted list of lanes for the given segment</returns>
        public static IList<LanePos> GetSortedLanes(
            this ref NetSegment netSegment,
            bool? startNode,
            NetInfo.LaneType? laneTypeFilter = null,
            VehicleInfo.VehicleType? vehicleTypeFilter = null,
            bool reverse = false,
            bool sort = true) {
            // TODO refactor together with getSegmentNumVehicleLanes, especially the vehicle type and lane type checks
            NetManager netManager = Singleton<NetManager>.instance;
            var laneList = new List<LanePos>();

            bool inverted = (netSegment.m_flags & NetSegment.Flags.Invert) != NetSegment.Flags.None;

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

            NetInfo segmentInfo = netSegment.Info;
            uint curLaneId = netSegment.m_lanes;
            byte laneIndex = 0;

            while (laneIndex < segmentInfo.m_lanes.Length && curLaneId != 0u) {
                NetInfo.Lane laneInfo = segmentInfo.m_lanes[laneIndex];
                if ((laneTypeFilter == null ||
                     (laneInfo.m_laneType & laneTypeFilter) != NetInfo.LaneType.None) &&
                    (vehicleTypeFilter == null || (laneInfo.m_vehicleType & vehicleTypeFilter) !=
                     VehicleInfo.VehicleType.None) &&
                    (filterDir == null ||
                     segmentInfo.m_lanes[laneIndex].m_finalDirection == filterDir)) {
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
