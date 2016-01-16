using ColossalFramework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.Traffic;

namespace TrafficManager.Custom.Misc {
	public class SegmentGeometry {
		private ushort segmentId;

		public ushort startNodeId = 0;
		public ushort endNodeId = 0;

		public byte startNodeNumLeftSegments = 0;
		public bool startNodeHasStraightSegment = false;
		public byte startNodeNumRightSegments = 0;

		public HashSet<ushort> startNodeLeftSegments = new HashSet<ushort>();
		public HashSet<ushort> startNodeRightSegments = new HashSet<ushort>();
		public HashSet<ushort> startNodeStraightSegments = new HashSet<ushort>();

		public byte endNodeNumLeftSegments = 0;
		public bool endNodeHasStraightSegment = false;
		public byte endNodeNumRightSegments = 0;

		public HashSet<ushort> endNodeLeftSegments = new HashSet<ushort>();
		public HashSet<ushort> endNodeRightSegments = new HashSet<ushort>();
		public HashSet<ushort> endNodeStraightSegments = new HashSet<ushort>();

		//private static ushort debugSegId = 22980;

		public SegmentGeometry(ushort segmentId) {
			this.segmentId = segmentId;
			recalculate();
		}

		public void recalculate() {
			if ((Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].m_flags & NetSegment.Flags.Created) == NetSegment.Flags.None)
				return;
			startNodeId = Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].m_startNode;
			endNodeId = Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].m_endNode;

			recalculate(out startNodeNumRightSegments, ref startNodeRightSegments, out startNodeNumLeftSegments, ref startNodeLeftSegments, out startNodeHasStraightSegment, ref startNodeStraightSegments, startNodeId);
			recalculate(out endNodeNumRightSegments, ref endNodeRightSegments, out endNodeNumLeftSegments, ref endNodeLeftSegments, out endNodeHasStraightSegment, ref endNodeStraightSegments, endNodeId);

			/*if (segmentId == 35053) {
				Log.Message($"Segment geometry for {segmentId}: snr: {startNodeRightSegments} sns: {startNodeHasStraightSegment} snl: {startNodeHasLeftSegments}");
				Log.Message($"Segment geometry for {segmentId}: enr: {endNodeRightSegments} ens: {endNodeHasStraightSegment} enl: {endNodeHasLeftSegments}");
			}*/
		}

		private void recalculate(out byte numRight, ref HashSet<ushort> right, out byte numLeft, ref HashSet<ushort> left, out bool hasStraightSegment, ref HashSet<ushort> straight, ushort nodeId) {
			numRight = 0;
			hasStraightSegment = false;
			numLeft = 0;
			right.Clear();
			left.Clear();
			straight.Clear();

			if (nodeId == 0)
				return;

			NetNode node = Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId];
			ItemClass connectionClass = Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].Info.GetConnectionClass();
			
			for (var s = 0; s < 8; s++) {
				var otherSegmentId = node.GetSegment(s);
				if (otherSegmentId == 0 || otherSegmentId == segmentId)
					continue;
				ItemClass otherConnectionClass = Singleton<NetManager>.instance.m_segments.m_buffer[otherSegmentId].Info.GetConnectionClass();
				if (otherConnectionClass.m_service != connectionClass.m_service)
					continue;

				if (TrafficPriority.IsRightSegment(segmentId, otherSegmentId, nodeId)) {
					right.Add(otherSegmentId);
					++numRight;
				} else if (TrafficPriority.IsLeftSegment(segmentId, otherSegmentId, nodeId)) {
					left.Add(otherSegmentId);
					++numLeft;
				} else {
					straight.Add(otherSegmentId);
					hasStraightSegment = true;
				}
			}
		}

		public bool HasLeftSegment(ushort nodeId) {
			if (startNodeId == nodeId)
				return startNodeNumLeftSegments > 0;
			else if (endNodeId == nodeId)
				return endNodeNumLeftSegments > 0;
			else {
				//if (segmentId == debugSegId)
					Log.Warning($"HasLeftSegment: Node {nodeId} is neither start nor end node of segment {segmentId}.");
                return false;
			}
		}

		public bool HasRightSegment(ushort nodeId) {
			if (startNodeId == nodeId)
				return startNodeNumRightSegments > 0;
			else if (endNodeId == nodeId)
				return endNodeNumRightSegments > 0;
			else {
				//if (segmentId == debugSegId)
					Log.Warning($"HasRightSegment: Node {nodeId} is neither start nor end node of segment {segmentId}.");
				return false;
			}
		}

		public bool HasStraightSegment(ushort nodeId) {
			if (startNodeId == nodeId)
				return startNodeHasStraightSegment;
			else if (endNodeId == nodeId)
				return endNodeHasStraightSegment;
			else {
				//if (segmentId == debugSegId)
					Log.Warning($"HasStraightSegment: Node {nodeId} is neither start nor end node of segment {segmentId}.");
				return false;
			}
		}

		public bool IsLeftSegment(ushort toSegmentId, ushort nodeId) {
			if (startNodeId == nodeId)
				return startNodeLeftSegments.Contains(toSegmentId);
			else if (endNodeId == nodeId)
				return endNodeLeftSegments.Contains(toSegmentId);
			else {
				//if (segmentId == debugSegId)
					Log.Warning($"IsLeftSegment: Node {nodeId} (segment {toSegmentId}) is neither start nor end node of segment {segmentId}.");
				return false;
			}
		}

		public bool IsRightSegment(ushort toSegmentId, ushort nodeId) {
			if (startNodeId == nodeId)
				return startNodeRightSegments.Contains(toSegmentId);
			else if (endNodeId == nodeId)
				return endNodeRightSegments.Contains(toSegmentId);
			else {
				//if (segmentId == debugSegId)
					Log.Warning($"IsRightSegment: Node {nodeId} (segment {toSegmentId}) is neither start nor end node of segment {segmentId}.");
				return false;
			}
		}

		public bool IsStraightSegment(ushort toSegmentId, ushort nodeId) {
			if (startNodeId == nodeId)
				return startNodeStraightSegments.Contains(toSegmentId);
			else if (endNodeId == nodeId)
				return endNodeStraightSegments.Contains(toSegmentId);
			else {
				//if (segmentId == debugSegId)
					Log.Warning($"IsStraightSegment: Node {nodeId} (segment {toSegmentId}) is neither start nor end node of segment {segmentId}.");
				return false;
			}
		}

		/// <summary>
		/// Determines the direction vehicles are turning when changing from segment `fromSegment` to segment `toSegment` at node `nodeId`.
		/// </summary>
		/// <param name="fromSegment"></param>
		/// <param name="toSegment"></param>
		/// <returns></returns>
		public Direction GetDirection(ushort toSegmentId, ushort nodeId) {
			if (toSegmentId == segmentId)
				return Direction.Turn;
			else if (IsRightSegment(toSegmentId, nodeId))
				return Direction.Right;
			else if (IsLeftSegment(toSegmentId, nodeId))
				return Direction.Left;
			else
				return Direction.Forward;
		}
	}
}
