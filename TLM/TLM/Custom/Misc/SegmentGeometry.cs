using ColossalFramework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.Traffic;

namespace TrafficManager.Custom.Misc {
	public class SegmentGeometry {
		private ushort segmentId;

		public bool startNodeHasLeftSegments = false;
		public bool startNodeHasStraightSegment = false;
		public byte startNodeRightSegments = 0;

		public bool endNodeHasLeftSegments = false;
		public bool endNodeHasStraightSegment = false;
		public byte endNodeRightSegments = 0;

		public SegmentGeometry(ushort segmentId) {
			this.segmentId = segmentId;
			recalculate();
		}

		public void recalculate() {
			if (Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].m_flags == NetSegment.Flags.None)
				return;

			ushort startNodeId = Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].m_startNode;
			ushort endNodeId = Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].m_endNode;

			recalculate(out startNodeRightSegments, out startNodeHasLeftSegments, out startNodeHasStraightSegment, startNodeId);
			recalculate(out endNodeRightSegments, out endNodeHasLeftSegments, out endNodeHasStraightSegment, endNodeId);

			/*if (segmentId == 35053) {
				Log.Message($"Segment geometry for {segmentId}: snr: {startNodeRightSegments} sns: {startNodeHasStraightSegment} snl: {startNodeHasLeftSegments}");
				Log.Message($"Segment geometry for {segmentId}: enr: {endNodeRightSegments} ens: {endNodeHasStraightSegment} enl: {endNodeHasLeftSegments}");
			}*/
		}

		private void recalculate(out byte rightSegments, out bool hasLeftSegments, out bool hasStraightSegment, ushort nodeId) {
			rightSegments = 0;
			hasStraightSegment = false;
			hasLeftSegments = false;

			if (nodeId == 0)
				return;

			ItemClass connectionClass = Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].Info.GetConnectionClass();

			NetNode node = Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId];
			for (var s = 0; s < 8; s++) {
				var otherSegmentId = node.GetSegment(s);
				if (otherSegmentId == 0 || otherSegmentId == segmentId)
					continue;
				ItemClass otherConnectionClass = Singleton<NetManager>.instance.m_segments.m_buffer[otherSegmentId].Info.GetConnectionClass();
				if (otherConnectionClass.m_service != connectionClass.m_service)
					continue;

				if (TrafficPriority.IsRightSegment(segmentId, otherSegmentId, nodeId)) {
					++rightSegments;
				} else if (TrafficPriority.IsLeftSegment(segmentId, otherSegmentId, nodeId)) {
					hasLeftSegments = true;
				} else {
					hasStraightSegment = true;
				}
			}
		}
	}
}
