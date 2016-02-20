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

namespace TrafficManager.Traffic {
	/// <summary>
	/// Manages segment geometry data (e.g. if a segment is one-way or not, which incoming/outgoing segments are connected at the start or end node) of one specific segment.
	/// Directional data (left, right, straight) is always given relatively to the managed segment.
	/// The terms "incoming"/"outgoing" refer to vehicles being able to move to/from the managed segment: Vehicles may to go to the managed segment if the other segment
	/// is "incoming". Vehicles may go to the other segment if it is "outgoing".
	/// 
	/// Segment geometry data is primarily updated by the path-finding master thread (see method CustomPathFind.ProcessItemMain and field CustomPathFind.IsMasterPathFind).
	/// However, other methods may manually update geometry data by calling the "Recalculate" method. This is especially necessary for segments that are not visited by the
	/// path-finding algorithm (apparently if a segment is not used by any vehicle)
	/// 
	/// Warning: Accessing/Iterating/Checking for element existence on the HashSets requires acquiring a lock on the "Lock" object beforehand. The path-finding does not use
	/// the HashSets at all (did not want to have locking in the path-finding). Instead, it iterates over the provided primitive arrays.
	/// </summary>
	public class SegmentGeometry {
		/// <summary>
		/// The id of the managed segment
		/// </summary>
		private ushort segmentId;
		
		/// <summary>
		/// Connected segments at start node
		/// </summary>
		private HashSet<ushort> startNodeSegments = new HashSet<ushort>();

		/// <summary>
		/// Connected segments at end node
		/// </summary>
		private HashSet<ushort> endNodeSegments = new HashSet<ushort>();

		private HashSet<ushort> startNodeLeftSegments = new HashSet<ushort>();
		private HashSet<ushort> startNodeIncomingLeftSegments = new HashSet<ushort>();
		private HashSet<ushort> startNodeOutgoingLeftSegments = new HashSet<ushort>();

		private HashSet<ushort> startNodeRightSegments = new HashSet<ushort>();
		private HashSet<ushort> startNodeIncomingRightSegments = new HashSet<ushort>();
		private HashSet<ushort> startNodeOutgoingRightSegments = new HashSet<ushort>();

		private HashSet<ushort> startNodeStraightSegments = new HashSet<ushort>();
		private HashSet<ushort> startNodeIncomingStraightSegments = new HashSet<ushort>();
		private HashSet<ushort> startNodeOutgoingStraightSegments = new HashSet<ushort>();

		private HashSet<ushort> endNodeLeftSegments = new HashSet<ushort>();
		private HashSet<ushort> endNodeIncomingLeftSegments = new HashSet<ushort>();
		private HashSet<ushort> endNodeOutgoingLeftSegments = new HashSet<ushort>();

		private HashSet<ushort> endNodeRightSegments = new HashSet<ushort>();
		private HashSet<ushort> endNodeIncomingRightSegments = new HashSet<ushort>();
		private HashSet<ushort> endNodeOutgoingRightSegments = new HashSet<ushort>();

		private HashSet<ushort> endNodeStraightSegments = new HashSet<ushort>();
		private HashSet<ushort> endNodeIncomingStraightSegments = new HashSet<ushort>();
		private HashSet<ushort> endNodeOutgoingStraightSegments = new HashSet<ushort>();

		private ushort[] startNodeIncomingLeftSegmentsArray = new ushort[7];
		private ushort[] startNodeIncomingRightSegmentsArray = new ushort[7];
		private ushort[] startNodeIncomingStraightSegmentsArray = new ushort[7];

		private ushort[] endNodeIncomingLeftSegmentsArray = new ushort[7];
		private ushort[] endNodeIncomingRightSegmentsArray = new ushort[7];
		private ushort[] endNodeIncomingStraightSegmentsArray = new ushort[7];

		/// <summary>
		/// Indicates that the managed segment is a one-way segment
		/// </summary>
		private bool oneWay = false;

		/// <summary>
		/// Indicates that the managed segment is an outgoing one-way segment at start node. That means vehicles may move to the start node.
		/// </summary>
		private bool outgoingOneWayAtStartNode = false;

		/// <summary>
		/// Indicates that the managed segment is an outgoing one-way segment at end node. That means vehicles may move to the end node.
		/// </summary>
		private bool outgoingOneWayAtEndNode = false;

		/// <summary>
		/// Connected left segments at start node
		/// </summary>
		public HashSet<ushort> StartNodeLeftSegments {
			get { return startNodeLeftSegments; }
			private set { startNodeLeftSegments = value; }
		}

		/// <summary>
		/// Connected incoming left segments at start node
		/// </summary>
		public HashSet<ushort> StartNodeIncomingLeftSegments {
			get { return startNodeIncomingLeftSegments; }
			private set { startNodeIncomingLeftSegments = value; }
		}

		/// <summary>
		/// Connected right segments at start node
		/// </summary>
		public HashSet<ushort> StartNodeRightSegments {
			get { return startNodeRightSegments; }
			private set { startNodeRightSegments = value; }
		}

		/// <summary>
		/// Connected incoming right segments at start node
		/// </summary>
		public HashSet<ushort> StartNodeIncomingRightSegments {
			get { return startNodeIncomingRightSegments; }
			private set { startNodeIncomingRightSegments = value; }
		}

		/// <summary>
		/// Connected straight segments at start node
		/// </summary>
		public HashSet<ushort> StartNodeStraightSegments {
			get { return startNodeStraightSegments; }
			private set { startNodeStraightSegments = value; }
		}

