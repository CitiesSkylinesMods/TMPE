namespace TrafficManager.Util.Extensions {
    using ColossalFramework;
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Diagnostics.CodeAnalysis;
    using TrafficManager.Manager.Impl;
    using TrafficManager.Util.Iterators;
    using static Shortcuts;

    public static class NetSegmentExtensions {
        private static NetSegment[] _segBuffer = Singleton<NetManager>.instance.m_segments.m_buffer;

        public static ref NetSegment ToSegment(this ushort segmentId) => ref _segBuffer[segmentId];

        /// <summary>
        /// Obtain the id of either the start or end node of a segment.
        /// </summary>
        /// <param name="segment">The <see cref="NetSegment"/> to inspect.</param>
        /// <param name="startNode">
        /// Set <c>true</c> to get start node id, or <c>false</c> for end node id.
        /// </param>
        /// <returns>Returns the id of the node.</returns>
        /// <example>
        /// Get start <see cref="NetNode"/> from a segment:
        /// <code>
        /// ref var node = ref segmentId.ToSegment().GetNodeId(true).ToNode();
        /// </code>
        /// </example>
        public static ushort GetNodeId(this ref NetSegment segment, bool startNode) =>
            startNode
                ? segment.m_startNode
                : segment.m_endNode;

        /// <summary>
        /// Get id of the Head node of the <paramref name="netSegment"/>,
        /// taking in to account segment inversion and traffic driving side.
        /// </summary>
        /// <param name="netSegment">The <see cref="NetSegment"/> to inspect.</param>
        /// <returns>Returns the id of the Head node.</returns>
        /// <example>
        /// Get head <see cref="NetNode"/> from a segment:
        /// <code>
        /// ref var node = ref segmentId.ToSegment().GetHeadNode().ToNode();
        /// </code>
        /// </example>
        public static ushort GetHeadNode(this ref NetSegment netSegment) =>
            netSegment.m_flags.IsFlagSet(NetSegment.Flags.Invert) ^ LHT
                ? netSegment.m_startNode
                : netSegment.m_endNode;

        /// <summary>
        /// Get id of the Tail node of the <paramref name="netSegment"/>,
        /// taking in to account segment inversion and traffic driving side.
        /// </summary>
        /// <param name="netSegment">The <see cref="NetSegment"/> to inspect.</param>
        /// <returns>Returns the id of the Tail node.</returns>
        /// <example>
        /// Get tail <see cref="NetNode"/> from a segment:
        /// <code>
        /// ref var node = ref segmentId.ToSegment().GetTailNode().ToNode();
        /// </code>
        /// </example>
        public static ushort GetTailNode(this ref NetSegment netSegment) =>
            netSegment.m_flags.IsFlagSet(NetSegment.Flags.Invert) ^ LHT
                ? netSegment.m_endNode
                : netSegment.m_startNode;

        /// <summary>
        /// Check if the <paramref name="nodeId"/> belongs to the
        /// <paramref name="netSegment"/>, and if so determines
        /// whether it is the start or end node.
        /// </summary>
        /// <param name="netSegment">The segment to inspect.</param>
        /// <param name="nodeId">The id of the node to examine.</param>
        /// <returns>
        /// <list type="bullet">
        /// <item><term><c>true</c></term> <description>start node</description></item>
        /// <item><term><c>false</c></term> <description>end node</description></item>
        /// <item><term><c>null</c></term> <description>not related to segment</description></item>
        /// </list>
        /// </returns>
        /// <example>
        /// Get and process node relationship:
        /// <code>
        /// bool? relation = segmentId.ToSegment().GetRelationToNode(nodeId);
        ///
        /// if (!relation.HasValue) {
        ///     // no relation
        /// } else if (relation.Value) {
        ///     // start node
        /// } else {
        ///     // end node
        /// }
        /// </code>
        /// </example>
        public static bool? GetRelationToNode(this ref NetSegment netSegment, ushort nodeId) {
            if (netSegment.m_startNode == nodeId) {
                return true;
            } else if (netSegment.m_endNode == nodeId) {
                return false;
            } else {
                return null;
            }
        }

        /// <summary>
        /// Determine if specified <paramref name="nodeId"/> is the start node for
        /// the <paramref name="netSegment"/>.
        /// </summary>
        /// <param name="netSegment">The segment to inspect.</param>
        /// <param name="nodeId">The id of the node to examine.</param>
        /// <returns>
        /// <para>Returns <c>true</c> if start node, otherwise <c>false</c>.</para>
        /// <para>A <c>false</c> return value does not guarantee the node is the
        /// segment end node; the node might not belong to the segment.
        /// If you need to ensure the node is related to the segment, use
        /// <see cref="GetRelationToNode(ref NetSegment, ushort)"/> instead.</para>
        /// </returns>
        public static bool IsStartNode(this ref NetSegment netSegment, ushort nodeId) =>
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

        /// <summary>
        /// Obtain the <see cref="NetInfo.Lane"/> for the specific <paramref name="laneIndex"/>
        /// of the <paramref name="netSegment"/>.
        /// </summary>
        /// <param name="netSegment">The <see cref="NetSegment"/> to inspect.</param>
        /// <param name="laneIndex">The index of the lane to retrieve.</param>
        /// <returns>
        /// Returns the associated <see cref="NetInfo.Lane"/> if found;
        /// otherwise <c>null</c>.
        /// </returns>
        [SuppressMessage("Correctness", "UNT0008:Null propagation on Unity objects", Justification = "Verified as working.")]
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
        /// Iterates the lanes in the specified <paramref name="netSegment"/> until it finds one which matches
        /// both the specified <paramref name="laneType"/> and <paramref name="vehicleType"/> masks.
        /// </summary>
        /// <param name="netSegment">The <see cref="NetSegment"/> to inspect.</param>
        /// <param name="laneType">The required <see cref="NetInfo.LaneType"/> flags (at least one must match).</param>
        /// <param name="vehicleType">The required <see cref="VehicleInfo.VehicleType"/> flags (at least one must match).</param>
        /// <returns>Returns <c>true</c> if a lane matches, otherwise <c>false</c> if none of the lanes match.</returns>
        [SuppressMessage("Correctness", "UNT0008:Null propagation on Unity objects", Justification = "Verified as working.")]
        public static bool AnyApplicableLane(
            this ref NetSegment netSegment,
            NetInfo.LaneType laneType,
            VehicleInfo.VehicleType vehicleType) {

#if DEBUG
            // AssertNotNone(laneType, nameof(laneType));
            // AssertNotNone(vehicleType, nameof(vehicleType));
#endif

            NetInfo segmentInfo = netSegment.Info;

            if (segmentInfo?.m_lanes == null)
                return false;

            uint curLaneId = netSegment.m_lanes;
            byte laneIndex = 0;

            NetManager netManager = Singleton<NetManager>.instance;

            while (laneIndex < segmentInfo.m_lanes.Length && curLaneId != 0u) {
                NetInfo.Lane laneInfo = segmentInfo.m_lanes[laneIndex];

                if ((laneInfo.m_vehicleType & vehicleType) != 0 &&
                    (laneInfo.m_laneType & laneType) != 0) {

                    return true;
                }

                curLaneId = netManager.m_lanes.m_buffer[curLaneId].m_nextLane;
                ++laneIndex;
            }

            return false;
        }

        /// <summary>
        /// Count the number of lanes matching specified criteria at a segment end.
        ///
        /// Faster than doing <c>GetSortedLanes().Count</c>.
        /// </summary>
        /// <param name="segment">The segment to inspect.</param>
        /// <param name="nodeId">The id of the node to inspect.</param>
        /// <param name="laneTypeFilter">Filter to specified lane types.</param>
        /// <param name="vehicleTypeFilter">Filter to specified vehicle types.</param>
        /// <param name="incoming">
        /// If <c>true</c>, count lanes entering the segment via the node.
        /// if <c>false</c>, count lanes leaving the segment via the node.
        /// </param>
        /// <returns>Returns number of lanes matching specified criteria.</returns>
        /// <remarks>
        /// See also: <c>CountLanes()</c> methods on the <see cref="NetSegment"/> struct.
        /// </remarks>
        [SuppressMessage("Correctness", "UNT0008:Null propagation on Unity objects", Justification = "Verified as working.")]
        public static int CountLanes(
            this ref NetSegment segment,
            ushort nodeId,
            NetInfo.LaneType laneTypeFilter,
            VehicleInfo.VehicleType vehicleTypeFilter,
            bool incoming = false) {

            NetInfo segmentInfo = segment.Info;

            byte numLanes = (byte)(segmentInfo?.m_lanes?.Length ?? 0);
            if (numLanes == 0)
                return 0;

            int count = 0;

            bool startNode = segment.IsStartNode(nodeId) ^ incoming;
            bool inverted = (segment.m_flags & NetSegment.Flags.Invert) != NetSegment.Flags.None;

            NetInfo.Direction filterDir = startNode
                ? NetInfo.Direction.Backward
                : NetInfo.Direction.Forward;

            if (inverted)
                filterDir = NetInfo.InvertDirection(filterDir);

            uint curLaneId = segment.m_lanes;
            byte laneIndex = 0;

            NetManager netManager = Singleton<NetManager>.instance;

            while (laneIndex < numLanes && curLaneId != 0u) {
                NetInfo.Lane laneInfo = segmentInfo.m_lanes[laneIndex];

                if ((laneInfo.m_finalDirection == filterDir) &&
                    (laneInfo.m_laneType & laneTypeFilter) != NetInfo.LaneType.None &&
                    (laneInfo.m_vehicleType & vehicleTypeFilter) != VehicleInfo.VehicleType.None) {

                    count++;
                }

                curLaneId = netManager.m_lanes.m_buffer[curLaneId].m_nextLane;
                ++laneIndex;
            }

            return count;
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
        [SuppressMessage("Correctness", "UNT0008:Null propagation on Unity objects", Justification = "Verified as working.")]
        public static IList<LanePos> GetSortedLanes(
            this ref NetSegment netSegment,
            bool? startNode,
            NetInfo.LaneType? laneTypeFilter = null,
            VehicleInfo.VehicleType? vehicleTypeFilter = null,
            bool reverse = false,
            bool sort = true) {
            // TODO refactor together with getSegmentNumVehicleLanes, especially the vehicle type and lane type checks

            NetInfo segmentInfo = netSegment.Info;

            if (!segmentInfo) {
                return EmptyLaneList;
            }

            byte numLanes = (byte)(segmentInfo.m_lanes?.Length ?? 0);
            if (numLanes == 0)
                return EmptyLaneList;

            var laneList = new List<LanePos>(numLanes);

            bool inverted = (netSegment.m_flags & NetSegment.Flags.Invert) != 0;

            NetInfo.Direction? filterDir = null;
            NetInfo.Direction sortDir;

            if (startNode.HasValue) {
                filterDir = startNode.Value
                    ? NetInfo.Direction.Backward
                    : NetInfo.Direction.Forward;

                if (inverted)
                    filterDir = NetInfo.InvertDirection(filterDir.Value);

                sortDir = NetInfo.InvertDirection(filterDir.Value);
            } else {
                sortDir = inverted
                    ? NetInfo.Direction.Backward
                    : NetInfo.Direction.Forward;
            }

            uint curLaneId = netSegment.m_lanes;
            byte laneIndex = 0;

            NetManager netManager = Singleton<NetManager>.instance;

            while (laneIndex < numLanes && curLaneId != 0u) {
                NetInfo.Lane laneInfo = segmentInfo.m_lanes[laneIndex];

                if ((laneTypeFilter == null || (laneInfo.m_laneType & laneTypeFilter) != 0) &&
                    (vehicleTypeFilter == null || (laneInfo.m_vehicleType & vehicleTypeFilter) != 0) &&
                    (filterDir == null || laneInfo.m_finalDirection == filterDir)) {

                    laneList.Add(new LanePos(
                        curLaneId,
                        laneIndex,
                        laneInfo.m_position,
                        laneInfo.m_vehicleType,
                        laneInfo.m_laneType));
                }

                curLaneId = netManager.m_lanes.m_buffer[curLaneId].m_nextLane;
                ++laneIndex;
            }

            laneList.TrimExcess();

            if (sort && laneList.Count > 0) {
                if (reverse)
                    sortDir = NetInfo.InvertDirection(sortDir);

                bool forward = sortDir == NetInfo.Direction.Forward;
                laneList.Sort(forward ? CompareLanePositionsForward : CompareLanePositionsBackward);
            }

            return laneList;
        }

        private static readonly ReadOnlyCollection<LanePos> EmptyLaneList = new(new List<LanePos>(0));
        private static Comparison<LanePos> CompareLanePositionsForward = (x, y) => CompareLanePositionsFun(x, y, true);
        private static Comparison<LanePos> CompareLanePositionsBackward = (x, y) => CompareLanePositionsFun(x, y, false);

        private static int CompareLanePositionsFun(LanePos x, LanePos y, bool fwd) {
            if (Math.Abs(x.position - y.position) < 1e-12) {
                if (x.position > 0) {
                    // mirror type-bound lanes (e.g. for coherent display of lane-wise speed limits)
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
    }
}
