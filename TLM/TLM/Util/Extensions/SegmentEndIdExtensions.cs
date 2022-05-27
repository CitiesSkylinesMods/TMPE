using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.API.Traffic;
using TrafficManager.API.Traffic.Data;
using TrafficManager.Geometry.Impl;
using TrafficManager.Network.Data;

namespace TrafficManager.Util.Extensions {
    /// <summary>
    /// This class exists so that if we want to move <see cref="Network.Data.SegmentEndId"/> to the API,
    /// we've already prepared for that by not including a lot of implementation-specific methods.
    /// </summary>
    internal static class SegmentEndIdExtensions {

        public static ref NetSegment GetSegment(this SegmentEndId segmentEndId) => ref segmentEndId.SegmentId.ToSegment();

        public static ushort GetNodeId(this SegmentEndId segmentEndId) =>
            segmentEndId.StartNode ? segmentEndId.GetSegment().m_startNode : segmentEndId.GetSegment().m_endNode;

        public static ref NetNode GetNode(this SegmentEndId segmentEndId) => ref segmentEndId.GetNodeId().ToNode();

        public static ISegmentEndId ToApi(this SegmentEndId segmentEndId) =>
            new SegmentEndIdApi(segmentEndId.SegmentId, segmentEndId.StartNode);

        public static SegmentEndId FromApi(this ISegmentEndId segmentEndId) =>
            new SegmentEndId(segmentEndId.SegmentId, segmentEndId.StartNode);

        public static SegmentEndId AtStartNode(this ushort segmentId) => new SegmentEndId(segmentId, true);

        public static SegmentEndId AtEndNode(this ushort segmentId) => new SegmentEndId(segmentId, false);

        public static SegmentEndId AtNode(this ushort segmentId, bool startNode) => new SegmentEndId(segmentId, startNode);

        public static SegmentEndId AtNode(this ushort segmentId, ushort nodeId) {
            if (segmentId == 0)
                return default;
            ref var segment = ref segmentId.ToSegment();
            if (nodeId == segment.m_startNode)
                return segmentId.AtStartNode();
            else if (nodeId == segment.m_endNode)
                return segmentId.AtEndNode();
            throw new ArgumentException($"Segment {segmentId} is not on node {nodeId}", nameof(nodeId));
        }

        public static SegmentEndId GetSegmentEnd(this ushort nodeId, int segmentIndex) =>
            nodeId.ToNode().GetSegment(segmentIndex).AtNode(nodeId);

        public static SegmentEndId GetSegmentEndId(this ExtSegmentEnd extSegmentEnd) =>
            extSegmentEnd.segmentId.AtNode(extSegmentEnd.startNode);
    }
}
