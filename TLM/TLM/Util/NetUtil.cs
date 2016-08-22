using ColossalFramework;
using System;
using System.Collections.Generic;
using System.Text;

namespace TrafficManager.Util {
	public static class NetUtil {
		public delegate void NetSegmentHandler(ushort segmentId, ref NetSegment segment);
		public delegate void NetNodeHandler(ushort nodeId, ref NetNode node);
		public delegate void NetLaneHandler(uint laneId, ref NetLane lane);

		static bool IsSegmentValid(ref NetSegment segment) {
			return (segment.m_flags & (NetSegment.Flags.Created | NetSegment.Flags.Deleted)) == NetSegment.Flags.Created;
		}

		public static bool IsSegmentValid(ushort segmentId) {
			return IsSegmentValid(ref Singleton<NetManager>.instance.m_segments.m_buffer[segmentId]);
		}

		public static void ProcessSegment(ushort segmentId, NetSegmentHandler handler) {
			ProcessSegment(segmentId, ref Singleton<NetManager>.instance.m_segments.m_buffer[segmentId], handler);
		}

		public static void ProcessSegment(ushort segmentId, ref NetSegment segment, NetSegmentHandler handler) {
			handler(segmentId, ref segment);
		}

		static bool IsNodeValid(ref NetNode node) {
			return (node.m_flags & (NetNode.Flags.Created | NetNode.Flags.Deleted)) == NetNode.Flags.Created;
		}

		public static bool IsNodeValid(ushort nodeId) {
			return IsNodeValid(ref Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId]);
		}

		public static void ProcessNode(ushort nodeId, NetNodeHandler handler) {
			ProcessNode(nodeId, ref Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId], handler);
		}

		public static void ProcessNode(ushort nodeId, ref NetNode node, NetNodeHandler handler) {
			handler(nodeId, ref node);
		}

		static bool IsLaneValid(ref NetLane lane) {
			if ((lane.m_flags & (uint)(NetLane.Flags.Created | NetLane.Flags.Deleted)) != (uint)NetLane.Flags.Created) {
				return false;
			}
			return IsSegmentValid(lane.m_segment);
		}

		public static bool IsLaneValid(uint laneId) {
			return IsLaneValid(ref Singleton<NetManager>.instance.m_lanes.m_buffer[laneId]);
		}

		public static void ProcessLane(uint laneId, NetLaneHandler handler) {
			ProcessLane(laneId, ref Singleton<NetManager>.instance.m_lanes.m_buffer[laneId], handler);
		}

		public static void ProcessLane(uint laneId, ref NetLane lane, NetLaneHandler handler) {
			handler(laneId, ref lane);
		}
	}
}