		/// <summary>
		/// Connected incoming straight segments at start node
		/// </summary>
		public HashSet<ushort> StartNodeIncomingStraightSegments {
			get { return startNodeIncomingStraightSegments; }
			private set { startNodeIncomingStraightSegments = value; }
		}

		/// <summary>
		/// Connected left segments at end node
		/// </summary>
		public HashSet<ushort> EndNodeLeftSegments {
			get { return endNodeLeftSegments; }
			private set { endNodeLeftSegments = value; }
		}

		/// <summary>
		/// Connected incoming left segments at end node
		/// </summary>
		public HashSet<ushort> EndNodeIncomingLeftSegments {
			get { return endNodeIncomingLeftSegments; }
			private set { endNodeIncomingLeftSegments = value; }
		}

		/// <summary>
		/// Connected right segmetns at end node
		/// </summary>
		public HashSet<ushort> EndNodeRightSegments {
			get { return endNodeRightSegments; }
			private set { endNodeRightSegments = value; }
		}

		/// <summary>
		/// Connected incoming right segments at end node
		/// </summary>
		public HashSet<ushort> EndNodeIncomingRightSegments {
			get { return endNodeIncomingRightSegments; }
			private set { endNodeIncomingRightSegments = value; }
		}

		/// <summary>
		/// Connected straight segments at end node
		/// </summary>
		public HashSet<ushort> EndNodeStraightSegments {
			get { return endNodeStraightSegments; }
			private set { endNodeStraightSegments = value; }
		}

		/// <summary>
		/// Connected incoming straight segments at end node
		/// </summary>
		public HashSet<ushort> EndNodeIncomingStraightSegments {
			get { return endNodeIncomingStraightSegments; }
			private set { endNodeIncomingStraightSegments = value; }
		}

		/// <summary>
		/// Connected incoming left segments at start node
		/// </summary>
		public ushort[] StartNodeIncomingLeftSegmentsArray {
			get { return startNodeIncomingLeftSegmentsArray; }
			private set { startNodeIncomingLeftSegmentsArray = value; }
		}

		/// <summary>
		/// Connected incoming right segments at start node
		/// </summary>
		public ushort[] StartNodeIncomingRightSegmentsArray {
			get { return startNodeIncomingRightSegmentsArray; }
			private set { startNodeIncomingRightSegmentsArray = value; }
		}

		/// <summary>
		/// Connected incoming straight segments at start node
		/// </summary>
		public ushort[] StartNodeIncomingStraightSegmentsArray {
			get { return startNodeIncomingStraightSegmentsArray; }
			private set { startNodeIncomingStraightSegmentsArray = value; }
		}

		/// <summary>
		/// Connected incoming left segments at end node
		/// </summary>
		public ushort[] EndNodeIncomingLeftSegmentsArray {
			get { return endNodeIncomingLeftSegmentsArray; }
			private set { endNodeIncomingLeftSegmentsArray = value; }
		}

		/// <summary>
		/// Connected incoming right segments at end node
		/// </summary>
		public ushort[] EndNodeIncomingRightSegmentsArray {
			get { return endNodeIncomingRightSegmentsArray; }
			private set { endNodeIncomingRightSegmentsArray = value; }
		}

		/// <summary>
		/// Connected incoming straight segmetns at end node
		/// </summary>
		public ushort[] EndNodeIncomingStraightSegmentsArray {
			get { return endNodeIncomingStraightSegmentsArray; }
			private set { endNodeIncomingStraightSegmentsArray = value; }
		}


		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="segmentId">id of the managed segment</param>
		public SegmentGeometry(ushort segmentId) {
			this.segmentId = segmentId;
			Recalculate(false, true);
		}

		/// <summary>
		/// Determines the start node of the managed segment
		/// </summary>
		/// <returns></returns>
		public ushort startNodeId() {
			return Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].m_startNode;
		}

		/// <summary>
		/// Determines the end node of the managed segment
		/// </summary>
		/// <returns></returns>
		public ushort endNodeId() {
			return Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].m_endNode;
		}

		/// <summary>
		/// Lock object. Acquire this before accessing the HashSets.
		/// </summary>
		public readonly object Lock = new object();

		/// <summary>
		/// Indicates if a recalculation is currently in progress.
		/// </summary>
		private bool recalculating = false;

		/// <summary>
		/// Last simulation frame (>> 6) at which the segment geometry was calculated
		/// </summary>
		private uint lastRecalculation = 0;

		/// <summary>
		/// Requests recalculation of the managed segment's geometry data. If recalculation is not enforced (argument "force"),
		/// recalculation is only done if recalculation has not been recently executed.
		/// </summary>
		/// <param name="output">Specifies if logging should be performed</param>
		/// <param name="force">Specifies if recalculation should be enforced.</param>
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
					Log._Debug($"Trying to get a lock for Recalculating geometries of segment {segmentId}...");
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
				recalculate(ref outgoingOneWayAtStartNode, ref startNodeSegments, ref startNodeRightSegments, ref startNodeIncomingRightSegments, ref startNodeOutgoingRightSegments, ref startNodeIncomingRightSegmentsArray, ref startNodeLeftSegments, ref startNodeIncomingLeftSegments, ref startNodeOutgoingLeftSegments, ref startNodeIncomingLeftSegmentsArray, ref startNodeStraightSegments, ref startNodeIncomingStraightSegments, ref startNodeOutgoingStraightSegments, ref startNodeIncomingStraightSegmentsArray, startNodeId());
				recalculate(ref outgoingOneWayAtEndNode, ref endNodeSegments, ref endNodeRightSegments, ref endNodeIncomingRightSegments, ref endNodeOutgoingRightSegments, ref endNodeIncomingRightSegmentsArray, ref endNodeLeftSegments, ref endNodeIncomingLeftSegments, ref endNodeOutgoingLeftSegments, ref endNodeIncomingLeftSegmentsArray, ref endNodeStraightSegments, ref endNodeIncomingStraightSegments, ref endNodeOutgoingStraightSegments, ref endNodeIncomingStraightSegmentsArray, endNodeId());

