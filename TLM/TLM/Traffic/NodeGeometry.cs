#define DEBUGGEOx

using ColossalFramework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using TrafficManager.State;
using TrafficManager.TrafficLight;
using TrafficManager.Util;

namespace TrafficManager.Traffic {
	public class NodeGeometry : IObservable<NodeGeometry> {
		private static NodeGeometry[] nodeGeometries;

		public ushort NodeId {
			get; private set;
		} = 0;

		public bool IsSimpleJunction {
			get; private set;
		} = false;

		public SegmentEndGeometry[] SegmentEndGeometries {
			get; private set;
		} = new SegmentEndGeometry[8];

		/// <summary>
		/// Holds a list of observers which are being notified as soon as the managed node's geometry is updated (but not neccessarily modified)
		/// </summary>
		private List<IObserver<NodeGeometry>> observers = new List<IObserver<NodeGeometry>>();

		/// <summary>
		/// Lock object. Acquire this before accessing the HashSets.
		/// </summary>
		public readonly object Lock = new object();

		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="nodeId">id of the managed node</param>
		public NodeGeometry(ushort nodeId) {
			this.NodeId = nodeId;
		}

		public bool IsValid() {
			return (Singleton<NetManager>.instance.m_nodes.m_buffer[NodeId].m_flags & (NetNode.Flags.Created | NetNode.Flags.Deleted)) == NetNode.Flags.Created;
		}

		/// <summary>
		/// Registers an observer.
		/// </summary>
		/// <param name="observer"></param>
		/// <returns>An unsubscriber</returns>
		public IDisposable Subscribe(IObserver<NodeGeometry> observer) {
			try {
				Monitor.Enter(Lock);
				observers.Add(observer);
			} finally {
				Monitor.Exit(Lock);
			}
			return new GenericUnsubscriber<NodeGeometry>(observers, observer, Lock);
		}
		
		internal void AddSegment(ushort segmentId, bool startNode, bool propagate) {
#if DEBUGGEO
			Log._Debug($"NodeGeometry: Add segment {segmentId}, start? {startNode} @ node {NodeId}");
#endif
			if (!IsValid()) {
				Log.Error($"NodeGeometry: Trying to add segment {segmentId} @ invalid node {NodeId}");
				Recalculate();
				return;
			}
			
			RemoveSegment(segmentId, false); // fallback: remove segment

			for (int i = 0; i < 8; ++i) {
				if (SegmentEndGeometries[i] == null) {
					SegmentEndGeometries[i] = startNode ? SegmentGeometry.Get(segmentId).StartNodeGeometry : SegmentGeometry.Get(segmentId).EndNodeGeometry;
					break;
				}
			}

			if (propagate)
				RecalculateSegments(segmentId);
			Recalculate();
		}

		internal void RemoveSegment(ushort segmentId, bool propagate) {
#if DEBUGGEO
			Log._Debug($"NodeGeometry: Remove segment {segmentId} @ node {NodeId}, propagate? {propagate}");
#endif
			if (!IsValid()) {
				Log.Warning($"NodeGeometry: Trying to remove segment {segmentId} @ invalid node {NodeId}");
				Recalculate();
				return;
			}

			for (int i = 0; i < 8; ++i) {
				if (SegmentEndGeometries[i]?.SegmentId == segmentId) {
					SegmentEndGeometries[i] = null;
				}
			}

			if (propagate)
				RecalculateSegments(segmentId);
			Recalculate();
		}

		private void Cleanup() {
			IsSimpleJunction = false;
		}

		internal void RecalculateSegments(ushort? ignoreSegmentId= null) {
#if DEBUGGEO
			Log._Debug($"NodeGeometry: Propagate @ {NodeId}. ignoreSegmentId={ignoreSegmentId}");
#endif

			// recalculate (other) segments
			for (int i = 0; i < 8; ++i) {
				if (SegmentEndGeometries[i] != null) {
					if (ignoreSegmentId != null && SegmentEndGeometries[i].SegmentId == ignoreSegmentId)
						continue;
#if DEBUGGEO
					Log._Debug($"NodeGeometry: Recalculating segment {SegmentEndGeometries[i].SegmentId} @ {NodeId}");
#endif
					SegmentEndGeometries[i].GetSegmentGeometry().Recalculate(false);
				}
			}
		}

