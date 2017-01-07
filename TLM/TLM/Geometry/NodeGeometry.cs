#define DEBUGGEOx

using ColossalFramework;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using TrafficManager.Manager;
using TrafficManager.State;
using TrafficManager.Traffic;
using TrafficManager.TrafficLight;
using TrafficManager.Util;

namespace TrafficManager.Geometry {
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

		public byte NumSegmentEnds { get; private set; } = 0;

		public bool NeedsRecalculation { get; private set; } = false; // TODO actually use this flag

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
			if (GlobalConfig.Instance.DebugSwitches[5])
				Log._Debug($"NodeGeometry: Add segment {segmentId}, start? {startNode} @ node {NodeId}");
#endif
			if (!IsValid()) {
				//Log.Error($"NodeGeometry: Trying to add segment {segmentId} @ invalid node {NodeId}");
				Recalculate();
				return;
			}

			for (int i = 0; i < 8; ++i) {
				if (SegmentEndGeometries[i]?.SegmentId == segmentId) {
					SegmentEndGeometries[i] = null;
				}
			}

			for (int i = 0; i < 8; ++i) {
				if (SegmentEndGeometries[i] == null) {
					SegmentEndGeometries[i] = startNode ? SegmentGeometry.Get(segmentId).StartNodeGeometry : SegmentGeometry.Get(segmentId).EndNodeGeometry;
					break;
				}
			}

			if (propagate) {
				NeedsRecalculation = true;
				try {
					RecalculateSegments(segmentId);
				} finally {
					NeedsRecalculation = false;
				}
			}
			Recalculate();
		}

		internal void RemoveSegment(ushort segmentId, bool propagate) {
#if DEBUGGEO
			if (GlobalConfig.Instance.DebugSwitches[5])
				Log._Debug($"NodeGeometry: Remove segment {segmentId} @ node {NodeId}, propagate? {propagate}");
#endif
			if (!IsValid()) {
				//Log.Warning($"NodeGeometry: Trying to remove segment {segmentId} @ invalid node {NodeId}");
				Recalculate();
				return;
			}

			for (int i = 0; i < 8; ++i) {
				if (SegmentEndGeometries[i]?.SegmentId == segmentId) {
					SegmentEndGeometries[i] = null;
				}
			}

			if (propagate) {
				NeedsRecalculation = true;
				try {
					RecalculateSegments(segmentId);
				} finally {
					NeedsRecalculation = false;
				}
			}
			Recalculate();
		}

		private void Cleanup() {
			IsSimpleJunction = false;
			NumSegmentEnds = 0;
		}

		internal void RecalculateSegments(ushort? ignoreSegmentId= null) {
#if DEBUGGEO
			if (GlobalConfig.Instance.DebugSwitches[5])
				Log._Debug($"NodeGeometry: Propagate @ {NodeId}. ignoreSegmentId={ignoreSegmentId}");
#endif

			// recalculate (other) segments
			for (int i = 0; i < 8; ++i) {
				if (SegmentEndGeometries[i] == null)
					continue;
				if (ignoreSegmentId != null && SegmentEndGeometries[i].SegmentId == ignoreSegmentId)
					continue;
#if DEBUGGEO
				if (GlobalConfig.Instance.DebugSwitches[5])
					Log._Debug($"NodeGeometry: Recalculating segment {SegmentEndGeometries[i].SegmentId} @ {NodeId}");
#endif
				SegmentEndGeometries[i].GetSegmentGeometry().Recalculate(false);
			}
		}

		internal void Recalculate() {
#if DEBUGGEO
			if (GlobalConfig.Instance.DebugSwitches[5])
				Log._Debug($"NodeGeometry: Recalculate @ {NodeId}");
#endif

			Cleanup();

			// check if node is valid
			if (!IsValid()) {
				for (int i = 0; i < 8; ++i) {
					SegmentEndGeometries[i] = null;
				}
			} else {
				// calculate node properties
				byte incomingSegments = 0;
				byte outgoingSegments = 0;
				for (int i = 0; i < 8; ++i) {
					if (SegmentEndGeometries[i] == null)
						continue;
					++NumSegmentEnds;
#if DEBUGGEO
					if (GlobalConfig.Instance.DebugSwitches[5])
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
				if (GlobalConfig.Instance.DebugSwitches[5])
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
#if DEBUGGEO
			Log._Debug($"Building {nodeGeometries.Length} node geometries...");
#endif
			for (ushort i = 0; i < nodeGeometries.Length; ++i) {
				nodeGeometries[i] = new NodeGeometry(i);
			}
#if DEBUGGEO
			Log._Debug($"Built node geometries.");
#endif
		}

		public static NodeGeometry Get(ushort nodeId) {
			return nodeGeometries[nodeId];
		}
	}
}
