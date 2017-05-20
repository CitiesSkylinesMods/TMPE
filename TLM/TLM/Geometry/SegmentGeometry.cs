using ColossalFramework;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using TrafficManager.Custom.AI;
using TrafficManager.State;
using TrafficManager.Geometry;
using TrafficManager.TrafficLight;
using TrafficManager.Util;
using UnityEngine;
using TrafficManager.Traffic;
using TrafficManager.Manager;
using System.Linq;
using CSUtil.Commons;

namespace TrafficManager.Geometry {
	/// <summary>
	/// Manages segment geometry data (e.g. if a segment is one-way or not, which incoming/outgoing segments are connected at the start or end node) of one specific segment.
	/// Directional data (left, right, straight) is always given relatively to the managed segment.
	/// The terms "incoming"/"outgoing" refer to vehicles being able to move to/from the managed segment: Vehicles may to go to the managed segment if the other segment
	/// is "incoming". Vehicles may go to the other segment if it is "outgoing".
	/// 
	/// Segment geometry data is primarily updated by the path-finding master thread (see method CustomPathFind.ProcessItemMain and field CustomPathFind.IsMasterPathFind).
	/// However, other methods may manually update geometry data by calling the "Recalculate" method. This is especially necessary for segments that are not visited by the
	/// path-finding algorithm (apparently if a segment is not used by any vehicle)
	/// </summary>
	public class SegmentGeometry : IObservable<SegmentGeometry>, IEquatable<SegmentGeometry> {
		private static SegmentGeometry[] segmentGeometries;

		public static void PrintDebugInfo() {
			string buf =
			"--------------------------\n" +
			"--- SEGMENT GEOMETRIES ---\n" +
			"--------------------------\n";
			foreach (SegmentGeometry segGeo in segmentGeometries) {
				if (segGeo.IsValid()) {
					buf += segGeo.ToString() + "\n" +
					"-------------------------\n";
				}
			}
			Log.Info(buf);
		}

		/*public LaneGeometry[] LaneGeometries {
			get; private set;
		} = null;*/

		/// <summary>
		/// The id of the managed segment
		/// </summary>
		public ushort SegmentId {
			get; private set;
		}

		private SegmentEndGeometry startNodeGeometry;
		private SegmentEndGeometry endNodeGeometry;

		public SegmentEndGeometry StartNodeGeometry {
			get {
				if (startNodeGeometry.IsValid())
					return startNodeGeometry;
				else
					return null;
			}
			private set { startNodeGeometry = value; }
		}

		public SegmentEndGeometry EndNodeGeometry {
			get {
				if (endNodeGeometry.IsValid())
					return endNodeGeometry;
				else
					return null;
			}
			private set { endNodeGeometry = value; }
		}

		/// <summary>
		/// Indicates that the managed segment is a one-way segment
		/// </summary>
		private bool oneWay = false;

		/// <summary>
		/// Indicates that the managed segment is a highway
		/// </summary>
		private bool highway = false;

		/// <summary>
		/// Indicates that the managed segment has a buslane
		/// </summary>
		private bool buslane = false;

		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="segmentId">id of the managed segment</param>
		public SegmentGeometry(ushort segmentId) {
			this.SegmentId = segmentId;
			startNodeGeometry = new SegmentEndGeometry(segmentId, true);
			endNodeGeometry = new SegmentEndGeometry(segmentId, false);
		}
		
		/// <summary>
		/// Determines the start node of the managed segment
		/// </summary>
		/// <returns></returns>
		public ushort StartNodeId() {
			return Singleton<NetManager>.instance.m_segments.m_buffer[SegmentId].m_startNode;
		}

		/// <summary>
		/// Determines the end node of the managed segment
		/// </summary>
		/// <returns></returns>
		public ushort EndNodeId() {
			return Singleton<NetManager>.instance.m_segments.m_buffer[SegmentId].m_endNode;
		}

		/// <summary>
		/// Lock object. Acquire this before accessing the HashSets.
		/// </summary>
		public readonly object Lock = new object();

		/// <summary>
		/// Holds a list of observers which are being notified as soon as the managed segment's geometry is updated (but not neccessarily modified)
		/// </summary>
		private List<IObserver<SegmentGeometry>> observers = new List<IObserver<SegmentGeometry>>();

		private bool valid = false;

		public override string ToString() {
			return $"[SegmentGeometry ({SegmentId})\n" +
				"\t" + $"IsValid() = {IsValid()}\n" +
				"\t" + $"oneWay = {oneWay}\n" +
				"\t" + $"highway = {highway}\n" +
				"\t" + $"buslane = {buslane}\n" +
				"\t" + $"StartNodeGeometry = {StartNodeGeometry}\n" +
				"\t" + $"EndNodeGeometry = {EndNodeGeometry}\n" +
				//"\t" + $"LaneGeometries = {(LaneGeometries == null ? "<null>" : LaneGeometries.ArrayToString())}\n" +
				"SegmentGeometry]";
		}

		/// <summary>
		/// Registers an observer.
		/// </summary>
		/// <param name="observer"></param>
		/// <returns>An unsubscriber</returns>
		public IDisposable Subscribe(IObserver<SegmentGeometry> observer) {
			try {
				Monitor.Enter(Lock);
				observers.Add(observer);
			} finally {
				Monitor.Exit(Lock);
			}
			return new GenericUnsubscriber<SegmentGeometry>(observers, observer, Lock);
		}

		public static bool IsValid(ushort segmentId) {
			return Constants.ServiceFactory.NetService.IsSegmentValid(segmentId);
		}

		public bool IsValid() {
			return IsValid(SegmentId);
		}

		/*public LaneGeometry GetLane(int laneIndex, bool recalcIfNecessary=true) {
			if (LaneGeometries == null || laneIndex >= LaneGeometries.Length) {
				if (recalcIfNecessary) {
					RecalculateLaneGeometries();
				} else {
					return null;
				}
			}

			return LaneGeometries[laneIndex];
		}

		public void RecalculateLaneGeometries(GeometryCalculationMode calcMode) {
#if DEBUGGEO
			bool output = GlobalConfig.Instance.DebugSwitches[5];

			if (output)
				Log._Debug($">>> SegmentGeometry.RecalculateLaneGeometries({calcMode}): called for segment {SegmentId}. IsValid()={IsValid()}, wasValid={valid}");
#endif

			if (!IsValid()) {
				if (valid) {
					valid = false;
					LaneGeometries = null;

					if (calcMode == GeometryCalculationMode.Propagate) {
						PropagateLaneGeometryRebuild();
					}

					Cleanup();
				}
				return;
			}

			RecalculateLaneGeometries();
			if (calcMode == GeometryCalculationMode.Propagate) {
				PropagateLaneGeometryRebuild();
			}
		}

		private void PropagateLaneGeometryRebuild() {
			foreach (bool b in new bool[] { true, false }) {
				foreach (ushort otherSegmentId in GetConnectedSegments(b)) {
					if (otherSegmentId == 0) {
						continue;
					}

					SegmentGeometry.Get(otherSegmentId, true).RecalculateLaneGeometries();
				}
			}
		}*/