#if DEBUG
				if (output) {
					Log.Info($"Recalculating geometries of segment {segmentId} FINISHED");
					Log._Debug($"seg. {segmentId}. outgoingOneWayAtStartNode={outgoingOneWayAtStartNode}");
					Log._Debug($"seg. {segmentId}. oneWay={oneWay}");
					Log._Debug($"seg. {segmentId}. startNodeSegments={ string.Join(", ", startNodeSegments.Select(x => x.ToString()).ToArray())}");
					Log._Debug($"seg. {segmentId}. startNodeRightSegments={ string.Join(", ", startNodeRightSegments.Select(x => x.ToString()).ToArray())}");
					Log._Debug($"seg. {segmentId}. startNodeIncomingRightSegments={ string.Join(", ", startNodeIncomingRightSegments.Select(x => x.ToString()).ToArray())}");
					Log._Debug($"seg. {segmentId}. startNodeOutgoingRightSegments={ string.Join(", ", startNodeOutgoingRightSegments.Select(x => x.ToString()).ToArray())}");

					Log._Debug($"seg. {segmentId}. startNodeLeftSegments={ string.Join(", ", startNodeLeftSegments.Select(x => x.ToString()).ToArray())}");
					Log._Debug($"seg. {segmentId}. startNodeIncomingLeftSegments={ string.Join(", ", startNodeIncomingLeftSegments.Select(x => x.ToString()).ToArray())}");
					Log._Debug($"seg. {segmentId}. startNodeOutgoingLeftSegments={ string.Join(", ", startNodeOutgoingLeftSegments.Select(x => x.ToString()).ToArray())}");

					Log._Debug($"seg. {segmentId}. startNodeStraightSegments={ string.Join(", ", startNodeStraightSegments.Select(x => x.ToString()).ToArray())}");
					Log._Debug($"seg. {segmentId}. startNodeIncomingStraightSegments={ string.Join(", ", startNodeIncomingStraightSegments.Select(x => x.ToString()).ToArray())}");
					Log._Debug($"seg. {segmentId}. startNodeOutgoingStraightSegments={ string.Join(", ", startNodeOutgoingStraightSegments.Select(x => x.ToString()).ToArray())}");

					Log._Debug($"seg. {segmentId}. endNodeSegments={ string.Join(", ", endNodeSegments.Select(x => x.ToString()).ToArray())}");
					Log._Debug($"seg. {segmentId}. endNodeRightSegments={ string.Join(", ", endNodeRightSegments.Select(x => x.ToString()).ToArray())}");
					Log._Debug($"seg. {segmentId}. endNodeIncomingRightSegments={ string.Join(", ", endNodeIncomingRightSegments.Select(x => x.ToString()).ToArray())}");
					Log._Debug($"seg. {segmentId}. endNodeOutgoingRightSegments={ string.Join(", ", endNodeOutgoingRightSegments.Select(x => x.ToString()).ToArray())}");

					Log._Debug($"seg. {segmentId}. endNodeLeftSegments={ string.Join(", ", endNodeLeftSegments.Select(x => x.ToString()).ToArray())}");
					Log._Debug($"seg. {segmentId}. endNodeIncomingLeftSegments={ string.Join(", ", endNodeIncomingLeftSegments.Select(x => x.ToString()).ToArray())}");
					Log._Debug($"seg. {segmentId}. endNodeOutgoingLeftSegments={ string.Join(", ", endNodeOutgoingLeftSegments.Select(x => x.ToString()).ToArray())}");

					Log._Debug($"seg. {segmentId}. endNodeStraightSegments={ string.Join(", ", endNodeStraightSegments.Select(x => x.ToString()).ToArray())}");
					Log._Debug($"seg. {segmentId}. endNodeIncomingStraightSegments={ string.Join(", ", endNodeIncomingStraightSegments.Select(x => x.ToString()).ToArray())}");
					Log._Debug($"seg. {segmentId}. endNodeOutgoingStraightSegments={ string.Join(", ", endNodeOutgoingStraightSegments.Select(x => x.ToString()).ToArray())}");
				}
