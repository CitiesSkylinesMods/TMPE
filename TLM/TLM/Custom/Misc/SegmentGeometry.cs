using ColossalFramework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using TrafficManager.State;
using TrafficManager.Traffic;
using TrafficManager.TrafficLight;

namespace TrafficManager.Custom.Misc {
	public class SegmentGeometry {
		private ushort segmentId;

		private static int[] recalcInterval = { 5000, 7500, 10000, 12500, 15000 };

		public int updateCounter = 0;

		private HashSet<ushort> startNodeSegments = new HashSet<ushort>();
		private HashSet<ushort> endNodeSegments = new HashSet<ushort>();

		private HashSet<ushort> startNodeLeftSegments = new HashSet<ushort>();
		private HashSet<ushort> startNodeIncomingLeftSegments = new HashSet<ushort>();
		private HashSet<ushort> startNodeRightSegments = new HashSet<ushort>();
		private HashSet<ushort> startNodeIncomingRightSegments = new HashSet<ushort>();
		private HashSet<ushort> startNodeStraightSegments = new HashSet<ushort>();
		private HashSet<ushort> startNodeIncomingStraightSegments = new HashSet<ushort>();

		private HashSet<ushort> endNodeLeftSegments = new HashSet<ushort>();
		private HashSet<ushort> endNodeIncomingLeftSegments = new HashSet<ushort>();
		private HashSet<ushort> endNodeRightSegments = new HashSet<ushort>();
		private HashSet<ushort> endNodeIncomingRightSegments = new HashSet<ushort>();
		private HashSet<ushort> endNodeStraightSegments = new HashSet<ushort>();
		private HashSet<ushort> endNodeIncomingStraightSegments = new HashSet<ushort>();


		public HashSet<ushort> StartNodeLeftSegments {
			get { ++updateCounter; return startNodeLeftSegments; }
			private set { startNodeLeftSegments = value; }
		}
		public HashSet<ushort> StartNodeIncomingLeftSegments {
			get { ++updateCounter; return startNodeIncomingLeftSegments; }
			private set { startNodeIncomingLeftSegments = value; }
		}
		public HashSet<ushort> StartNodeRightSegments {
			get { ++updateCounter; return startNodeRightSegments; }
			private set { startNodeRightSegments = value; }
		}
		public HashSet<ushort> StartNodeIncomingRightSegments {
			get { ++updateCounter; return startNodeIncomingRightSegments; }
			private set { startNodeIncomingRightSegments = value; }
		}
		public HashSet<ushort> StartNodeStraightSegments {
			get { ++updateCounter; return startNodeStraightSegments; }
			private set { startNodeStraightSegments = value; }
		}
		public HashSet<ushort> StartNodeIncomingStraightSegments {
			get { ++updateCounter; return startNodeIncomingStraightSegments; }
			private set { startNodeIncomingStraightSegments = value; }
		}

		public HashSet<ushort> EndNodeLeftSegments {
			get { ++updateCounter; return endNodeLeftSegments; }
			private set { endNodeLeftSegments = value; }
		}
		public HashSet<ushort> EndNodeIncomingLeftSegments {
			get { ++updateCounter; return endNodeIncomingLeftSegments; }
			private set { endNodeIncomingLeftSegments = value; }
		}
		public HashSet<ushort> EndNodeRightSegments {
			get { ++updateCounter; return endNodeRightSegments; }
			private set { endNodeRightSegments = value; }
		}
		public HashSet<ushort> EndNodeIncomingRightSegments {
			get { ++updateCounter; return endNodeIncomingRightSegments; }
			private set { endNodeIncomingRightSegments = value; }
		}
		public HashSet<ushort> EndNodeStraightSegments {
			get { ++updateCounter; return endNodeStraightSegments; }
			private set { endNodeStraightSegments = value; }
		}
		public HashSet<ushort> EndNodeIncomingStraightSegments {
			get { ++updateCounter; return endNodeIncomingStraightSegments; }
			private set { endNodeIncomingStraightSegments = value; }
		}

		//private static ushort debugSegId = 22980;

		public SegmentGeometry(ushort segmentId) {
			updateCounter = segmentId % recalcInterval[Options.simAccuracy];
			this.segmentId = segmentId;
			Recalculate(false);
		}

		public ushort startNodeId() {
			return Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].m_startNode;
		}