		public void StartRecalculation(GeometryCalculationMode calcMode) {
			Recalculate(calcMode);
			//RecalculateLaneGeometries(calcMode);
		}

		/// <summary>
		/// Requests recalculation of the managed segment's geometry data. If recalculation is not enforced (argument "force"),
		/// recalculation is only done if recalculation has not been recently executed.
		/// </summary>
		/// <param name="output">Specifies if logging should be performed</param>
		/// <param name="force">Specifies if recalculation should be enforced.</param>
		public void Recalculate(GeometryCalculationMode calcMode) {
#if DEBUGGEO
			bool output = GlobalConfig.Instance.DebugSwitches[5];

			if (output)
				Log._Debug($">>> SegmentGeometry.Recalculate({calcMode}): called for segment {SegmentId}. IsValid()={IsValid()}, wasValid={valid}");
#endif

			if (!IsValid()) {
				if (valid) {
					valid = false;

					if (calcMode == GeometryCalculationMode.Propagate) {
						startNodeGeometry.Recalculate(GeometryCalculationMode.Propagate);
						endNodeGeometry.Recalculate(GeometryCalculationMode.Propagate);
					}

					Cleanup();
					NotifyObservers();
				}
				return;
			}

			valid = true;
			try {
#if DEBUGGEO
				if (output)
					Log._Debug($"Trying to get a lock for Recalculating geometries of segment {SegmentId}...");
#endif
				//Monitor.Enter(Lock);

#if DEBUGGEO
				if (output)
					Log.Info($"Recalculating geometries of segment {SegmentId} STARTED");
#endif

				Cleanup();

				oneWay = calculateIsOneWay(SegmentId);
				highway = calculateIsHighway(SegmentId);
				buslane = calculateHasBusLane(SegmentId);
				startNodeGeometry.Recalculate(calcMode);
				endNodeGeometry.Recalculate(calcMode);

#if DEBUGGEO
				if (output) {
					Log.Info($"Recalculating geometries of segment {SegmentId} FINISHED (flags={Singleton<NetManager>.instance.m_segments.m_buffer[SegmentId].m_flags})");
					SegmentEndGeometry[] endGeometries = new SegmentEndGeometry[] { startNodeGeometry, endNodeGeometry };
					Log._Debug($"seg. {SegmentId}. oneWay={oneWay}");
					Log._Debug($"seg. {SegmentId}. highway={highway}");

					int i = 0;
					foreach (SegmentEndGeometry endGeometry in endGeometries) {
						if (i == 0)
							Log._Debug("--- end @ start node ---");
						else
							Log._Debug("--- end @ end node ---");
						Log._Debug($"Node {endGeometry.NodeId()} (flags={Singleton<NetManager>.instance.m_nodes.m_buffer[endGeometry.NodeId()].m_flags})");

						Log._Debug($"seg. {SegmentId}. connectedSegments={ string.Join(", ", endGeometry.ConnectedSegments.Select(x => x.ToString()).ToArray())}");

						Log._Debug($"seg. {SegmentId}. leftSegments={ string.Join(", ", endGeometry.LeftSegments.Select(x => x.ToString()).ToArray())}");
						Log._Debug($"seg. {SegmentId}. incomingLeftSegments={ string.Join(", ", endGeometry.IncomingLeftSegments.Select(x => x.ToString()).ToArray())}");
						Log._Debug($"seg. {SegmentId}. outgoingLeftSegments={ string.Join(", ", endGeometry.OutgoingLeftSegments.Select(x => x.ToString()).ToArray())}");

						Log._Debug($"seg. {SegmentId}. rightSegments={ string.Join(", ", endGeometry.RightSegments.Select(x => x.ToString()).ToArray())}");
						Log._Debug($"seg. {SegmentId}. incomingRightSegments={ string.Join(", ", endGeometry.IncomingRightSegments.Select(x => x.ToString()).ToArray())}");
						Log._Debug($"seg. {SegmentId}. outgoingRightSegments={ string.Join(", ", endGeometry.OutgoingRightSegments.Select(x => x.ToString()).ToArray())}");

						Log._Debug($"seg. {SegmentId}. straightSegments={ string.Join(", ", endGeometry.StraightSegments.Select(x => x.ToString()).ToArray())}");
						Log._Debug($"seg. {SegmentId}. incomingStraightSegments={ string.Join(", ", endGeometry.IncomingStraightSegments.Select(x => x.ToString()).ToArray())}");
						Log._Debug($"seg. {SegmentId}. outgoingStraightSegments={ string.Join(", ", endGeometry.OutgoingStraightSegments.Select(x => x.ToString()).ToArray())}");

						Log._Debug($"seg. {SegmentId}. onlyHighways={endGeometry.OnlyHighways}");
						Log._Debug($"seg. {SegmentId}. outgoingOneWay={endGeometry.OutgoingOneWay}");
						
						++i;
					}
				}
#endif

#if DEBUGGEO
				//Log._Debug($"Recalculation of segment {SegmentId} completed. Valid? {IsValid()}");
#endif
				NotifyObservers();
			} finally {
#if DEBUGGEO
				if (output)
					Log._Debug($"Lock released after recalculating geometries of segment {SegmentId}");
#endif
				//Monitor.Exit(Lock);
			}
		}

		public SegmentEndGeometry GetEnd(bool startNode) {
			if (! IsValid()) {
				return null;
			}

			if (startNode) {
				if (StartNodeGeometry != null && StartNodeGeometry.IsValid()) {
					return StartNodeGeometry;
				}
			} else if (EndNodeGeometry != null && EndNodeGeometry.IsValid()) {
				return EndNodeGeometry;
			}
			return null;
		}

		public SegmentEndGeometry GetEnd(ushort nodeId) {
			if (!IsValid()) {
				return null;
			}

			ushort startNodeId = StartNodeId();
			if (nodeId == startNodeId) {
				return StartNodeGeometry;
			} else {
				ushort endNodeId = EndNodeId();
				if (nodeId == endNodeId)
					return EndNodeGeometry;
			}
			return null;
		}

