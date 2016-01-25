#define DEBUGLOCKSx

using ColossalFramework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using TrafficManager.Custom.AI;
using TrafficManager.State;
using TrafficManager.Traffic;
using TrafficManager.TrafficLight;

namespace TrafficManager.Custom.Misc {
	public class SegmentGeometry {
		private ushort segmentId;

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

		private ushort[] startNodeIncomingLeftSegmentsArray = new ushort[7];
		private ushort[] startNodeIncomingRightSegmentsArray = new ushort[7];
		private ushort[] startNodeIncomingStraightSegmentsArray = new ushort[7];

		private ushort[] endNodeIncomingLeftSegmentsArray = new ushort[7];
		private ushort[] endNodeIncomingRightSegmentsArray = new ushort[7];
		private ushort[] endNodeIncomingStraightSegmentsArray = new ushort[7];

		private bool oneWay = false;
		private bool outgoingOneWayAtStartNode = false;
		private bool outgoingOneWayAtEndNode = false;

		public HashSet<ushort> StartNodeLeftSegments {
			get { return startNodeLeftSegments; }
			private set { startNodeLeftSegments = value; }
		}
		public HashSet<ushort> StartNodeIncomingLeftSegments {
			get { return startNodeIncomingLeftSegments; }
			private set { startNodeIncomingLeftSegments = value; }
		}
		public HashSet<ushort> StartNodeRightSegments {
			get { return startNodeRightSegments; }
			private set { startNodeRightSegments = value; }
		}
		public HashSet<ushort> StartNodeIncomingRightSegments {
			get { return startNodeIncomingRightSegments; }
			private set { startNodeIncomingRightSegments = value; }
		}
		public HashSet<ushort> StartNodeStraightSegments {
			get { return startNodeStraightSegments; }
			private set { startNodeStraightSegments = value; }
		}
		public HashSet<ushort> StartNodeIncomingStraightSegments {
			get { return startNodeIncomingStraightSegments; }
			private set { startNodeIncomingStraightSegments = value; }
		}

		public HashSet<ushort> EndNodeLeftSegments {
			get { return endNodeLeftSegments; }
			private set { endNodeLeftSegments = value; }
		}
		public HashSet<ushort> EndNodeIncomingLeftSegments {
			get { return endNodeIncomingLeftSegments; }
			private set { endNodeIncomingLeftSegments = value; }
		}
		public HashSet<ushort> EndNodeRightSegments {
			get { return endNodeRightSegments; }
			private set { endNodeRightSegments = value; }
		}
		public HashSet<ushort> EndNodeIncomingRightSegments {
			get { return endNodeIncomingRightSegments; }
			private set { endNodeIncomingRightSegments = value; }
		}
		public HashSet<ushort> EndNodeStraightSegments {
			get { return endNodeStraightSegments; }
			private set { endNodeStraightSegments = value; }
		}
		public HashSet<ushort> EndNodeIncomingStraightSegments {
			get { return endNodeIncomingStraightSegments; }
			private set { endNodeIncomingStraightSegments = value; }
		}

		public ushort[] StartNodeIncomingLeftSegmentsArray {
			get { return startNodeIncomingLeftSegmentsArray; }
			private set { startNodeIncomingLeftSegmentsArray = value; }
		}
		public ushort[] StartNodeIncomingRightSegmentsArray {
			get { return startNodeIncomingRightSegmentsArray; }
			private set { startNodeIncomingRightSegmentsArray = value; }
		}
		public ushort[] StartNodeIncomingStraightSegmentsArray {
			get { return startNodeIncomingStraightSegmentsArray; }
			private set { startNodeIncomingStraightSegmentsArray = value; }
		}