#endif
			} finally {
				lastRecalculation = Singleton<SimulationManager>.instance.m_currentFrameIndex >> 6;
				recalculating = false;
#if DEBUG
				if (output)
					Log._Debug($"Lock released after recalculating geometries of segment {segmentId}");
#endif
				Monitor.Exit(Lock);
			}
		}

		/// <summary>
		/// Verifies the information that another is/is not connected to the managed segment. If the verification fails, a recalculation of geometry data is performed.
		/// After calling this method it is guaranteed that the segment geometry data regarding the queried segment with id "otherSegmentId" is correct.
		/// 
		/// This method should only be called if there is a good case to believe that the other segment may be connected to the managed segment.
		/// Else, a possibly unnecessary geometry recalculation is performed.
		/// </summary>
		/// <param name="otherSegmentId">The other segment that is could be connected to the managed segment.</param>
		/// <returns></returns>
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
				Log.Warning($"Neither the segments of start node {startNodeId()} nor of end node {endNodeId()} of segment {segmentId} contain the segment {otherSegmentId}");
                Recalculate();
				return true;
			}
			return false;
		}

		/// <summary>
		/// Runs a simple segment geometry verification that only checks if the stored number of connected segments at start and end node. 
		/// 
		/// If the numbers of connected segments at the given node mismatches, a geometry recalculation is performed.
		/// </summary>
		/// <param name="nodeId">Node at which segment counts should be checked</param>
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

		/// <summary>
		/// Determines the number of segments connected to the managed segment at the given node.
		/// 
		/// A segment geometry verification is not performed.
		/// </summary>
		/// <param name="nodeId">The node at which other segments shall be counted</param>
		/// <returns>number of connected segments at the given node</returns>
		private int CountOtherSegments(ushort nodeId) {
			if ((Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId].m_flags & NetNode.Flags.Created) == NetNode.Flags.None) {
				return 0;
			}
			/*if (startNodeId() != nodeId && endNodeId() != nodeId) {
				Log.Info($"Node {nodeId} is neither start ({startNodeId()}) nor end node ({endNodeId()}) of segment {segmentId}!");
				Recalculate();
			}*/

			if (startNodeId() == nodeId)
				return startNodeSegments.Count;
			else if (endNodeId() == nodeId)
				return endNodeSegments.Count;
			else {
				Log.Info($"CountOtherSegments: Node {nodeId} is neither start nor end node of segment {segmentId}.");
				return 0;
			}
		}

		/// <summary>
		/// Determines the number of right segments connected to the managed segment at the given node.
		/// 
		/// A segment geometry verification is not performed.
		/// </summary>
		/// <param name="nodeId">The node at which other segments shall be counted</param>
		/// <returns>number of connected right segments at the given node</returns>
		public int CountRightSegments(ushort nodeId) {
			if ((Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId].m_flags & NetNode.Flags.Created) == NetNode.Flags.None) {
				return 0;
			}
			/*if (startNodeId() != nodeId && endNodeId() != nodeId) {
				Log.Info($"Node {nodeId} is neither start ({startNodeId()}) nor end node ({endNodeId()}) of segment {segmentId}!");
				Recalculate();
			}*/

			if (startNodeId() == nodeId)
				return startNodeRightSegments.Count;
			else if (endNodeId() == nodeId)
				return endNodeRightSegments.Count;
			else {
				Log.Info($"CountRightSegments: Node {nodeId} is neither start nor end node of segment {segmentId}.");
				return 0;
			}
		}

		/// <summary>
		/// Determines the number of left segments connected to the managed segment at the given node.
		/// 
		/// A segment geometry verification is not performed.
		/// </summary>
		/// <param name="nodeId">The node at which other segments shall be counted</param>
		/// <returns>number of connected left segments at the given node</returns>
		public int CountLeftSegments(ushort nodeId) {
			if ((Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId].m_flags & NetNode.Flags.Created) == NetNode.Flags.None) {
				return 0;
			}
			/*if (startNodeId() != nodeId && endNodeId() != nodeId) {
				Log.Info($"Node {nodeId} is neither start ({startNodeId()}) nor end node ({endNodeId()}) of segment {segmentId}!");
				Recalculate();
			}*/

			if (startNodeId() == nodeId)
				return startNodeLeftSegments.Count;
			else if (endNodeId() == nodeId)
				return endNodeLeftSegments.Count;
			else {
				Log.Info($"CountLeftSegments: Node {nodeId} is neither start nor end node of segment {segmentId}.");
				return 0;
			}
		}

		/// <summary>
		/// Determines the number of straight segments connected to the managed segment at the given node.
		/// 
		/// A segment geometry verification is not performed.
		/// </summary>
		/// <param name="nodeId">The node at which other segments shall be counted</param>
		/// <returns>number of connected straight segments at the given node</returns>
		public int CountStraightSegments(ushort nodeId) {
			if ((Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId].m_flags & NetNode.Flags.Created) == NetNode.Flags.None) {
				return 0;
			}
			/*if (startNodeId() != nodeId && endNodeId() != nodeId) {
				Log.Info($"Node {nodeId} is neither start ({startNodeId()}) nor end node ({endNodeId()}) of segment {segmentId}!");
				Recalculate();
			}*/

			if (startNodeId() == nodeId)
				return startNodeStraightSegments.Count;
			else if (endNodeId() == nodeId)
				return endNodeStraightSegments.Count;
			else {
				Log.Info($"CountStraightSegments: Node {nodeId} is neither start nor end node of segment {segmentId}.");
				return 0;
			}
		}

		/// <summary>
		/// Determines the number of incoming right segments connected to the managed segment at the given node.
		/// 
		/// A segment geometry verification is not performed.
		/// </summary>
		/// <param name="nodeId">The node at which other segments shall be counted</param>
		/// <returns>number of connected incoming right segments at the given node</returns>
		public int CountIncomingRightSegments(ushort nodeId) {
			if ((Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId].m_flags & NetNode.Flags.Created) == NetNode.Flags.None) {
				return 0;
			}
			/*if (startNodeId() != nodeId && endNodeId() != nodeId) {
				Log.Info($"Node {nodeId} is neither start ({startNodeId()}) nor end node ({endNodeId()}) of segment {segmentId}!");
				Recalculate();
			}*/

			if (startNodeId() == nodeId)
				return startNodeIncomingRightSegments.Count;
			else if (endNodeId() == nodeId)
				return endNodeIncomingRightSegments.Count;
			else {
				Log.Info($"CountIncomingRightSegments: Node {nodeId} is neither start nor end node of segment {segmentId}.");
				return 0;
			}
		}

		/// <summary>
		/// Determines the number of incoming left segments connected to the managed segment at the given node.
		/// 
		/// A segment geometry verification is not performed.
		/// </summary>
		/// <param name="nodeId">The node at which other segments shall be counted</param>
		/// <returns>number of connected incoming left segments at the given node</returns>
		public int CountIncomingLeftSegments(ushort nodeId) {
			if ((Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId].m_flags & NetNode.Flags.Created) == NetNode.Flags.None) {
				return 0;
			}
			/*if (startNodeId() != nodeId && endNodeId() != nodeId) {
				Log.Info($"Node {nodeId} is neither start ({startNodeId()}) nor end node ({endNodeId()}) of segment {segmentId}!");
				Recalculate();
			}*/

			if (startNodeId() == nodeId)
				return startNodeIncomingLeftSegments.Count;
			else if (endNodeId() == nodeId)
				return endNodeIncomingLeftSegments.Count;
			else {
				Log.Info($"CountIncomingLeftSegments: Node {nodeId} is neither start nor end node of segment {segmentId}.");
				return 0;
			}
		}

		/// <summary>
		/// Determines the number of incoming straight segments connected to the managed segment at the given node.
		/// 
		/// A segment geometry verification is not performed.
		/// </summary>
		/// <param name="nodeId">The node at which other segments shall be counted</param>
		/// <returns>number of connected incoming straight segments at the given node</returns>
		public int CountIncomingStraightSegments(ushort nodeId) {
			if ((Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId].m_flags & NetNode.Flags.Created) == NetNode.Flags.None) {
				return 0;
			}

			/*if (startNodeId() != nodeId && endNodeId() != nodeId) {
				Log.Info($"Node {nodeId} is neither start ({startNodeId()}) nor end node ({endNodeId()}) of segment {segmentId}!");
				Recalculate();
			}*/

			if (startNodeId() == nodeId)
				return startNodeIncomingStraightSegments.Count;
			else if (endNodeId() == nodeId)
				return endNodeIncomingStraightSegments.Count;
			else {
				Log.Info($"CountIncomingStraightSegments: Node {nodeId} is neither start nor end node of segment {segmentId}.");
				return 0;
			}
		}

		/// <summary>
		/// Determines the number of outgoing right segments connected to the managed segment at the given node.
		/// 
		/// A segment geometry verification is not performed.
		/// </summary>
		/// <param name="nodeId">The node at which other segments shall be counted</param>
		/// <returns>number of connected outgoing right segments at the given node</returns>
		public int CountOutgoingRightSegments(ushort nodeId) {
			if ((Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId].m_flags & NetNode.Flags.Created) == NetNode.Flags.None) {
				return 0;
			}
			/*if (startNodeId() != nodeId && endNodeId() != nodeId) {
				Log.Info($"Node {nodeId} is neither start ({startNodeId()}) nor end node ({endNodeId()}) of segment {segmentId}!");
				Recalculate();
			}*/

			if (startNodeId() == nodeId)
				return startNodeOutgoingRightSegments.Count;
			else if (endNodeId() == nodeId)
				return endNodeOutgoingRightSegments.Count;
			else {
				Log.Info($"CountOutgoingRightSegments: Node {nodeId} is neither start nor end node of segment {segmentId}.");
				return 0;
			}
		}

		/// <summary>
		/// Determines the number of outgoing left segments connected to the managed segment at the given node.
		/// 
		/// A segment geometry verification is not performed.
		/// </summary>
		/// <param name="nodeId">The node at which other segments shall be counted</param>
		/// <returns>number of connected outgoing left segments at the given node</returns>
		public int CountOutgoingLeftSegments(ushort nodeId) {
			if ((Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId].m_flags & NetNode.Flags.Created) == NetNode.Flags.None) {
				return 0;
			}
			/*if (startNodeId() != nodeId && endNodeId() != nodeId) {
				Log.Info($"Node {nodeId} is neither start ({startNodeId()}) nor end node ({endNodeId()}) of segment {segmentId}!");
				Recalculate();
			}*/

			if (startNodeId() == nodeId)
				return startNodeOutgoingLeftSegments.Count;
			else if (endNodeId() == nodeId)
				return endNodeOutgoingLeftSegments.Count;
			else {
				Log.Info($"CountOutgoingLeftSegments: Node {nodeId} is neither start nor end node of segment {segmentId}.");
				return 0;
			}
		}

		/// <summary>
		/// Determines the number of outgoing straight segments connected to the managed segment at the given node.
		/// 
		/// A segment geometry verification is not performed.
		/// </summary>
		/// <param name="nodeId">The node at which other segments shall be counted</param>
		/// <returns>number of connected outgoing straight segments at the given node</returns>
		public int CountOutgoingStraightSegments(ushort nodeId) {
			if ((Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId].m_flags & NetNode.Flags.Created) == NetNode.Flags.None) {
				return 0;
			}

			/*if (startNodeId() != nodeId && endNodeId() != nodeId) {
				Log.Info($"Node {nodeId} is neither start ({startNodeId()}) nor end node ({endNodeId()}) of segment {segmentId}!");
				Recalculate();
			}*/

			if (startNodeId() == nodeId)
				return startNodeOutgoingStraightSegments.Count;
			else if (endNodeId() == nodeId)
				return endNodeOutgoingStraightSegments.Count;
			else {
				Log.Info($"CountOutgoingStraightSegments: Node {nodeId} is neither start nor end node of segment {segmentId}.");
				return 0;
			}
		}

		/// <summary>
		/// Determines if the managed segment is connected to left segments at the given node.
		/// 
		/// A segment geometry verification is not performed.
		/// </summary>
		/// <param name="nodeId">The node the other segments have to be connected to</param>
		/// <returns>true, if, according to the stored geometry data, the managed segment is connected to left segments at the given node, else false.</returns>
		public bool HasLeftSegment(ushort nodeId) {
			return CountLeftSegments(nodeId) > 0;
		}

		/// <summary>
		/// Determines if the managed segment is connected to right segments at the given node.
		/// 
		/// A segment geometry verification is not performed.
		/// </summary>
		/// <param name="nodeId">The node the other segments have to be connected to</param>
		/// <returns>true, if, according to the stored geometry data, the managed segment is connected to right segments at the given node, else false.</returns>
		public bool HasRightSegment(ushort nodeId) {
			return CountRightSegments(nodeId) > 0;
		}

		/// <summary>
		/// Determines if the managed segment is connected to straight segments at the given node.
		/// 
		/// A segment geometry verification is not performed.
		/// </summary>
		/// <param name="nodeId">The node the other segments have to be connected to</param>
		/// <returns>true, if, according to the stored geometry data, the managed segment is connected to straight segments at the given node, else false.</returns>
		public bool HasStraightSegment(ushort nodeId) {
			return CountStraightSegments(nodeId) > 0;
		}

		/// <summary>
		/// Determines if the managed segment is connected to incoming left segments at the given node.
		/// 
		/// A segment geometry verification is not performed.
		/// </summary>
		/// <param name="nodeId">The node the other segments have to be connected to</param>
		/// <returns>true, if, according to the stored geometry data, the managed segment is connected to incoming left segments at the given node, else false.</returns>
		public bool HasIncomingLeftSegment(ushort nodeId) {
			return CountIncomingLeftSegments(nodeId) > 0;
		}

		/// <summary>
		/// Determines if the managed segment is connected to incoming right segments at the given node.
		/// 
		/// A segment geometry verification is not performed.
		/// </summary>
		/// <param name="nodeId">The node the other segments have to be connected to</param>
		/// <returns>true, if, according to the stored geometry data, the managed segment is connected to incoming right segments at the given node, else false.</returns>
		public bool HasIncomingRightSegment(ushort nodeId) {
			return CountIncomingRightSegments(nodeId) > 0;
		}

		/// <summary>
		/// Determines if the managed segment is connected to incoming straight segments at the given node.
		/// 
		/// A segment geometry verification is not performed.
		/// </summary>
		/// <param name="nodeId">The node the other segments have to be connected to</param>
		/// <returns>true, if, according to the stored geometry data, the managed segment is connected to incoming straight segments at the given node, else false.</returns>
		public bool HasIncomingStraightSegment(ushort nodeId) {
			return CountIncomingStraightSegments(nodeId) > 0;
		}

		/// <summary>
		/// Determines if the managed segment is connected to outgoing left segments at the given node.
		/// 
		/// A segment geometry verification is not performed.
		/// </summary>
		/// <param name="nodeId">The node the other segments have to be connected to</param>
		/// <returns>true, if, according to the stored geometry data, the managed segment is connected to outgoing left segments at the given node, else false.</returns>
		public bool HasOutgoingLeftSegment(ushort nodeId) {
			return CountOutgoingLeftSegments(nodeId) > 0;
		}

		/// <summary>
		/// Determines if the managed segment is connected to outgoing right segments at the given node.
		/// 
		/// A segment geometry verification is not performed.
		/// </summary>
		/// <param name="nodeId">The node the other segments have to be connected to</param>
		/// <returns>true, if, according to the stored geometry data, the managed segment is connected to outgoing right segments at the given node, else false.</returns>
		public bool HasOutgoingRightSegment(ushort nodeId) {
			return CountOutgoingRightSegments(nodeId) > 0;
		}

		/// <summary>
		/// Determines if the managed segment is connected to outgoing straight segments at the given node.
		/// 
		/// A segment geometry verification is not performed.
		/// </summary>
		/// <param name="nodeId">The node the other segments have to be connected to</param>
		/// <returns>true, if, according to the stored geometry data, the managed segment is connected to outgoing straight segments at the given node, else false.</returns>
		public bool HasOutgoingStraightSegment(ushort nodeId) {
			return CountOutgoingStraightSegments(nodeId) > 0;
		}

		/// <summary>
		/// Determines if, according to the stored geometry data, the given segment is connected to the managed segment and is a left segment at the given node relatively to the managed segment.
		/// 
		/// A segment geometry verification is not performed.
		/// </summary>
		/// <param name="toSegmentId">other segment that ought to be left, relatively to the managed segment</param>
		/// <param name="nodeId">node which both the managed and the other segment ought to be connected to</param>
		/// <returns>true, if the other segment is, according to the stored geometry data, connected on the left-hand side of the managed segment at the given node</returns>
		public bool IsLeftSegment(ushort toSegmentId, ushort nodeId) {
			if ((Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId].m_flags & NetNode.Flags.Created) == NetNode.Flags.None) {
				return false;
			}
			if ((Singleton<NetManager>.instance.m_segments.m_buffer[toSegmentId].m_flags & NetSegment.Flags.Created) == NetSegment.Flags.None) {
				return false;
			}
			if (toSegmentId == segmentId)
				return false;

			/*if (startNodeId() != nodeId && endNodeId() != nodeId) {
				Log.Info($"Node {nodeId} is neither start ({startNodeId()}) nor end node ({endNodeId()}) of segment {segmentId}!");
				Recalculate();
			} else
				VerifyConnectedSegment(toSegmentId);*/

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

		/// <summary>
		/// Determines if, according to the stored geometry data, the given segment is connected to the managed segment and is a right segment at the given node relatively to the managed segment.
		/// 
		/// A segment geometry verification is not performed.
		/// </summary>
		/// <param name="toSegmentId">other segment that ought to be right, relatively to the managed segment</param>
		/// <param name="nodeId">node which both the managed and the other segment ought to be connected to</param>
		/// <returns>true, if the other segment is, according to the stored geometry data, connected on the right-hand side of the managed segment at the given node</returns>
		public bool IsRightSegment(ushort toSegmentId, ushort nodeId) {
			if ((Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId].m_flags & NetNode.Flags.Created) == NetNode.Flags.None) {
				return false;
			}
			if ((Singleton<NetManager>.instance.m_segments.m_buffer[toSegmentId].m_flags & NetSegment.Flags.Created) == NetSegment.Flags.None) {
				return false;
			}
			if (toSegmentId == segmentId)
				return false;

			/*if (startNodeId() != nodeId && endNodeId() != nodeId) {
				Log.Info($"Node {nodeId} is neither start ({startNodeId()}) nor end node ({endNodeId()}) of segment {segmentId}!");
				Recalculate();
			} else
				VerifyConnectedSegment(toSegmentId);*/

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

		/// <summary>
		/// Determines if, according to the stored geometry data, the given segment is connected to the managed segment and is a straight segment at the given node relatively to the managed segment.
		/// 
		/// A segment geometry verification is not performed.
		/// </summary>
		/// <param name="toSegmentId">other segment that ought to be straight, relatively to the managed segment</param>
		/// <param name="nodeId">node which both the managed and the other segment ought to be connected to</param>
		/// <returns>true, if the other segment is, according to the stored geometry data, connected straight-wise to the managed segment at the given node</returns>
		public bool IsStraightSegment(ushort toSegmentId, ushort nodeId) {
			if ((Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId].m_flags & NetNode.Flags.Created) == NetNode.Flags.None) {
				return false;
			}
			if ((Singleton<NetManager>.instance.m_segments.m_buffer[toSegmentId].m_flags & NetSegment.Flags.Created) == NetSegment.Flags.None) {
				return false;
			}
			if (toSegmentId == segmentId)
				return false;

			/*if (startNodeId() != nodeId && endNodeId() != nodeId) {
				Log.Info($"Node {nodeId} is neither start ({startNodeId()}) nor end node ({endNodeId()}) of segment {segmentId}!");
				Recalculate();
			} else
				VerifyConnectedSegment(toSegmentId);*/

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

		/// <summary>
		/// Determines if, according to the stored geometry data, the managed segment is a one-way road.
		/// 
		/// A segment geometry verification is not performed.
		/// </summary>
		/// <returns>true, if, according to the stored geometry data, the managed segment is a one-way road, false otherwise</returns>
		public bool IsOneWay() {
			return oneWay;
		}

		/// <summary>
		/// Determines if, according to the stored geometry data, the managed segment is an outgoing one-way road at the given node.
		/// 
		/// A segment geometry verification is not performed.
		/// </summary>
		/// <returns>true, if, according to the stored geometry data, the managed segment is an outgoing one-way road at the given node, false otherwise</returns>
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

		/// <summary>
		/// Determines if, according to the stored geometry data, the managed segment is an incoming one-way road at the given node.
		/// 
		/// A segment geometry verification is not performed.
		/// </summary>
		/// <returns>true, if, according to the stored geometry data, the managed segment is an incoming one-way road at the given node, false otherwise</returns>
		public bool IsIncomingOneWay(ushort nodeId) {
			return (IsOneWay() && !IsOutgoingOneWay(nodeId));
		}

		/// <summary>
		/// Determines the relative direction of the other segment relatively to the managed segment at the given node, according to the stored geometry information.
		/// 
		/// A segment geometry verification is not performed.
		/// </summary>
		/// <param name="otherSegmentId">other segment</param>
		/// <returns>relative direction of the other segment relatively to the managed segment at the given node</returns>
		public Direction GetDirection(ushort otherSegmentId, ushort nodeId) {
			if ((Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId].m_flags & NetNode.Flags.Created) == NetNode.Flags.None) {
				return Direction.Forward;
			}
			if ((Singleton<NetManager>.instance.m_segments.m_buffer[otherSegmentId].m_flags & NetSegment.Flags.Created) == NetSegment.Flags.None) {
				return Direction.Forward;
			}

			/*if (startNodeId() != nodeId && endNodeId() != nodeId) {
				Log.Info($"Node {nodeId} is neither start ({startNodeId()}) nor end node ({endNodeId()}) of segment {segmentId}!");
				Recalculate();
			} else
				VerifyConnectedSegment(otherSegmentId);*/

			if (otherSegmentId == segmentId)
				return Direction.Turn;
			else if (IsRightSegment(otherSegmentId, nodeId))
				return Direction.Right;
			else if (IsLeftSegment(otherSegmentId, nodeId))
				return Direction.Left;
			else {
				if (startNodeId() != nodeId && endNodeId() != nodeId) {
					Log.Info($"Node {nodeId} is neither start ({startNodeId()}) nor end node ({endNodeId()}) of segment {segmentId}!");
				}
				return Direction.Forward;
			}
		}

		/// <summary>
		/// Determines if highway merging/splitting rules are activated at the managed segment for the given node.
		/// 
		/// A segment geometry verification is not performed.
		/// </summary>
		/// <param name="startNode"></param>
		/// <returns></returns>
		public bool AreHighwayRulesEnabled(bool startNode) {
			if (!Options.highwayRules)
				return false;
			if (!IsIncomingOneWay(startNode ? startNodeId() : endNodeId()))
				return false;
			if (!(Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].Info.m_netAI is RoadBaseAI))
				return false;
			if (!((RoadBaseAI)Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].Info.m_netAI).m_highwayRules)
				return false;

			HashSet<ushort> otherSegmentIds = null;
			try {
				Monitor.Enter(Lock);
				otherSegmentIds = new HashSet<ushort>(startNode ? startNodeSegments : endNodeSegments);
			} finally {
				Monitor.Exit(Lock);
			}

			if (otherSegmentIds.Count <= 1)
				return false;

			bool nextAreOnlyOneWayHighways = true;
			foreach (ushort otherSegmentId in otherSegmentIds) {
				if (Singleton<NetManager>.instance.m_segments.m_buffer[otherSegmentId].Info.m_netAI is RoadBaseAI) {
					if (!CustomRoadAI.GetSegmentGeometry(otherSegmentId).IsOneWay() || !((RoadBaseAI)Singleton<NetManager>.instance.m_segments.m_buffer[otherSegmentId].Info.m_netAI).m_highwayRules) {
						nextAreOnlyOneWayHighways = false;
						break;
					}
				} else {
					nextAreOnlyOneWayHighways = false;
					break;
				}
			}

			return nextAreOnlyOneWayHighways;
		}

		/// <summary>
		/// Calculates if the given segment is an outgoing one-way road at the given node.
		/// </summary>
		/// <param name="segmentId">segment to check</param>
		/// <param name="nodeId">node the given segment shall be checked at</param>
		/// <returns>true, if the given segment is an outgoing one-way road at the given node, false otherwise</returns>
		private static bool calculateIsOutgoingOneWay(ushort segmentId, ushort nodeId) {
			var instance = Singleton<NetManager>.instance;

			var info = instance.m_segments.m_buffer[segmentId].Info;

			var dir = NetInfo.Direction.Forward;
			if (instance.m_segments.m_buffer[segmentId].m_startNode == nodeId)
				dir = NetInfo.Direction.Backward;
			var dir2 = ((instance.m_segments.m_buffer[segmentId].m_flags & NetSegment.Flags.Invert) == NetSegment.Flags.None) ? dir : NetInfo.InvertDirection(dir);
			var dir3 = TrafficPriority.IsLeftHandDrive() ? NetInfo.InvertDirection(dir2) : dir2;

			var laneId = instance.m_segments.m_buffer[segmentId].m_lanes;
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

		/// <summary>
		/// Calculates if the given segment is a one-way road.
		/// </summary>
		/// <returns>true, if the managed segment is a one-way road, false otherwise</returns>
		private static bool calculateIsOneWay(ushort segmentId) {
			var instance = Singleton<NetManager>.instance;

			var info = instance.m_segments.m_buffer[segmentId].Info;

			var hasForward = false;
			var hasBackward = false;

			var laneId = instance.m_segments.m_buffer[segmentId].m_lanes;
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
		/// Clears the segment geometry data.
		/// </summary>
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
				Flags.removeHighwayLaneArrowFlagsAtSegment(segmentId); // TODO refactor
			} finally {
				Monitor.Exit(Lock);
			}
		}

		/// <summary>
		/// Performs a segment geometry recalculation at the given node.
		/// </summary>
		/// <param name="outgoingOneWay"></param>
		/// <param name="nodeSegments"></param>
		/// <param name="right"></param>
		/// <param name="incomingRight"></param>
		/// <param name="outgoingRight"></param>
		/// <param name="incomingRightArray"></param>
		/// <param name="left"></param>
		/// <param name="incomingLeft"></param>
		/// <param name="outgoingLeft"></param>
		/// <param name="incomingLeftArray"></param>
		/// <param name="straight"></param>
		/// <param name="incomingStraight"></param>
		/// <param name="outgoingStraight"></param>
		/// <param name="incomingStraightArray"></param>
		/// <param name="nodeId">node at which the geometry recalculation shall be performed at</param>
		private void recalculate(ref bool outgoingOneWay, ref HashSet<ushort> nodeSegments,
			ref HashSet<ushort> right, ref HashSet<ushort> incomingRight, ref HashSet<ushort> outgoingRight, ref ushort[] incomingRightArray,
			ref HashSet<ushort> left, ref HashSet<ushort> incomingLeft, ref HashSet<ushort> outgoingLeft, ref ushort[] incomingLeftArray,
			ref HashSet<ushort> straight, ref HashSet<ushort> incomingStraight, ref HashSet<ushort> outgoingStraight, ref ushort[] incomingStraightArray,
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
						if (!calculateIsOneWay(otherSegmentId))
							outgoingRight.Add(otherSegmentId);
					} else {
						outgoingRight.Add(otherSegmentId);
					}
				} else if (TrafficPriority.IsLeftSegment(segmentId, otherSegmentId, nodeId)) {
					left.Add(otherSegmentId);
					if (!calculateIsOutgoingOneWay(otherSegmentId, nodeId)) {
						incomingLeft.Add(otherSegmentId);
						if (incomingLeftIndex < 7)
							incomingLeftArray[incomingLeftIndex++] = otherSegmentId;
						if (!calculateIsOneWay(otherSegmentId))
							outgoingLeft.Add(otherSegmentId);
					} else {
						outgoingLeft.Add(otherSegmentId);
					}
				} else {
					straight.Add(otherSegmentId);
					if (!calculateIsOutgoingOneWay(otherSegmentId, nodeId)) {
						incomingStraight.Add(otherSegmentId);
						if (incomingStraightIndex < 7)
							incomingStraightArray[incomingStraightIndex++] = otherSegmentId;
						if (!calculateIsOneWay(otherSegmentId))
							outgoingStraight.Add(otherSegmentId);
					} else {
						outgoingStraight.Add(otherSegmentId);
					}
				}

				// reset highway lane arrows
				Flags.removeHighwayLaneArrowFlagsAtSegment(otherSegmentId); // TODO refactor

				nodeSegments.Add(otherSegmentId);
			}
		}
	}
}