		/// <summary>
		/// Verifies the information that another is/is not connected to the managed segment. If the verification fails, a recalculation of geometry data is performed.
		/// The method does not necessarily guarantee that the segment geometry data regarding the queried segment with id "otherSegmentId" is correct.
		/// 
		/// This method should only be called if there is a good case to believe that the other segment may be connected to the managed segment.
		/// Else, a possibly unnecessary geometry recalculation is performed.
		/// </summary>
		/// <param name="otherSegmentId">The other segment that is could be connected to the managed segment.</param>
		/// <returns></returns>
		/*internal bool VerifyConnectedSegment(ushort otherSegmentId) {
			if ((Singleton<NetManager>.instance.m_segments.m_buffer[otherSegmentId].m_flags & NetSegment.Flags.Created) == NetSegment.Flags.None) {
				return false;
			}
			if (otherSegmentId == SegmentId)
				return false;

			bool segmentIsConnectedToStartNode = false;
			bool segmentIsConnectedToEndNode = false;
			try {
				Monitor.Enter(Lock);
				segmentIsConnectedToStartNode = startNodeGeometry.IsConnectedTo(otherSegmentId);
				segmentIsConnectedToEndNode = endNodeGeometry.IsConnectedTo(otherSegmentId);
			} finally {
				Monitor.Exit(Lock);
			}

			if (!segmentIsConnectedToStartNode && !segmentIsConnectedToEndNode) {
				Log.Warning($"Neither the segments of start node {startNodeGeometry.NodeId()} nor of end node {endNodeGeometry.NodeId()} of segment {SegmentId} contain the segment {otherSegmentId}");
                Recalculate(true);
				return true;
			}
			return false;
		}*/

		/// <summary>
		/// Runs a simple segment geometry verification that only checks if the stored number of connected segments at start and end node. 
		/// 
		/// If the numbers of connected segments at the given node mismatches, a geometry recalculation is performed.
		/// </summary>
		/// <param name="nodeId">Node at which segment counts should be checked</param>
		/// <returns>true if a recalculation has been performed, false otherwise</returns>
		/*internal bool VerifySegmentsByCount(bool startNode) {
			ushort nodeId = startNode ? startNodeGeometry.NodeId() : endNodeGeometry.NodeId();
			if (nodeId == 0 || (Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId].m_flags & NetNode.Flags.Created) == NetNode.Flags.None)
				return false;

			int expectedCount = Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId].CountSegments(NetSegment.Flags.Created, SegmentId);
			var storedCount = CountOtherSegments(startNode);
			if (storedCount != expectedCount) {
				Log._Debug($"The number of other segments (expected {expectedCount}) at node {nodeId} does not equals the stored count ({storedCount})");
				Recalculate();
				return true;
			}
			return false;
		}

		internal void VerifySegmentsByCount() {
			if (VerifySegmentsByCount(true))
				return;
			VerifySegmentsByCount(false);
		}*/

		/*internal void VerifyCreated() {
			if (!IsValid() && wasValid) {
				Log._Debug($"SegmentGeometry: Segment {SegmentId} has become invalid. Recalculating.");
				Recalculate(true);
			}
		}

		internal void VerifyByNodes() {
			if (startNodeGeometry.NodeId() != startNodeGeometry.LastKnownNodeId)
				startNodeGeometry.Recalculate(true);
			if (endNodeGeometry.NodeId() != endNodeGeometry.LastKnownNodeId)
				endNodeGeometry.Recalculate(true);
		}*/

		/// <summary>
		/// Determines the node id at the given segment end.
		/// </summary>
		/// <param name="startNode">defines if the segment should be checked at the start node (true) or end node (false)</param>
		/// <returns></returns>
		public ushort[] GetConnectedSegments(bool startNode) {
			SegmentEndGeometry endGeometry = startNode ? startNodeGeometry : endNodeGeometry;
			return endGeometry.ConnectedSegments;
		}

		/// <summary>
		/// Determines all connected segments at the given node.
		/// </summary>
		/// <param name="startNode">defines if the segment should be checked at the start node (true) or end node (false)</param>
		/// <returns></returns>
		public ushort GetNodeId(bool startNode) {
			if (!IsValid())
				return 0;
			SegmentEndGeometry endGeometry = startNode ? startNodeGeometry : endNodeGeometry;
			return endGeometry.NodeId();
		}

		/// <summary>
		/// Determines all incoming segments at the given node.
		/// </summary>
		/// <param name="startNode">defines if the segment should be checked at the start node (true) or end node (false)</param>
		/// <returns></returns>
		public ushort[] GetIncomingSegments(bool startNode) {
			SegmentEndGeometry endGeometry = startNode ? startNodeGeometry : endNodeGeometry;
			return endGeometry.GetIncomingSegments();
		}

		/// <summary>
		/// Determines all incoming straight segments at the given node.
		/// </summary>
		/// <param name="startNode">defines if the segment should be checked at the start node (true) or end node (false)</param>
		/// <returns></returns>
		public ushort[] GetIncomingStraightSegments(bool startNode) {
			SegmentEndGeometry endGeometry = startNode ? startNodeGeometry : endNodeGeometry;
			return endGeometry.IncomingStraightSegments;
		}

		/// <summary>
		/// Determines all incoming left segments at the given node.
		/// </summary>
		/// <param name="startNode">defines if the segment should be checked at the start node (true) or end node (false)</param>
		/// <returns></returns>
		public ushort[] GetIncomingLeftSegments(bool startNode) {
			SegmentEndGeometry endGeometry = startNode ? startNodeGeometry : endNodeGeometry;
			return endGeometry.IncomingLeftSegments;
		}

		/// <summary>
		/// Determines all incoming right segments at the given node.
		/// </summary>
		/// <param name="startNode">defines if the segment should be checked at the start node (true) or end node (false)</param>
		/// <returns></returns>
		public ushort[] GetIncomingRightSegments(bool startNode) {
			SegmentEndGeometry endGeometry = startNode ? startNodeGeometry : endNodeGeometry;
			return endGeometry.IncomingRightSegments;
		}

		/// <summary>
		/// Determines all outgoing segments at the given node.
		/// </summary>
		/// <param name="startNode">defines if the segment should be checked at the start node (true) or end node (false)</param>
		/// <returns></returns>
		public ushort[] GetOutgoingSegments(bool startNode) {
			SegmentEndGeometry endGeometry = startNode ? startNodeGeometry : endNodeGeometry;
			return endGeometry.GetOutgoingSegments();
		}

		/// <summary>
		/// Determines all outgoing straight segments at the given node.
		/// </summary>
		/// <param name="startNode">defines if the segment should be checked at the start node (true) or end node (false)</param>
		/// <returns></returns>
		public ushort[] GetOutgoingStraightSegments(bool startNode) {
			SegmentEndGeometry endGeometry = startNode ? startNodeGeometry : endNodeGeometry;
			return endGeometry.OutgoingStraightSegments;
		}

		/// <summary>
		/// Determines all outgoing left segments at the given node.
		/// </summary>
		/// <param name="startNode">defines if the segment should be checked at the start node (true) or end node (false)</param>
		/// <returns></returns>
		public ushort[] GetOutgoingLeftSegments(bool startNode) {
			SegmentEndGeometry endGeometry = startNode ? startNodeGeometry : endNodeGeometry;
			return endGeometry.OutgoingLeftSegments;
		}