		public ushort[] EndNodeIncomingLeftSegmentsArray {
			get { return endNodeIncomingLeftSegmentsArray; }
			private set { endNodeIncomingLeftSegmentsArray = value; }
		}
		public ushort[] EndNodeIncomingRightSegmentsArray {
			get { return endNodeIncomingRightSegmentsArray; }
			private set { endNodeIncomingRightSegmentsArray = value; }
		}
		public ushort[] EndNodeIncomingStraightSegmentsArray {
			get { return endNodeIncomingStraightSegmentsArray; }
			private set { endNodeIncomingStraightSegmentsArray = value; }
		}

		//private static ushort debugSegId = 22980;

		public SegmentGeometry(ushort segmentId) {
			this.segmentId = segmentId;
			Recalculate(false, true);
		}

		public ushort startNodeId() {
			return Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].m_startNode;
		}

		public ushort endNodeId() {
			return Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].m_endNode;
		}

		public readonly object Lock = new object();
		private bool recalculating = false;
		private uint lastRecalculation = 0;

		public void Recalculate(bool output = true, bool force = false) {
			if ((Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].m_flags & NetSegment.Flags.Created) == NetSegment.Flags.None) {
				cleanup();
				return;
			}

			uint frame = Singleton<SimulationManager>.instance.m_currentFrameIndex >> 6;
			if (!force && (recalculating || lastRecalculation >= frame))
				return;
			
#if DEBUGLOCKS
				uint lockIter = 0;