		internal void Recalculate() {
#if DEBUGGEO
			Log._Debug($"NodeGeometry: Recalculate @ {NodeId}");
#endif

			Cleanup();

			Flags.applyNodeTrafficLightFlag(NodeId);

			// check if node is valid
			if (!IsValid()) {
				for (int i = 0; i < 8; ++i) {
					SegmentEndGeometries[i] = null;
				}
				TrafficLightSimulation.RemoveNodeFromSimulation(NodeId, false, true);
				Flags.setNodeTrafficLight(NodeId, false);
			} else {
				NetManager netManager = Singleton<NetManager>.instance;
				bool hasTrafficLight = (netManager.m_nodes.m_buffer[NodeId].m_flags & NetNode.Flags.TrafficLights) != NetNode.Flags.None;
				var nodeSim = TrafficLightSimulation.GetNodeSimulation(NodeId);
				if (nodeSim == null) {
					byte numSegmentsWithSigns = 0;
					for (var s = 0; s < 8; s++) {
						var segmentId = netManager.m_nodes.m_buffer[NodeId].GetSegment(s);
						if (segmentId <= 0)
							continue;

#if DEBUGx
						Log._Debug($"NodeGeometry.Recalculate: Housekeeping segment {segmentId}");
#endif

						SegmentEnd prioritySegment = TrafficPriority.GetPrioritySegment(NodeId, segmentId);
						if (prioritySegment == null) {
							continue;
						}

						// if node is a traffic light, it must not have priority signs
						if (hasTrafficLight && prioritySegment.Type != SegmentEnd.PriorityType.None) {
							Log.Warning($"Housekeeping: Node {NodeId}, Segment {segmentId} is a priority sign but node has a traffic light!");
							prioritySegment.Type = SegmentEnd.PriorityType.None;
						}

						// if a priority sign is set, everything is ok
						if (prioritySegment.Type != SegmentEnd.PriorityType.None) {
							++numSegmentsWithSigns;
						}
					}

					if (numSegmentsWithSigns > 0) {
						// add priority segments for newly created segments
						numSegmentsWithSigns += TrafficPriority.AddPriorityNode(NodeId);
					}
				}

				// calculate node properties
				byte incomingSegments = 0;
				byte outgoingSegments = 0;
				for (int i = 0; i < 8; ++i) {
					if (SegmentEndGeometries[i] == null)
						continue;
#if DEBUGGEO
					Log._Debug($"NodeGeometry.Recalculate: Iterating over segment end {SegmentEndGeometries[i].SegmentId} @ node {NodeId}");
#endif

					bool startNode = SegmentEndGeometries[i].StartNode;
					if (SegmentEndGeometries[i].GetSegmentGeometry().IsIncoming(startNode))
						++incomingSegments;
					if (SegmentEndGeometries[i].GetSegmentGeometry().IsOutgoing(startNode))
						++outgoingSegments;
				}

				IsSimpleJunction = incomingSegments == 1 || outgoingSegments == 1;
#if DEBUGGEO
				Log._Debug($"NodeGeometry.Recalculate: Node {NodeId} has {incomingSegments} incoming and {outgoingSegments} outgoing segments.");
#endif
			}

			NotifyObservers();
		}

		internal void NotifyObservers() {
			try {
				Monitor.Enter(Lock);
				List<IObserver<NodeGeometry>> myObservers = new List<IObserver<NodeGeometry>>(observers); // in case somebody unsubscribes while iterating over subscribers

				foreach (IObserver<NodeGeometry> observer in myObservers) {
					observer.OnUpdate(this);
				}
			} finally {
				Monitor.Exit(Lock);
			}
		}

		// static methods

		internal static void OnBeforeLoadData() {
			nodeGeometries = new NodeGeometry[NetManager.MAX_NODE_COUNT];
			Log._Debug($"Building {nodeGeometries.Length} node geometries...");
			for (ushort i = 0; i < nodeGeometries.Length; ++i) {
				nodeGeometries[i] = new NodeGeometry(i);
			}
			Log._Debug($"Built node geometries.");
		}

		public static NodeGeometry Get(ushort nodeId) {
			return nodeGeometries[nodeId];
		}
	}
}