		/// <summary>
		/// Determines all outgoing right segments at the given node.
		/// </summary>
		/// <param name="startNode">defines if the segment should be checked at the start node (true) or end node (false)</param>
		/// <returns></returns>
		public ushort[] GetOutgoingRightSegments(bool startNode) {
			SegmentEndGeometry endGeometry = startNode ? startNodeGeometry : endNodeGeometry;
			return endGeometry.OutgoingRightSegments;
		}

		/// <summary>
		/// Determines the number of segments connected to the managed segment at the given node.
		/// 
		/// A segment geometry verification is not performed.
		/// </summary>
		/// <param name="startNode">defines if the segment should be checked at the start node (true) or end node (false)</param>
		/// <returns>number of connected segments at the given node</returns>
		public int CountOtherSegments(bool startNode) {
			SegmentEndGeometry endGeometry = startNode ? startNodeGeometry : endNodeGeometry;
			return endGeometry.NumConnectedSegments;
		}

		/// <summary>
		/// Determines the number of left segments connected to the managed segment at the given node.
		/// 
		/// A segment geometry verification is not performed.
		/// </summary>
		/// <param name="startNode">defines if the segment should be checked at the start node (true) or end node (false)</param>
		/// <returns>number of connected left segments at the given node</returns>
		public int CountLeftSegments(bool startNode) {
			SegmentEndGeometry endGeometry = startNode ? startNodeGeometry : endNodeGeometry;
			return endGeometry.NumLeftSegments;
		}

		/// <summary>
		/// Determines the number of right segments connected to the managed segment at the given node.
		/// 
		/// A segment geometry verification is not performed.
		/// </summary>
		/// <param name="startNode">defines if the segment should be checked at the start node (true) or end node (false)</param>
		/// <returns>number of connected right segments at the given node</returns>
		public int CountRightSegments(bool startNode) {
			SegmentEndGeometry endGeometry = startNode ? startNodeGeometry : endNodeGeometry;
			return endGeometry.NumRightSegments;
		}

		/// <summary>
		/// Determines the number of straight segments connected to the managed segment at the given node.
		/// 
		/// A segment geometry verification is not performed.
		/// </summary>
		/// <param name="startNode">defines if the segment should be checked at the start node (true) or end node (false)</param>
		/// <returns>number of connected straight segments at the given node</returns>
		public int CountStraightSegments(bool startNode) {
			SegmentEndGeometry endGeometry = startNode ? startNodeGeometry : endNodeGeometry;
			return endGeometry.NumStraightSegments;
		}

		/// <summary>
		/// Determines the number of incoming segments connected to the managed segment at the given node.
		/// 
		/// A segment geometry verification is not performed.
		/// </summary>
		/// <param name="startNode">defines if the segment should be checked at the start node (true) or end node (false)</param>
		/// <returns>number of connected incoming left segments at the given node</returns>
		public int CountIncomingSegments(bool startNode) {
			SegmentEndGeometry endGeometry = startNode ? startNodeGeometry : endNodeGeometry;
			return endGeometry.NumIncomingSegments;
		}

		/// <summary>
		/// Determines the number of incoming left segments connected to the managed segment at the given node.
		/// 
		/// A segment geometry verification is not performed.
		/// </summary>
		/// <param name="startNode">defines if the segment should be checked at the start node (true) or end node (false)</param>
		/// <returns>number of connected incoming left segments at the given node</returns>
		public int CountIncomingLeftSegments(bool startNode) {
			SegmentEndGeometry endGeometry = startNode ? startNodeGeometry : endNodeGeometry;
			return endGeometry.NumIncomingLeftSegments;
		}

		/// <summary>
		/// Determines the number of incoming right segments connected to the managed segment at the given node.
		/// 
		/// A segment geometry verification is not performed.
		/// </summary>
		/// <param name="startNode">defines if the segment should be checked at the start node (true) or end node (false)</param>
		/// <returns>number of connected incoming right segments at the given node</returns>
		public int CountIncomingRightSegments(bool startNode) {
			SegmentEndGeometry endGeometry = startNode ? startNodeGeometry : endNodeGeometry;
			return endGeometry.NumIncomingRightSegments;
		}

		/// <summary>
		/// Determines the number of incoming straight segments connected to the managed segment at the given node.
		/// 
		/// A segment geometry verification is not performed.
		/// </summary>
		/// <param name="startNode">defines if the segment should be checked at the start node (true) or end node (false)</param>
		/// <returns>number of connected incoming straight segments at the given node</returns>
		public int CountIncomingStraightSegments(bool startNode) {
			SegmentEndGeometry endGeometry = startNode ? startNodeGeometry : endNodeGeometry;
			return endGeometry.NumIncomingStraightSegments;
		}

		/// <summary>
		/// Determines the number of outgoing segments connected to the managed segment at the given node.
		/// 
		/// A segment geometry verification is not performed.
		/// </summary>
		/// <param name="startNode">defines if the segment should be checked at the start node (true) or end node (false)</param>
		/// <returns>number of connected incoming left segments at the given node</returns>
		public int CountOutgoingSegments(bool startNode) {
			SegmentEndGeometry endGeometry = startNode ? startNodeGeometry : endNodeGeometry;
			return endGeometry.NumOutgoingSegments;
		}

		/// <summary>
		/// Determines the number of outgoing left segments connected to the managed segment at the given node.
		/// 
		/// A segment geometry verification is not performed.
		/// </summary>
		/// <param name="startNode">defines if the segment should be checked at the start node (true) or end node (false)</param>
		/// <returns>number of connected outgoing left segments at the given node</returns>
		public int CountOutgoingLeftSegments(bool startNode) {
			SegmentEndGeometry endGeometry = startNode ? startNodeGeometry : endNodeGeometry;
			return endGeometry.NumOutgoingLeftSegments;
		}

		/// <summary>
		/// Determines the number of outgoing right segments connected to the managed segment at the given node.
		/// 
		/// A segment geometry verification is not performed.
		/// </summary>
		/// <param name="startNode">defines if the segment should be checked at the start node (true) or end node (false)</param>
		/// <returns>number of connected outgoing right segments at the given node</returns>
		public int CountOutgoingRightSegments(bool startNode) {
			SegmentEndGeometry endGeometry = startNode ? startNodeGeometry : endNodeGeometry;
			return endGeometry.NumOutgoingRightSegments;
		}

		/// <summary>
		/// Determines the number of outgoing straight segments connected to the managed segment at the given node.
		/// 
		/// A segment geometry verification is not performed.
		/// </summary>
		/// <param name="startNode">defines if the segment should be checked at the start node (true) or end node (false)</param>
		/// <returns>number of connected outgoing straight segments at the given node</returns>
		public int CountOutgoingStraightSegments(bool startNode) {
			SegmentEndGeometry endGeometry = startNode ? startNodeGeometry : endNodeGeometry;
			return endGeometry.NumOutgoingStraightSegments;
		}