#endif
			try {
#if DEBUG
				if (output)
					Log.Warning($"Trying to get a lock for Recalculating geometries of segment {segmentId}...");
#endif
				Monitor.Enter(Lock);
#if DEBUGLOCKS
					++lockIter;
					if (lockIter % 100 == 0)
						Log._Debug("SegmentGeometry.Recalculate lockIter: " + lockIter);
#endif
				if (!force && (recalculating || lastRecalculation >= frame))
					return;
				recalculating = true;

#if DEBUG
				if (output)
					Log.Info($"Recalculating geometries of segment {segmentId} STARTED");
#endif

				cleanup();
				oneWay = calculateIsOneWay(segmentId);
				recalculate(ref outgoingOneWayAtStartNode, ref startNodeSegments, ref startNodeRightSegments, ref startNodeIncomingRightSegments, ref startNodeIncomingRightSegmentsArray, ref startNodeLeftSegments, ref startNodeIncomingLeftSegments, ref startNodeIncomingLeftSegmentsArray, ref startNodeStraightSegments, ref startNodeIncomingStraightSegments, ref startNodeIncomingStraightSegmentsArray, startNodeId());
				recalculate(ref outgoingOneWayAtEndNode, ref endNodeSegments, ref endNodeRightSegments, ref endNodeIncomingRightSegments, ref endNodeIncomingRightSegmentsArray, ref endNodeLeftSegments, ref endNodeIncomingLeftSegments, ref endNodeIncomingLeftSegmentsArray, ref endNodeStraightSegments, ref endNodeIncomingStraightSegments, ref endNodeIncomingStraightSegmentsArray, endNodeId());

#if DEBUG
				if (output) {
					Log.Info($"Recalculating geometries of segment {segmentId} FINISHED");
					Log._Debug($"seg. {segmentId}. outgoingOneWayAtStartNode={outgoingOneWayAtStartNode}");
					Log._Debug($"seg. {segmentId}. oneWay={oneWay}");
					Log._Debug($"seg. {segmentId}. startNodeSegments={ string.Join(", ", startNodeSegments.Select(x => x.ToString()).ToArray())}");
					Log._Debug($"seg. {segmentId}. startNodeRightSegments={ string.Join(", ", startNodeRightSegments.Select(x => x.ToString()).ToArray())}");
					Log._Debug($"seg. {segmentId}. startNodeIncomingRightSegments={ string.Join(", ", startNodeIncomingRightSegments.Select(x => x.ToString()).ToArray())}");
					Log._Debug($"seg. {segmentId}. startNodeLeftSegments={ string.Join(", ", startNodeLeftSegments.Select(x => x.ToString()).ToArray())}");
					Log._Debug($"seg. {segmentId}. startNodeIncomingLeftSegments={ string.Join(", ", startNodeIncomingLeftSegments.Select(x => x.ToString()).ToArray())}");
					Log._Debug($"seg. {segmentId}. startNodeStraightSegments={ string.Join(", ", startNodeStraightSegments.Select(x => x.ToString()).ToArray())}");
					Log._Debug($"seg. {segmentId}. startNodeIncomingStraightSegments={ string.Join(", ", startNodeIncomingStraightSegments.Select(x => x.ToString()).ToArray())}");
					Log._Debug($"seg. {segmentId}. endNodeSegments={ string.Join(", ", endNodeSegments.Select(x => x.ToString()).ToArray())}");
					Log._Debug($"seg. {segmentId}. endNodeRightSegments={ string.Join(", ", endNodeRightSegments.Select(x => x.ToString()).ToArray())}");
					Log._Debug($"seg. {segmentId}. endNodeIncomingRightSegments={ string.Join(", ", endNodeIncomingRightSegments.Select(x => x.ToString()).ToArray())}");
					Log._Debug($"seg. {segmentId}. endNodeLeftSegments={ string.Join(", ", endNodeLeftSegments.Select(x => x.ToString()).ToArray())}");
					Log._Debug($"seg. {segmentId}. endNodeIncomingLeftSegments={ string.Join(", ", endNodeIncomingLeftSegments.Select(x => x.ToString()).ToArray())}");
					Log._Debug($"seg. {segmentId}. endNodeStraightSegments={ string.Join(", ", endNodeStraightSegments.Select(x => x.ToString()).ToArray())}");
					Log._Debug($"seg. {segmentId}. endNodeIncomingStraightSegments={ string.Join(", ", endNodeIncomingStraightSegments.Select(x => x.ToString()).ToArray())}");
				}
#endif
			} finally {
				lastRecalculation = Singleton<SimulationManager>.instance.m_currentFrameIndex >> 6;
				recalculating = false;
#if DEBUG
				if (output)
					Log.Info($"Lock released after recalculating geometries of segment {segmentId}");
#endif
				Monitor.Exit(Lock);
			}

			/*if (segmentId == 35053) {
				Log.Message($"Segment geometry for {segmentId}: snr: {startNodeRightSegments} sns: {startNodeHasStraightSegment} snl: {startNodeHasLeftSegments}");
				Log.Message($"Segment geometry for {segmentId}: enr: {endNodeRightSegments} ens: {endNodeHasStraightSegment} enl: {endNodeHasLeftSegments}");
			}*/
		}

		private void cleanup() {
			try {
				Monitor.Enter(Lock);

				startNodeSegments.Clear();
				startNodeRightSegments.Clear();
				startNodeIncomingRightSegments.Clear();
				startNodeLeftSegments.Clear();
				startNodeIncomingLeftSegments.Clear();
				startNodeStraightSegments.Clear();
				startNodeIncomingStraightSegments.Clear();

				endNodeSegments.Clear();
				endNodeRightSegments.Clear();
				endNodeIncomingRightSegments.Clear();
				endNodeLeftSegments.Clear();
				endNodeIncomingLeftSegments.Clear();
				endNodeStraightSegments.Clear();
				endNodeIncomingStraightSegments.Clear();

				for (int i = 0; i < 7; ++i) {
					startNodeIncomingRightSegmentsArray[i] = 0;
					startNodeIncomingLeftSegmentsArray[i] = 0;
					startNodeIncomingStraightSegmentsArray[i] = 0;
					endNodeIncomingRightSegmentsArray[i] = 0;
					endNodeIncomingLeftSegmentsArray[i] = 0;
					endNodeIncomingStraightSegmentsArray[i] = 0;
				}

				// reset highway lane arrows
				Flags.removeHighwayLaneArrowFlagsAtSegment(segmentId);
			} finally {
				Monitor.Exit(Lock);
			}
		}

		private void recalculate(ref bool outgoingOneWay, ref HashSet<ushort> nodeSegments,
			ref HashSet<ushort> right, ref HashSet<ushort> incomingRight, ref ushort[] incomingRightArray,
			ref HashSet<ushort> left, ref HashSet<ushort> incomingLeft, ref ushort[] incomingLeftArray,
			ref HashSet<ushort> straight, ref HashSet<ushort> incomingStraight, ref ushort[] incomingStraightArray,
			ushort nodeId) {

			if (nodeId == 0)
				return;

			NetManager netManager = Singleton<NetManager>.instance;

			ItemClass connectionClass = netManager.m_segments.m_buffer[segmentId].Info.GetConnectionClass();

			int incomingRightIndex = 0;
			int incomingLeftIndex = 0;
			int incomingStraightIndex = 0;
			for (var s = 0; s < 8; s++) {
				var otherSegmentId = netManager.m_nodes.m_buffer[nodeId].GetSegment(s);
				if (otherSegmentId == 0 || otherSegmentId == segmentId)
					continue;
				/*ItemClass otherConnectionClass = Singleton<NetManager>.instance.m_segments.m_buffer[otherSegmentId].Info.GetConnectionClass();
				if (otherConnectionClass.m_service != connectionClass.m_service)
					continue;*/

				// determine geometry
				outgoingOneWay = calculateIsOutgoingOneWay(segmentId, nodeId);

				if (TrafficPriority.IsRightSegment(segmentId, otherSegmentId, nodeId)) {
					right.Add(otherSegmentId);
					if (!calculateIsOutgoingOneWay(otherSegmentId, nodeId)) {
						incomingRight.Add(otherSegmentId);
						if (incomingRightIndex < 7)
							incomingRightArray[incomingRightIndex++] = otherSegmentId;
					}
				} else if (TrafficPriority.IsLeftSegment(segmentId, otherSegmentId, nodeId)) {
					left.Add(otherSegmentId);
					if (!calculateIsOutgoingOneWay(otherSegmentId, nodeId)) {
						incomingLeft.Add(otherSegmentId);
						if (incomingLeftIndex < 7)
							incomingLeftArray[incomingLeftIndex++] = otherSegmentId;
					}
				} else {
					straight.Add(otherSegmentId);
					if (!calculateIsOutgoingOneWay(otherSegmentId, nodeId)) {
						incomingStraight.Add(otherSegmentId);
						if (incomingStraightIndex < 7)
							incomingStraightArray[incomingStraightIndex++] = otherSegmentId;
					}
				}

				// reset highway lane arrows
				Flags.removeHighwayLaneArrowFlagsAtSegment(otherSegmentId);

				nodeSegments.Add(otherSegmentId);
			}
		}

		internal bool VerifyConnectedSegment(ushort otherSegmentId) {
			if ((Singleton<NetManager>.instance.m_segments.m_buffer[otherSegmentId].m_flags & NetSegment.Flags.Created) == NetSegment.Flags.None) {
				return false;
			}
			if (otherSegmentId == segmentId)
				return false;

			bool segmentIsConnectedToStartNode = false;
			bool segmentIsConnectedToEndNode = false;
			try {
				Monitor.Enter(Lock);
				segmentIsConnectedToStartNode = startNodeSegments.Contains(otherSegmentId);
				segmentIsConnectedToEndNode = endNodeSegments.Contains(otherSegmentId);
			} finally {
				Monitor.Exit(Lock);
			}

			if (!segmentIsConnectedToStartNode && !segmentIsConnectedToEndNode) {
				Log._Debug($"Neither the segments of start node {startNodeId()} nor of end node {endNodeId()} of segment {segmentId} contain the segment {otherSegmentId}");
                Recalculate();
				return true;
			}
			return false;
		}

		internal void VerifySegmentsByCount(ushort nodeId) {
			if ((Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId].m_flags & NetNode.Flags.Created) == NetNode.Flags.None) {
				return;
			}

			int expectedCount = Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId].CountSegments(NetSegment.Flags.Created, segmentId);
			var storedCount = CountOtherSegments(nodeId);
			if (storedCount != expectedCount) {
				Log._Debug($"The number of other segments (expected {expectedCount}) at node {nodeId} does not equals the stored count ({storedCount})");
				Recalculate();
			}
		}

		internal void LazyVerify() {
			if ((Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].m_flags & NetSegment.Flags.Created) == NetSegment.Flags.None) {
				cleanup();
				return;
			}

			if (lastRecalculation < (Singleton<SimulationManager>.instance.m_currentFrameIndex >> 8) - 5) {
				Recalculate(true, true);
				return;
			}

			uint lastUpdate = lastRecalculation;
			VerifySegmentsByCount(startNodeId());
			if (lastUpdate != lastRecalculation)
				return;
			VerifySegmentsByCount(endNodeId());
		}

		private int CountOtherSegments(ushort nodeId) {
			if ((Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId].m_flags & NetNode.Flags.Created) == NetNode.Flags.None) {
				return 0;
			}
			if (startNodeId() != nodeId && endNodeId() != nodeId) {
				Log.Info($"Node {nodeId} is neither start ({startNodeId()}) nor end node ({endNodeId()}) of segment {segmentId}!");
				Recalculate();
			}

			if (startNodeId() == nodeId)
				return startNodeSegments.Count;
			else if (endNodeId() == nodeId)
				return endNodeSegments.Count;
			else {
				Log.Info($"CountOtherSegments: Node {nodeId} is neither start nor end node of segment {segmentId}.");
				return 0;
			}
		}

		public int CountRightSegments(ushort nodeId) {
			if ((Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId].m_flags & NetNode.Flags.Created) == NetNode.Flags.None) {
				return 0;
			}
			if (startNodeId() != nodeId && endNodeId() != nodeId) {
				Log.Info($"Node {nodeId} is neither start ({startNodeId()}) nor end node ({endNodeId()}) of segment {segmentId}!");
				Recalculate();
			}

			if (startNodeId() == nodeId)
				return startNodeRightSegments.Count;
			else if (endNodeId() == nodeId)
				return endNodeRightSegments.Count;
			else {
				Log.Info($"CountRightSegments: Node {nodeId} is neither start nor end node of segment {segmentId}.");
				return 0;
			}
		}

		public int CountLeftSegments(ushort nodeId) {
			if ((Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId].m_flags & NetNode.Flags.Created) == NetNode.Flags.None) {
				return 0;
			}
			if (startNodeId() != nodeId && endNodeId() != nodeId) {
				Log.Info($"Node {nodeId} is neither start ({startNodeId()}) nor end node ({endNodeId()}) of segment {segmentId}!");
				Recalculate();
			}

			if (startNodeId() == nodeId)
				return startNodeLeftSegments.Count;
			else if (endNodeId() == nodeId)
				return endNodeLeftSegments.Count;
			else {
				Log.Info($"CountLeftSegments: Node {nodeId} is neither start nor end node of segment {segmentId}.");
				return 0;
			}
		}

		public int CountStraightSegments(ushort nodeId) {
			if ((Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId].m_flags & NetNode.Flags.Created) == NetNode.Flags.None) {
				return 0;
			}
			if (startNodeId() != nodeId && endNodeId() != nodeId) {
				Log.Info($"Node {nodeId} is neither start ({startNodeId()}) nor end node ({endNodeId()}) of segment {segmentId}!");
				Recalculate();
			}

			if (startNodeId() == nodeId)
				return startNodeStraightSegments.Count;
			else if (endNodeId() == nodeId)
				return endNodeStraightSegments.Count;
			else {
				Log.Info($"CountStraightSegments: Node {nodeId} is neither start nor end node of segment {segmentId}.");
				return 0;
			}
		}

		public int CountIncomingRightSegments(ushort nodeId) {
			if ((Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId].m_flags & NetNode.Flags.Created) == NetNode.Flags.None) {
				return 0;
			}
			if (startNodeId() != nodeId && endNodeId() != nodeId) {
				Log.Info($"Node {nodeId} is neither start ({startNodeId()}) nor end node ({endNodeId()}) of segment {segmentId}!");
				Recalculate();
			}

			if (startNodeId() == nodeId)
				return startNodeIncomingRightSegments.Count;
			else if (endNodeId() == nodeId)
				return endNodeIncomingRightSegments.Count;
			else {
				Log.Info($"CountIncomingRightSegments: Node {nodeId} is neither start nor end node of segment {segmentId}.");
				return 0;
			}
		}

		public int CountIncomingLeftSegments(ushort nodeId) {
			if ((Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId].m_flags & NetNode.Flags.Created) == NetNode.Flags.None) {
				return 0;
			}
			if (startNodeId() != nodeId && endNodeId() != nodeId) {
				Log.Info($"Node {nodeId} is neither start ({startNodeId()}) nor end node ({endNodeId()}) of segment {segmentId}!");
				Recalculate();
			}

			if (startNodeId() == nodeId)
				return startNodeIncomingLeftSegments.Count;
			else if (endNodeId() == nodeId)
				return endNodeIncomingLeftSegments.Count;
			else {
				Log.Info($"CountIncomingLeftSegments: Node {nodeId} is neither start nor end node of segment {segmentId}.");
				return 0;
			}
		}

		public int CountIncomingStraightSegments(ushort nodeId) {
			if ((Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId].m_flags & NetNode.Flags.Created) == NetNode.Flags.None) {
				return 0;
			}
			if (startNodeId() != nodeId && endNodeId() != nodeId) {
				Log.Info($"Node {nodeId} is neither start ({startNodeId()}) nor end node ({endNodeId()}) of segment {segmentId}!");
				Recalculate();
			}

			if (startNodeId() == nodeId)
				return startNodeIncomingStraightSegments.Count;
			else if (endNodeId() == nodeId)
				return endNodeIncomingStraightSegments.Count;
			else {
				Log.Info($"CountIncomingStraightSegments: Node {nodeId} is neither start nor end node of segment {segmentId}.");
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

			if (startNodeId() != nodeId && endNodeId() != nodeId) {
				Log.Info($"Node {nodeId} is neither start ({startNodeId()}) nor end node ({endNodeId()}) of segment {segmentId}!");
				Recalculate();
			} else
				VerifyConnectedSegment(toSegmentId);

			if (startNodeId() == nodeId)
				return startNodeLeftSegments.Contains(toSegmentId);
			else if (endNodeId() == nodeId)
				return endNodeLeftSegments.Contains(toSegmentId);
			else {
				//if (segmentId == debugSegId)
					Log.Info($"IsLeftSegment: Node {nodeId} (segment {toSegmentId}) is neither start nor end node of segment {segmentId}.");
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

			if (startNodeId() != nodeId && endNodeId() != nodeId) {
				Log.Info($"Node {nodeId} is neither start ({startNodeId()}) nor end node ({endNodeId()}) of segment {segmentId}!");
				Recalculate();
			} else
				VerifyConnectedSegment(toSegmentId);

			if (startNodeId() == nodeId)
				return startNodeRightSegments.Contains(toSegmentId);
			else if (endNodeId() == nodeId)
				return endNodeRightSegments.Contains(toSegmentId);
			else {
				//if (segmentId == debugSegId)
					Log.Info($"IsRightSegment: Node {nodeId} (segment {toSegmentId}) is neither start nor end node of segment {segmentId}.");
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

			if (startNodeId() != nodeId && endNodeId() != nodeId) {
				Log.Info($"Node {nodeId} is neither start ({startNodeId()}) nor end node ({endNodeId()}) of segment {segmentId}!");
				Recalculate();
			} else
				VerifyConnectedSegment(toSegmentId);

			if (startNodeId() == nodeId)
				return startNodeStraightSegments.Contains(toSegmentId);
			else if (endNodeId() == nodeId)
				return endNodeStraightSegments.Contains(toSegmentId);
			else {
				//if (segmentId == debugSegId)
					Log.Info($"IsStraightSegment: Node {nodeId} (segment {toSegmentId}) is neither start nor end node of segment {segmentId}.");
				return false;
			}
		}

		public bool IsOneWay() {
			return oneWay;
		}

		public bool IsOutgoingOneWay(ushort nodeId) {
			if ((Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId].m_flags & NetNode.Flags.Created) == NetNode.Flags.None) {
				return false;
			}

			if (startNodeId() == nodeId)
				return outgoingOneWayAtStartNode;
			else if (endNodeId() == nodeId)
				return outgoingOneWayAtEndNode;
			else {
				Log.Info($"IsOutgoingOneWay: Node {nodeId} is neither start nor end node of segment {segmentId}.");
				return false;
			}
		}

		public bool IsIncomingOneWay(ushort nodeId) {
			return (IsOneWay() && !IsOutgoingOneWay(nodeId));
		}

		private bool calculateIsOutgoingOneWay(ushort segmentId, ushort nodeId) {
			var instance = Singleton<NetManager>.instance;

			var segment = instance.m_segments.m_buffer[segmentId];
			var info = segment.Info;

			var dir = NetInfo.Direction.Forward;
			if (segment.m_startNode == nodeId)
				dir = NetInfo.Direction.Backward;
			var dir2 = ((segment.m_flags & NetSegment.Flags.Invert) == NetSegment.Flags.None) ? dir : NetInfo.InvertDirection(dir);
			var dir3 = TrafficPriority.LeftHandDrive ? NetInfo.InvertDirection(dir2) : dir2;

			var laneId = segment.m_lanes;
			var laneIndex = 0;
			while (laneIndex < info.m_lanes.Length && laneId != 0u) {
				if (info.m_lanes[laneIndex].m_laneType != NetInfo.LaneType.Pedestrian &&
					(info.m_lanes[laneIndex].m_direction == dir3)) {
					return false;
				}

				laneId = instance.m_lanes.m_buffer[laneId].m_nextLane;
				laneIndex++;
			}

			return true;
		}

		private bool calculateIsOneWay(ushort segmentId) {
			var instance = Singleton<NetManager>.instance;

			var segment = instance.m_segments.m_buffer[segmentId];
			var info = segment.Info;

			var hasForward = false;
			var hasBackward = false;

			var laneId = segment.m_lanes;
			var laneIndex = 0;
			while (laneIndex < info.m_lanes.Length && laneId != 0u) {
				if (info.m_lanes[laneIndex].m_laneType != NetInfo.LaneType.Pedestrian &&
					(info.m_lanes[laneIndex].m_direction == NetInfo.Direction.Forward)) {
					hasForward = true;
				}

				if (info.m_lanes[laneIndex].m_laneType != NetInfo.LaneType.Pedestrian &&
					(info.m_lanes[laneIndex].m_direction == NetInfo.Direction.Backward)) {
					hasBackward = true;
				}

				if (hasForward && hasBackward) {
					return false;
				}

				laneId = instance.m_lanes.m_buffer[(int)((UIntPtr)laneId)].m_nextLane;
				laneIndex++;
			}

			return true;
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

			if (startNodeId() != nodeId && endNodeId() != nodeId) {
				Log.Info($"Node {nodeId} is neither start ({startNodeId()}) nor end node ({endNodeId()}) of segment {segmentId}!");
				Recalculate();
			} else
				VerifyConnectedSegment(toSegmentId);

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