		public ushort endNodeId() {
			return Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].m_endNode;
		}

		private object recalcLock = new object();
		private bool recalculating = false;

		public void Recalculate(bool output = true) {
			if ((Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].m_flags & NetSegment.Flags.Created) == NetSegment.Flags.None)
				return;
			if (recalculating)
				return;
			else {
#if DEBUG
				uint lockIter = 0;
#endif
				try {
					while (!Monitor.TryEnter(recalcLock, CustomPathFind.SYNC_TIMEOUT)) {
#if DEBUG
						++lockIter;
						if (lockIter % 100 == 0)
							Log.Message("SegmentGeometry.Recalculate lockIter: " + lockIter);
#endif
					}

					if (recalculating)
						return;
					recalculating = true;

					if (output)
						Log.Warning($"Recalculating geometries of segment {segmentId} STARTED");

					recalculate(ref startNodeSegments, ref startNodeRightSegments, ref startNodeIncomingRightSegments, ref startNodeLeftSegments, ref startNodeIncomingLeftSegments, ref startNodeStraightSegments, ref startNodeIncomingStraightSegments, startNodeId());
					recalculate(ref endNodeSegments, ref endNodeRightSegments, ref endNodeIncomingRightSegments, ref endNodeLeftSegments, ref endNodeIncomingLeftSegments, ref endNodeStraightSegments, ref endNodeIncomingStraightSegments, endNodeId());

					if (output)
						Log.Warning($"Recalculating geometries of segment {segmentId} FINISHED");
				} finally {
					recalculating = false;
					Monitor.Exit(recalcLock);
				}
			}

			/*if (segmentId == 35053) {
				Log.Message($"Segment geometry for {segmentId}: snr: {startNodeRightSegments} sns: {startNodeHasStraightSegment} snl: {startNodeHasLeftSegments}");
				Log.Message($"Segment geometry for {segmentId}: enr: {endNodeRightSegments} ens: {endNodeHasStraightSegment} enl: {endNodeHasLeftSegments}");
			}*/
		}

		private void recalculate(ref HashSet<ushort> nodeSegments, ref HashSet<ushort> right, ref HashSet<ushort> incomingRight, ref HashSet<ushort> left, ref HashSet<ushort> incomingLeft, ref HashSet<ushort> straight, ref HashSet<ushort> incomingStraight, ushort nodeId) {
			nodeSegments.Clear();
			right.Clear();
			incomingRight.Clear();
			left.Clear();
			incomingLeft.Clear();
			straight.Clear();
			incomingStraight.Clear();

			if (nodeId == 0)
				return;

			NetManager netManager = Singleton<NetManager>.instance;

			ItemClass connectionClass = netManager.m_segments.m_buffer[segmentId].Info.GetConnectionClass();

			for (var s = 0; s < 8; s++) {
				var otherSegmentId = netManager.m_nodes.m_buffer[nodeId].GetSegment(s);
				if (otherSegmentId == 0 || otherSegmentId == segmentId)
					continue;
				/*ItemClass otherConnectionClass = Singleton<NetManager>.instance.m_segments.m_buffer[otherSegmentId].Info.GetConnectionClass();
				if (otherConnectionClass.m_service != connectionClass.m_service)
					continue;*/

				// reset highway lane arrows
				int i = 0;
				uint curLaneId = netManager.m_segments.m_buffer[otherSegmentId].m_lanes;
				uint wIter = 0;
				while (i < netManager.m_segments.m_buffer[otherSegmentId].Info.m_lanes.Length && curLaneId != 0u) {
					++wIter;
					if (wIter >= 20) {
						Log.Error("Too many iterations in SegmentGeometry.recalculate!");
						break;
					}

					Flags.removeHighwayLaneArrowFlags(curLaneId);
					curLaneId = netManager.m_lanes.m_buffer[curLaneId].m_nextLane;
					++i;
				} // foreach lane

				// determine geometry
				if (TrafficPriority.IsRightSegment(segmentId, otherSegmentId, nodeId)) {
					right.Add(otherSegmentId);
					if (!TrafficLightsManual.SegmentIsOutgoingOneWay(otherSegmentId, nodeId))
						incomingRight.Add(otherSegmentId);
				} else if (TrafficPriority.IsLeftSegment(segmentId, otherSegmentId, nodeId)) {
					left.Add(otherSegmentId);
					if (!TrafficLightsManual.SegmentIsOutgoingOneWay(otherSegmentId, nodeId))
						incomingLeft.Add(otherSegmentId);
				} else {
					straight.Add(otherSegmentId);
					if (!TrafficLightsManual.SegmentIsOutgoingOneWay(otherSegmentId, nodeId))
						incomingStraight.Add(otherSegmentId);
				}

				nodeSegments.Add(otherSegmentId);
			}
		}

		public bool VerifySegments(ushort otherSegmentId) {
			if ((Singleton<NetManager>.instance.m_segments.m_buffer[otherSegmentId].m_flags & NetSegment.Flags.Created) == NetSegment.Flags.None) {
				return false;
			}
			if (otherSegmentId == segmentId)
				return false;

			if (!startNodeSegments.Contains(otherSegmentId) && !endNodeSegments.Contains(otherSegmentId)) {
				Recalculate();
				return true;
			}
			return false;
		}

		internal void VerifySegmentsByCount(ushort nodeId) {
			if ((Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId].m_flags & NetNode.Flags.Created) == NetNode.Flags.None) {
				return;
			}

			if (startNodeId() != nodeId && endNodeId() != nodeId) {
				Recalculate();
				return;
			}

			int expectedCount = Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId].CountSegments(NetSegment.Flags.Created, segmentId);
			if (CountOtherSegments(nodeId) != expectedCount)
				Recalculate();
		}

		private int CountOtherSegments(ushort nodeId) {
			if ((Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId].m_flags & NetNode.Flags.Created) == NetNode.Flags.None) {
				return 0;
			}
			if (startNodeId() != nodeId && endNodeId() != nodeId)
				Recalculate();

			if (startNodeId() == nodeId)
				return startNodeSegments.Count;
			else if (endNodeId() == nodeId)
				return endNodeSegments.Count;
			else {
				Log.Warning($"CountOtherSegments: Node {nodeId} is neither start nor end node of segment {segmentId}.");
				return 0;
			}
		}

		public int CountRightSegments(ushort nodeId) {
			if ((Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId].m_flags & NetNode.Flags.Created) == NetNode.Flags.None) {
				return 0;
			}
			if (startNodeId() != nodeId && endNodeId() != nodeId)
				Recalculate();

			if (startNodeId() == nodeId)
				return startNodeRightSegments.Count;
			else if (endNodeId() == nodeId)
				return endNodeRightSegments.Count;
			else {
				Log.Warning($"CountRightSegments: Node {nodeId} is neither start nor end node of segment {segmentId}.");
				return 0;
			}
		}

		public int CountLeftSegments(ushort nodeId) {
			if ((Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId].m_flags & NetNode.Flags.Created) == NetNode.Flags.None) {
				return 0;
			}
			if (startNodeId() != nodeId && endNodeId() != nodeId)
				Recalculate();

			if (startNodeId() == nodeId)
				return startNodeLeftSegments.Count;
			else if (endNodeId() == nodeId)
				return endNodeLeftSegments.Count;
			else {
				Log.Warning($"CountLeftSegments: Node {nodeId} is neither start nor end node of segment {segmentId}.");
				return 0;
			}
		}

		public int CountStraightSegments(ushort nodeId) {
			if ((Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId].m_flags & NetNode.Flags.Created) == NetNode.Flags.None) {
				return 0;
			}
			if (startNodeId() != nodeId && endNodeId() != nodeId)
				Recalculate();

			if (startNodeId() == nodeId)
				return startNodeStraightSegments.Count;
			else if (endNodeId() == nodeId)
				return endNodeStraightSegments.Count;
			else {
				Log.Warning($"CountStraightSegments: Node {nodeId} is neither start nor end node of segment {segmentId}.");
				return 0;
			}
		}

		public int CountIncomingRightSegments(ushort nodeId) {
			if ((Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId].m_flags & NetNode.Flags.Created) == NetNode.Flags.None) {
				return 0;
			}
			if (startNodeId() != nodeId && endNodeId() != nodeId)
				Recalculate();

			if (startNodeId() == nodeId)
				return startNodeIncomingRightSegments.Count;
			else if (endNodeId() == nodeId)
				return endNodeIncomingRightSegments.Count;
			else {
				Log.Warning($"CountIncomingRightSegments: Node {nodeId} is neither start nor end node of segment {segmentId}.");
				return 0;
			}
		}

		public int CountIncomingLeftSegments(ushort nodeId) {
			if ((Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId].m_flags & NetNode.Flags.Created) == NetNode.Flags.None) {
				return 0;
			}
			if (startNodeId() != nodeId && endNodeId() != nodeId)
				Recalculate();

			if (startNodeId() == nodeId)
				return startNodeIncomingLeftSegments.Count;
			else if (endNodeId() == nodeId)
				return endNodeIncomingLeftSegments.Count;
			else {
				Log.Warning($"CountIncomingLeftSegments: Node {nodeId} is neither start nor end node of segment {segmentId}.");
				return 0;
			}
		}

		public int CountIncomingStraightSegments(ushort nodeId) {
			if ((Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId].m_flags & NetNode.Flags.Created) == NetNode.Flags.None) {
				return 0;
			}
			if (startNodeId() != nodeId && endNodeId() != nodeId)
				Recalculate();

			if (startNodeId() == nodeId)
				return startNodeIncomingStraightSegments.Count;
			else if (endNodeId() == nodeId)
				return endNodeIncomingStraightSegments.Count;
			else {
				Log.Warning($"CountIncomingStraightSegments: Node {nodeId} is neither start nor end node of segment {segmentId}.");
				return 0;
			}
		}

		public bool HasLeftSegment(ushort nodeId) {
			return CountLeftSegments(nodeId) > 0;
		}

		public bool HasRightSegment(ushort nodeId) {
			return CountRightSegments(nodeId) > 0;
		}

		public bool HasStraightSegment(ushort nodeId) {
			return CountStraightSegments(nodeId) > 0;
		}

		public bool HasIncomingLeftSegment(ushort nodeId) {
			return CountIncomingLeftSegments(nodeId) > 0;
		}

		public bool HasIncomingRightSegment(ushort nodeId) {
			return CountIncomingRightSegments(nodeId) > 0;
		}

		public bool HasIncomingStraightSegment(ushort nodeId) {
			return CountIncomingStraightSegments(nodeId) > 0;
		}

		public bool IsLeftSegment(ushort toSegmentId, ushort nodeId) {
			if ((Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId].m_flags & NetNode.Flags.Created) == NetNode.Flags.None) {
				return false;
			}
			if ((Singleton<NetManager>.instance.m_segments.m_buffer[toSegmentId].m_flags & NetSegment.Flags.Created) == NetSegment.Flags.None) {
				return false;
			}
			if (toSegmentId == segmentId)
				return false;

			if (startNodeId() != nodeId && endNodeId() != nodeId)
				Recalculate();
			else
				VerifySegments(toSegmentId);

			if (startNodeId() == nodeId)
				return startNodeLeftSegments.Contains(toSegmentId);
			else if (endNodeId() == nodeId)
				return endNodeLeftSegments.Contains(toSegmentId);
			else {
				//if (segmentId == debugSegId)
					Log.Warning($"IsLeftSegment: Node {nodeId} (segment {toSegmentId}) is neither start nor end node of segment {segmentId}.");
				return false;
			}
		}

		public bool IsRightSegment(ushort toSegmentId, ushort nodeId) {
			if ((Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId].m_flags & NetNode.Flags.Created) == NetNode.Flags.None) {
				return false;
			}
			if ((Singleton<NetManager>.instance.m_segments.m_buffer[toSegmentId].m_flags & NetSegment.Flags.Created) == NetSegment.Flags.None) {
				return false;
			}
			if (toSegmentId == segmentId)
				return false;

			if (startNodeId() != nodeId && endNodeId() != nodeId)
				Recalculate();
			else
				VerifySegments(toSegmentId);

			if (startNodeId() == nodeId)
				return startNodeRightSegments.Contains(toSegmentId);
			else if (endNodeId() == nodeId)
				return endNodeRightSegments.Contains(toSegmentId);
			else {
				//if (segmentId == debugSegId)
					Log.Warning($"IsRightSegment: Node {nodeId} (segment {toSegmentId}) is neither start nor end node of segment {segmentId}.");
				return false;
			}
		}

		public bool IsStraightSegment(ushort toSegmentId, ushort nodeId) {
			if ((Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId].m_flags & NetNode.Flags.Created) == NetNode.Flags.None) {
				return false;
			}
			if ((Singleton<NetManager>.instance.m_segments.m_buffer[toSegmentId].m_flags & NetSegment.Flags.Created) == NetSegment.Flags.None) {
				return false;
			}
			if (toSegmentId == segmentId)
				return false;

			if (startNodeId() != nodeId && endNodeId() != nodeId)
				Recalculate();
			else
				VerifySegments(toSegmentId);

			if (startNodeId() == nodeId)
				return startNodeStraightSegments.Contains(toSegmentId);
			else if (endNodeId() == nodeId)
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
			if ((Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId].m_flags & NetNode.Flags.Created) == NetNode.Flags.None) {
				return Direction.Forward;
			}
			if ((Singleton<NetManager>.instance.m_segments.m_buffer[toSegmentId].m_flags & NetSegment.Flags.Created) == NetSegment.Flags.None) {
				return Direction.Forward;
			}

			if (startNodeId() != nodeId && endNodeId() != nodeId)
				Recalculate();
			else
				VerifySegments(toSegmentId);

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