		/// <summary>
		/// Determines if the managed segment is connected to left segments at the given node.
		/// 
		/// A segment geometry verification is not performed.
		/// </summary>
		/// <param name="startNode">defines if the segment should be checked at the start node (true) or end node (false)</param>
		/// <returns>true, if, according to the stored geometry data, the managed segment is connected to left segments at the given node, else false.</returns>
		public bool HasLeftSegment(bool startNode) {
			return CountLeftSegments(startNode) > 0;
		}

		/// <summary>
		/// Determines if the managed segment is connected to right segments at the given node.
		/// 
		/// A segment geometry verification is not performed.
		/// </summary>
		/// <param name="startNode">defines if the segment should be checked at the start node (true) or end node (false)</param>
		/// <returns>true, if, according to the stored geometry data, the managed segment is connected to right segments at the given node, else false.</returns>
		public bool HasRightSegment(bool startNode) {
			return CountRightSegments(startNode) > 0;
		}

		/// <summary>
		/// Determines if the managed segment is connected to straight segments at the given node.
		/// 
		/// A segment geometry verification is not performed.
		/// </summary>
		/// <param name="startNode">defines if the segment should be checked at the start node (true) or end node (false)</param>
		/// <returns>true, if, according to the stored geometry data, the managed segment is connected to straight segments at the given node, else false.</returns>
		public bool HasStraightSegment(bool startNode) {
			return CountStraightSegments(startNode) > 0;
		}

		/// <summary>
		/// Determines if the managed segment is connected to incoming left segments at the given node.
		/// 
		/// A segment geometry verification is not performed.
		/// </summary>
		/// <param name="startNode">defines if the segment should be checked at the start node (true) or end node (false)</param>
		/// <returns>true, if, according to the stored geometry data, the managed segment is connected to incoming left segments at the given node, else false.</returns>
		public bool HasIncomingLeftSegment(bool startNode) {
			return CountIncomingLeftSegments(startNode) > 0;
		}

		/// <summary>
		/// Determines if the managed segment is connected to incoming right segments at the given node.
		/// 
		/// A segment geometry verification is not performed.
		/// </summary>
		/// <param name="startNode">defines if the segment should be checked at the start node (true) or end node (false)</param>
		/// <returns>true, if, according to the stored geometry data, the managed segment is connected to incoming right segments at the given node, else false.</returns>
		public bool HasIncomingRightSegment(bool startNode) {
			return CountIncomingRightSegments(startNode) > 0;
		}

		/// <summary>
		/// Determines if the managed segment is connected to incoming straight segments at the given node.
		/// 
		/// A segment geometry verification is not performed.
		/// </summary>
		/// <param name="startNode">defines if the segment should be checked at the start node (true) or end node (false)</param>
		/// <returns>true, if, according to the stored geometry data, the managed segment is connected to incoming straight segments at the given node, else false.</returns>
		public bool HasIncomingStraightSegment(bool startNode) {
			return CountIncomingStraightSegments(startNode) > 0;
		}

		/// <summary>
		/// Determines if the managed segment is connected to outgoing left segments at the given node.
		/// 
		/// A segment geometry verification is not performed.
		/// </summary>
		/// <param name="startNode">defines if the segment should be checked at the start node (true) or end node (false)</param>
		/// <returns>true, if, according to the stored geometry data, the managed segment is connected to outgoing left segments at the given node, else false.</returns>
		public bool HasOutgoingLeftSegment(bool startNode) {
			return CountOutgoingLeftSegments(startNode) > 0;
		}

		/// <summary>
		/// Determines if the managed segment is connected to outgoing right segments at the given node.
		/// 
		/// A segment geometry verification is not performed.
		/// </summary>
		/// <param name="startNode">defines if the segment should be checked at the start node (true) or end node (false)</param>
		/// <returns>true, if, according to the stored geometry data, the managed segment is connected to outgoing right segments at the given node, else false.</returns>
		public bool HasOutgoingRightSegment(bool startNode) {
			return CountOutgoingRightSegments(startNode) > 0;
		}

		/// <summary>
		/// Determines if the managed segment is connected to outgoing straight segments at the given node.
		/// 
		/// A segment geometry verification is not performed.
		/// </summary>
		/// <param name="startNode">defines if the segment should be checked at the start node (true) or end node (false)</param>
		/// <returns>true, if, according to the stored geometry data, the managed segment is connected to outgoing straight segments at the given node, else false.</returns>
		public bool HasOutgoingStraightSegment(bool startNode) {
			return CountOutgoingStraightSegments(startNode) > 0;
		}

		/// <summary>
		/// Determines if, according to the stored geometry data, the given segment is connected to the managed segment and is a left segment at the given node relatively to the managed segment.
		/// 
		/// A segment geometry verification is not performed.
		/// </summary>
		/// <param name="toSegmentId">other segment that ought to be left, relatively to the managed segment</param>
		/// <param name="startNode">defines if the segment should be checked at the start node (true) or end node (false)</param>
		/// <returns>true, if the other segment is, according to the stored geometry data, connected on the left-hand side of the managed segment at the given node</returns>
		public bool IsLeftSegment(ushort toSegmentId, bool startNode) {
			if (!IsValid(toSegmentId))
				return false;
			if (toSegmentId == SegmentId)
				return false;

			SegmentEndGeometry endGeometry = startNode ? startNodeGeometry : endNodeGeometry;

			bool contains = false;
			foreach (ushort segId in endGeometry.LeftSegments)
				if (segId == toSegmentId) {
					contains = true;
					break;
				}
			return contains;
		}

		/// <summary>
		/// Determines if, according to the stored geometry data, the given segment is connected to the managed segment and is a right segment at the given node relatively to the managed segment.
		/// 
		/// A segment geometry verification is not performed.
		/// </summary>
		/// <param name="toSegmentId">other segment that ought to be right, relatively to the managed segment</param>
		/// <param name="startNode">defines if the segment should be checked at the start node (true) or end node (false)</param>
		/// <returns>true, if the other segment is, according to the stored geometry data, connected on the right-hand side of the managed segment at the given node</returns>
		public bool IsRightSegment(ushort toSegmentId, bool startNode) {
			if (!IsValid(toSegmentId))
				return false;
			if (toSegmentId == SegmentId)
				return false;

			SegmentEndGeometry endGeometry = startNode ? startNodeGeometry : endNodeGeometry;

			bool contains = false;
			foreach (ushort segId in endGeometry.RightSegments)
				if (segId == toSegmentId) {
					contains = true;
					break;
				}
			return contains;
		}

		/// <summary>
		/// Determines if, according to the stored geometry data, the given segment is connected to the managed segment and is a straight segment at the given node relatively to the managed segment.
		/// 
		/// A segment geometry verification is not performed.
		/// </summary>
		/// <param name="toSegmentId">other segment that ought to be straight, relatively to the managed segment</param>
		/// <param name="startNode">defines if the segment should be checked at the start node (true) or end node (false)</param>
		/// <returns>true, if the other segment is, according to the stored geometry data, connected straight-wise to the managed segment at the given node</returns>
		public bool IsStraightSegment(ushort toSegmentId, bool startNode) {
			if (!IsValid(toSegmentId))
				return false;
			if (toSegmentId == SegmentId)
				return false;

			SegmentEndGeometry endGeometry = startNode ? startNodeGeometry : endNodeGeometry;
			
			bool contains = false;
			foreach (ushort segId in endGeometry.StraightSegments)
				if (segId == toSegmentId) {
					contains = true;
					break;
				}
			return contains;
		}

		/// <summary>
		/// Determines if, according to the stored geometry data, the given segment is connected to the managed segment and is a left segment at the given node relatively to the managed segment.
		/// 
		/// A segment geometry verification is not performed.
		/// </summary>
		/// <param name="toSegmentId">other segment that ought to be left, relatively to the managed segment</param>
		/// <param name="startNode">defines if the segment should be checked at the start node (true) or end node (false)</param>
		/// <returns>true, if the other segment is, according to the stored geometry data, connected on the left-hand side of the managed segment at the given node</returns>
		public bool IsIncomingLeftSegment(ushort toSegmentId, bool startNode) {
			if (!IsValid(toSegmentId))
				return false;
			if (toSegmentId == SegmentId)
				return false;

			SegmentEndGeometry endGeometry = startNode ? startNodeGeometry : endNodeGeometry;

			bool contains = false;
			foreach (ushort segId in endGeometry.IncomingLeftSegments)
				if (segId == toSegmentId) {
					contains = true;
					break;
				}
			return contains;
		}

		/// <summary>
		/// Determines if, according to the stored geometry data, the given segment is connected to the managed segment and is a right segment at the given node relatively to the managed segment.
		/// 
		/// A segment geometry verification is not performed.
		/// </summary>
		/// <param name="toSegmentId">other segment that ought to be right, relatively to the managed segment</param>
		/// <param name="startNode">defines if the segment should be checked at the start node (true) or end node (false)</param>
		/// <returns>true, if the other segment is, according to the stored geometry data, connected on the right-hand side of the managed segment at the given node</returns>
		public bool IsIncomingRightSegment(ushort toSegmentId, bool startNode) {
			if (!IsValid(toSegmentId))
				return false;
			if (toSegmentId == SegmentId)
				return false;

			SegmentEndGeometry endGeometry = startNode ? startNodeGeometry : endNodeGeometry;
			
			bool contains = false;
			foreach (ushort segId in endGeometry.IncomingRightSegments)
				if (segId == toSegmentId) {
					contains = true;
					break;
				}
			return contains;
		}

		/// <summary>
		/// Determines if, according to the stored geometry data, the given segment is connected to the managed segment and is a straight segment at the given node relatively to the managed segment.
		/// 
		/// A segment geometry verification is not performed.
		/// </summary>
		/// <param name="toSegmentId">other segment that ought to be straight, relatively to the managed segment</param>
		/// <param name="startNode">defines if the segment should be checked at the start node (true) or end node (false)</param>
		/// <returns>true, if the other segment is, according to the stored geometry data, connected straight-wise to the managed segment at the given node</returns>
		public bool IsIncomingStraightSegment(ushort toSegmentId, bool startNode) {
			if (!IsValid(toSegmentId))
				return false;
			if (toSegmentId == SegmentId)
				return false;

			SegmentEndGeometry endGeometry = startNode ? startNodeGeometry : endNodeGeometry;
			
			bool contains = false;
			foreach (ushort segId in endGeometry.IncomingStraightSegments)
				if (segId == toSegmentId) {
					contains = true;
					break;
				}
			return contains;
		}

		/// <summary>
		/// Determines if, according to the stored geometry data, the managed segment is only connected to highways at the given node.
		/// 
		/// A segment geometry verification is not performed.
		/// </summary>
		/// <returns>true, if, according to the stored geometry data, the managed segment is only connected to highways at the given node, false otherwise</returns>
		public bool HasOnlyHighways(bool startNode) {
			SegmentEndGeometry endGeometry = startNode ? startNodeGeometry : endNodeGeometry;
			return endGeometry.OnlyHighways;
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
		/// Determines if, according to the stored geometry data, the managed segment is a highway.
		/// 
		/// A segment geometry verification is not performed.
		/// </summary>
		/// <returns>true, if, according to the stored geometry data, the managed segment is a highway, false otherwise</returns>
		public bool IsHighway() {
			return highway;
		}

		/// <summary>
		/// Determines if, according to the stored data, the managed segment has a buslane.
		/// </summary>
		/// <returns></returns>
		public bool HasBusLane() {
			return buslane;
		}

		/// <summary>
		/// Determines if, according to the stored geometry data, the managed segment is an outgoing one-way road at the given node.
		/// 
		/// A segment geometry verification is not performed.
		/// </summary>
		/// <param name="startNode">defines if the segment should be checked at the start node (true) or end node (false)</param>
		/// <returns>true, if, according to the stored geometry data, the managed segment is an outgoing one-way road at the given node, false otherwise</returns>
		public bool IsOutgoingOneWay(bool startNode) {
			SegmentEndGeometry endGeometry = startNode ? startNodeGeometry : endNodeGeometry;
			return endGeometry.OutgoingOneWay;
		}

		/// <summary>
		/// Determines if, according to the stored geometry data, the managed segment is an incoming one-way road at the given node.
		/// 
		/// A segment geometry verification is not performed.
		/// </summary>
		/// <param name="startNode">defines if the segment should be checked at the start node (true) or end node (false)</param>
		/// <returns>true, if, according to the stored geometry data, the managed segment is an incoming one-way road at the given node, false otherwise</returns>
		public bool IsIncomingOneWay(bool startNode) {
			return (IsOneWay() && !IsOutgoingOneWay(startNode));
		}

		/// <summary>
		/// Determines if, according to the stored geometry data, the managed segment is an incoming road at the given node.
		/// 
		/// A segment geometry verification is not performed.
		/// </summary>
		/// <param name="startNode">defines if the segment should be checked at the start node (true) or end node (false)</param>
		/// <returns>true, if, according to the stored geometry data, the managed segment is an incoming road at the given node, false otherwise</returns>
		public bool IsIncoming(bool startNode) {
			return !IsOutgoingOneWay(startNode);
		}

		/// <summary>
		/// Determines if, according to the stored geometry data, the managed segment is an outgoing road at the given node.
		/// 
		/// A segment geometry verification is not performed.
		/// </summary>
		/// <param name="startNode">defines if the segment should be checked at the start node (true) or end node (false)</param>
		/// <returns>true, if, according to the stored geometry data, the managed segment is an outgoing road at the given node, false otherwise</returns>
		public bool IsOutgoing(bool startNode) {
			return !IsIncomingOneWay(startNode);
		}

		/// <summary>
		/// Determines the relative direction of the other segment relatively to the managed segment at the given node, according to the stored geometry information.
		/// 
		/// A segment geometry verification is not performed.
		/// </summary>
		/// <param name="otherSegmentId">other segment</param>
		/// <param name="startNode">defines if the segment should be checked at the start node (true) or end node (false)</param>
		/// <returns>relative direction of the other segment relatively to the managed segment at the given node</returns>
		public ArrowDirection GetDirection(ushort otherSegmentId, bool startNode) {
			if (!IsValid(otherSegmentId))
				return ArrowDirection.None;

			if (otherSegmentId == SegmentId)
				return ArrowDirection.Turn;
			else if (IsRightSegment(otherSegmentId, startNode))
				return ArrowDirection.Right;
			else if (IsLeftSegment(otherSegmentId, startNode))
				return ArrowDirection.Left;
			else
				return ArrowDirection.Forward;
		}

		/// <summary>
		/// Determines if highway merging/splitting rules are activated at the managed segment for the given node.
		/// 
		/// A segment geometry verification is not performed.
		/// </summary>
		/// <param name="startNode"></param>
		/// <returns></returns>
		[Obsolete]
		public bool AreHighwayRulesEnabled(bool startNode) {
			if (!Options.highwayRules)
				return false;
			if (!IsIncomingOneWay(startNode))
				return false;
			if (!(Singleton<NetManager>.instance.m_segments.m_buffer[SegmentId].Info.m_netAI is RoadBaseAI))
				return false;
			if (!((RoadBaseAI)Singleton<NetManager>.instance.m_segments.m_buffer[SegmentId].Info.m_netAI).m_highwayRules)
				return false;

			SegmentEndGeometry endGeometry = startNode ? startNodeGeometry : endNodeGeometry;

			if (endGeometry.NumConnectedSegments <= 1)
				return false;

			bool nextAreOnlyOneWayHighways = true;
			foreach (ushort otherSegmentId in endGeometry.ConnectedSegments) {
				if (Singleton<NetManager>.instance.m_segments.m_buffer[otherSegmentId].Info.m_netAI is RoadBaseAI) {
					if (! SegmentGeometry.Get(otherSegmentId).IsOneWay() || !((RoadBaseAI)Singleton<NetManager>.instance.m_segments.m_buffer[otherSegmentId].Info.m_netAI).m_highwayRules) {
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
		internal static bool calculateIsOutgoingOneWay(ushort segmentId, ushort nodeId) { // TODO move to SegmentEnd
			if (!IsValid(segmentId))
				return false;

			var instance = Singleton<NetManager>.instance;

			var info = instance.m_segments.m_buffer[segmentId].Info;

			NetInfo.Direction dir = Constants.ServiceFactory.NetService.GetFinalSegmentEndDirection(segmentId, ref instance.m_segments.m_buffer[segmentId], instance.m_segments.m_buffer[segmentId].m_startNode == nodeId);

			var laneId = instance.m_segments.m_buffer[segmentId].m_lanes;
			var laneIndex = 0;
			while (laneIndex < info.m_lanes.Length && laneId != 0u) {
				bool validLane = (info.m_lanes[laneIndex].m_laneType & (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) != NetInfo.LaneType.None &&
					(info.m_lanes[laneIndex].m_vehicleType & (VehicleInfo.VehicleType.Car | VehicleInfo.VehicleType.Train | VehicleInfo.VehicleType.Tram | VehicleInfo.VehicleType.Metro | VehicleInfo.VehicleType.Monorail)) != VehicleInfo.VehicleType.None;
				// TODO the lane types and vehicle types should be specified to make it clear which lanes we need to check

				if (validLane) {
					if ((info.m_lanes[laneIndex].m_finalDirection & dir) != NetInfo.Direction.None) {
						return false;
					}
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
			if (!IsValid(segmentId))
				return false;

			var instance = Singleton<NetManager>.instance;

			var info = instance.m_segments.m_buffer[segmentId].Info;

			var hasForward = false;
			var hasBackward = false;

			var laneId = instance.m_segments.m_buffer[segmentId].m_lanes;
			var laneIndex = 0;
			while (laneIndex < info.m_lanes.Length && laneId != 0u) {
				bool validLane = (info.m_lanes[laneIndex].m_laneType & (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) != NetInfo.LaneType.None &&
					(info.m_lanes[laneIndex].m_vehicleType & (VehicleInfo.VehicleType.Car | VehicleInfo.VehicleType.Train | VehicleInfo.VehicleType.Tram | VehicleInfo.VehicleType.Metro | VehicleInfo.VehicleType.Monorail)) != VehicleInfo.VehicleType.None;
				// TODO the lane types and vehicle types should be specified to make it clear which lanes we need to check

				if (validLane) {
					if ((info.m_lanes[laneIndex].m_direction & NetInfo.Direction.Forward) != NetInfo.Direction.None) {
						hasForward = true;
					}

					if ((info.m_lanes[laneIndex].m_direction & NetInfo.Direction.Backward) != NetInfo.Direction.None) {
						hasBackward = true;
					}

					if (hasForward && hasBackward) {
						return false;
					}
				}

				laneId = instance.m_lanes.m_buffer[(int)((UIntPtr)laneId)].m_nextLane;
				laneIndex++;
			}

			return true;
		}

		/// <summary>
		/// Calculates if the given segment has a buslane.
		/// </summary>
		/// <param name="segmentId">segment to check</param>
		/// <returns>true, if the given segment has a buslane, false otherwise</returns>
		internal static bool calculateHasBusLane(ushort segmentId) {
			if (!IsValid(segmentId))
				return false;

			bool ret = false;
			Constants.ServiceFactory.NetService.ProcessSegment(segmentId, delegate (ushort segId, ref NetSegment segment) {
				ret = calculateHasBusLane(segment.Info);
				return true;
			});
			return ret;
		}

		/// <summary>
		/// Calculates if the given segment info describes a segment having a bus lane
		/// </summary>
		/// <param name="segmentInfo"></param>
		/// <returns></returns>
		internal static bool calculateHasBusLane(NetInfo segmentInfo) {
			for (int laneIndex = 0; laneIndex < segmentInfo.m_lanes.Length; ++laneIndex) {
				if (segmentInfo.m_lanes[laneIndex].m_laneType == NetInfo.LaneType.TransportVehicle &&
					(segmentInfo.m_lanes[laneIndex].m_vehicleType & VehicleInfo.VehicleType.Car) != VehicleInfo.VehicleType.None) {
					return true;
				}
			}

			return false;
		}

		internal static void calculateOneWayAtNode(ushort segmentId, ushort nodeId, out bool isOneway, out bool isOutgoingOneWay) {
			if (!IsValid(segmentId)) {
				isOneway = false;
				isOutgoingOneWay = false;
				return;
			}

			var instance = Singleton<NetManager>.instance;

			var info = instance.m_segments.m_buffer[segmentId].Info;

			var dir = NetInfo.Direction.Forward;
			if (instance.m_segments.m_buffer[segmentId].m_startNode == nodeId)
				dir = NetInfo.Direction.Backward;
			var dir2 = ((instance.m_segments.m_buffer[segmentId].m_flags & NetSegment.Flags.Invert) == NetSegment.Flags.None) ? dir : NetInfo.InvertDirection(dir);

			var hasForward = false;
			var hasBackward = false;
			isOutgoingOneWay = true;
			var laneId = instance.m_segments.m_buffer[segmentId].m_lanes;
			var laneIndex = 0;
			while (laneIndex < info.m_lanes.Length && laneId != 0u) {
				bool validLane = (info.m_lanes[laneIndex].m_laneType & (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) != NetInfo.LaneType.None &&
					(info.m_lanes[laneIndex].m_vehicleType & (VehicleInfo.VehicleType.Car | VehicleInfo.VehicleType.Train | VehicleInfo.VehicleType.Tram | VehicleInfo.VehicleType.Metro | VehicleInfo.VehicleType.Monorail)) != VehicleInfo.VehicleType.None;
				// TODO the lane types and vehicle types should be specified to make it clear which lanes we need to check

				if (validLane) {
					if ((info.m_lanes[laneIndex].m_finalDirection & dir2) != NetInfo.Direction.None) {
						isOutgoingOneWay = false;
					}

					if ((info.m_lanes[laneIndex].m_direction & NetInfo.Direction.Forward) != NetInfo.Direction.None) {
						hasForward = true;
					}

					if ((info.m_lanes[laneIndex].m_direction & NetInfo.Direction.Backward) != NetInfo.Direction.None) {
						hasBackward = true;
					}
				}

				laneId = instance.m_lanes.m_buffer[laneId].m_nextLane;
				laneIndex++;
			}

			isOneway = !(hasForward && hasBackward);
			if (!isOneway)
				isOutgoingOneWay = false;
		}

		/// <summary>
		/// Calculates if the given segment is a highway
		/// </summary>
		/// <param name="segmentId"></param>
		/// <returns></returns>
		internal static bool calculateIsHighway(ushort segmentId) {
			if (!IsValid(segmentId))
				return false;

			bool ret = false;
			Constants.ServiceFactory.NetService.ProcessSegment(segmentId, delegate (ushort segId, ref NetSegment segment) {
				ret = calculateIsHighway(segment.Info);
				return true;
			});
			return ret;
		}

		/// <summary>
		/// Calculates if the given segment info describes a highway segment
		/// </summary>
		/// <param name="segmentInfo"></param>
		/// <returns></returns>
		internal static bool calculateIsHighway(NetInfo segmentInfo) {
			if (segmentInfo.m_netAI is RoadBaseAI)
				return ((RoadBaseAI)segmentInfo.m_netAI).m_highwayRules;
			return false;
		}

		/// <summary>
		/// Clears the segment geometry data.
		/// </summary>
		private void Cleanup() {
			highway = false;
			oneWay = false;
			buslane = false;

			try {
				//Monitor.Enter(Lock);

				startNodeGeometry.Cleanup();
				endNodeGeometry.Cleanup();

				// reset highway lane arrows
				//Flags.removeHighwayLaneArrowFlagsAtSegment(SegmentId); // TODO refactor

				// clear default vehicle type cache
				VehicleRestrictionsManager.Instance.ClearCache(SegmentId);
			} finally {
				//Monitor.Exit(Lock);
			}
		}

		public bool Equals(SegmentGeometry otherSegGeo) {
			if (otherSegGeo == null) {
				return false;
			}
			return SegmentId == otherSegGeo.SegmentId;
		}

		public override bool Equals(object other) {
			if (other == null) {
				return false;
			}
			if (!(other is SegmentGeometry)) {
				return false;
			}
			return Equals((SegmentGeometry)other);
		}

		public override int GetHashCode() {
			int prime = 31;
			int result = 1;
			result = prime * result + SegmentId.GetHashCode();
			return result;
		}

		// static methods

		static SegmentGeometry() {
			segmentGeometries = new SegmentGeometry[NetManager.MAX_SEGMENT_COUNT];
		}

		internal static void OnBeforeLoadData() {
			Log._Debug($"Building {segmentGeometries.Length} segment geometries...");
			for (ushort i = 0; i < segmentGeometries.Length; ++i) {
				segmentGeometries[i] = new SegmentGeometry(i);
			}
			for (ushort i = 0; i < segmentGeometries.Length; ++i) {
				segmentGeometries[i].Recalculate(GeometryCalculationMode.Init);
			}
			/*for (ushort i = 0; i < segmentGeometries.Length; ++i) {
				segmentGeometries[i].RecalculateLaneGeometries(GeometryCalculationMode.Init);
			}*/
			Log._Debug($"Calculated segment geometries.");
		}

		internal static void OnBeforeSaveData() {
			/*Log._Debug($"Recalculating all segment geometries...");
			for (ushort i = 0; i < segmentGeometries.Length; ++i) {
				segmentGeometries[i].Recalculate(false);
			}
			Log._Debug($"Calculated segment geometries.");*/
		}

		public static SegmentGeometry Get(ushort segmentId, bool ignoreInvalid=false) {
			if (segmentGeometries == null) {
				return null;
			}

			SegmentGeometry segGeo = segmentGeometries[segmentId];
			if (segGeo == null || (! ignoreInvalid && ! segGeo.valid)) {
				return null;
			}
			return segGeo;
		}

		/*private void RecalculateLaneGeometries() {
			RebuildLaneGeometries();
			foreach (LaneGeometry laneGeo in LaneGeometries) {
				laneGeo.Recalculate();
			}
		}*/

		/*private void RebuildLaneGeometries() {
			LaneGeometry[] newLaneGeos = null;

			if (IsValid()) {
				Constants.ServiceFactory.NetService.ProcessSegment(SegmentId, delegate (ushort segmentId, ref NetSegment segment) {
					newLaneGeos = new LaneGeometry[segment.Info.m_lanes.Length];
					return true;
				});

				int minNum = 0;
				if (LaneGeometries != null) {
					minNum = Math.Min(newLaneGeos.Length, LaneGeometries.Length);
					for (int i = 0; i < minNum; ++i) {
						newLaneGeos[i] = LaneGeometries[i];
					}
				}

				for (int i = minNum; i < newLaneGeos.Length; ++i) {
					newLaneGeos[i] = new LaneGeometry(SegmentId, i);
				}
			}

			LaneGeometries = newLaneGeos;
		}*/

		private void NotifyObservers() {
			List<IObserver<SegmentGeometry>> myObservers = new List<IObserver<SegmentGeometry>>(observers); // in case somebody unsubscribes while iterating over subscribers
			foreach (IObserver<SegmentGeometry> observer in myObservers) {
				observer.OnUpdate(this);
			}
		}
	}
}
