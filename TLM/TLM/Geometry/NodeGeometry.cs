using ColossalFramework;
using CSUtil.Commons;
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
	public class NodeGeometry : IObservable<NodeGeometry>, IEquatable<NodeGeometry> {
		private const byte MAX_NUM_SEGMENTS = 8;

		private static NodeGeometry[] nodeGeometries;

		public static void PrintDebugInfo() {
			string buf = 
			"-----------------------\n" +
			"--- NODE GEOMETRIES ---\n" +
			"-----------------------";
			buf += $"Total: {nodeGeometries.Length}\n";
			foreach (NodeGeometry nodeGeo in nodeGeometries) {
				if (nodeGeo.IsValid()) {
					buf += nodeGeo.ToString() + "\n" +
					"-------------------------\n";
				}
			}
			Log.Info(buf);
		}

		public ushort NodeId {
			get; private set;
		} = 0;

		public bool IsSimpleJunction {
			get; private set;
		} = false;

		/// <summary>
		/// Connected segment end geometries.
		/// WARNING: Individual entries may be null
		/// </summary>
		public SegmentEndGeometry[] SegmentEndGeometries {
			get; private set;
		} = new SegmentEndGeometry[MAX_NUM_SEGMENTS];

		public byte NumSegmentEnds { get; private set; } = 0;

		/// <summary>
		/// Holds a list of observers which are being notified as soon as the managed node's geometry is updated (but not neccessarily modified)
		/// </summary>
		private List<IObserver<NodeGeometry>> observers = new List<IObserver<NodeGeometry>>();

		/// <summary>
		/// Lock object. Acquire this before accessing the HashSets.
		/// </summary>
		public readonly object Lock = new object();

		public override string ToString() {
			return $"[NodeGeometry ({NodeId})\n" +
				"\t" + $"IsValid() = {IsValid()}\n" +
				"\t" + $"IsSimpleJunction = {IsSimpleJunction}\n" +
				"\t" + $"SegmentEndGeometries = {SegmentEndGeometries.ArrayToString()}\n" +
				"\t" + $"NumSegmentEnds = {NumSegmentEnds}\n" +
				"NodeGeometry]";
		}

		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="nodeId">id of the managed node</param>
		public NodeGeometry(ushort nodeId) {
			this.NodeId = nodeId;
		}

		public bool IsValid() {
			return Constants.ServiceFactory.NetService.IsNodeValid(NodeId);
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
		
		internal void AddSegmentEnd(SegmentEndGeometry segEndGeo, GeometryCalculationMode calcMode) {
#if DEBUGGEO
			if (GlobalConfig.Instance.DebugSwitches[5])
				Log._Debug($">>> NodeGeometry: Add segment end {segEndGeo.SegmentId}, start? {segEndGeo.StartNode} @ node {NodeId}");
#endif
			if (!IsValid()) {
				//Log.Error($"NodeGeometry: Trying to add segment {segmentId} @ invalid node {NodeId}");
				RemoveAllSegmentEnds();
				return;
			}

			bool found = false;
			int freeIndex = -1;
			for (int i = 0; i < MAX_NUM_SEGMENTS; ++i) {
				SegmentEndGeometry storedEndGeo = SegmentEndGeometries[i];
				if (segEndGeo.Equals(storedEndGeo)) {
					SegmentEndGeometries[i] = segEndGeo;
					found = true;
					break;
				} else if (storedEndGeo == null && freeIndex < 0) {
					freeIndex = i;
				}
			}

			if (!found) {
				if (freeIndex >= 0) {
					SegmentEndGeometries[freeIndex] = segEndGeo;
				} else {
					Log.Error($"NodeGeometry.AddSegmentEnd: Detected inconsistency. Unable to add segment end {segEndGeo} to node {NodeId}. Maximum segment end capacity reached.");
				}
			}

			if (calcMode == GeometryCalculationMode.Propagate) {
				RecalculateSegments(segEndGeo.SegmentId);
			}
			Recalculate();
		}

		internal void RemoveSegmentEnd(SegmentEndGeometry segmentEndGeo, GeometryCalculationMode calcMode) {
#if DEBUGGEO
			if (GlobalConfig.Instance.DebugSwitches[5])
				Log._Debug($">>> NodeGeometry: Remove segment end {segmentEndGeo.SegmentId} @ {NodeId}, calcMode? {calcMode}");
#endif

			if (calcMode == GeometryCalculationMode.Init) {
				return;
			}

			if (!IsValid()) {
				//Log.Warning($"NodeGeometry: Trying to remove segment {segmentId} @ invalid node {NodeId}");
				RemoveAllSegmentEnds();
				return;
			}

			for (int i = 0; i < MAX_NUM_SEGMENTS; ++i) {
				if (segmentEndGeo.Equals(SegmentEndGeometries[i])) {
					SegmentEndGeometries[i] = null;
				}
			}

			if (calcMode == GeometryCalculationMode.Propagate) {
				RecalculateSegments(segmentEndGeo.SegmentId);
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
			for (int i = 0; i < MAX_NUM_SEGMENTS; ++i) {
				if (SegmentEndGeometries[i] == null)
					continue;
				if (ignoreSegmentId != null && SegmentEndGeometries[i].SegmentId == ignoreSegmentId)
					continue;
#if DEBUGGEO
				if (GlobalConfig.Instance.DebugSwitches[5])
					Log._Debug($"NodeGeometry: Recalculating segment {SegmentEndGeometries[i].SegmentId} @ {NodeId}");
#endif
				SegmentEndGeometries[i].GetSegmentGeometry(true).StartRecalculation(GeometryCalculationMode.NoPropagate);
			}
		}

		internal void Recalculate() {
#if DEBUGGEO
			if (GlobalConfig.Instance.DebugSwitches[5])
				Log._Debug($">>> NodeGeometry: Recalculate @ {NodeId}");
#endif

			Cleanup();

			// check if node is valid
			if (!IsValid()) {
				RemoveAllSegmentEnds();
				return;
			} else {
				// calculate node properties
				byte incomingSegments = 0;
				byte outgoingSegments = 0;
				for (int i = 0; i < MAX_NUM_SEGMENTS; ++i) {
					if (SegmentEndGeometries[i] == null)
						continue;
					++NumSegmentEnds;
#if DEBUGGEO
					if (GlobalConfig.Instance.DebugSwitches[5])
						Log._Debug($"NodeGeometry.Recalculate: Iterating over segment end {SegmentEndGeometries[i].SegmentId} @ node {NodeId}");
#endif

					bool startNode = SegmentEndGeometries[i].StartNode;
					if (SegmentEndGeometries[i].GetSegmentGeometry(true).IsIncoming(startNode))
						++incomingSegments;
					if (SegmentEndGeometries[i].GetSegmentGeometry(true).IsOutgoing(startNode))
						++outgoingSegments;
				}

				IsSimpleJunction = incomingSegments == 1 || outgoingSegments == 1;
#if DEBUGGEO
				if (GlobalConfig.Instance.DebugSwitches[5])
					Log._Debug($"NodeGeometry.Recalculate: Node {NodeId} has {incomingSegments} incoming and {outgoingSegments} outgoing segments.");
#endif
				NotifyObservers();
			}
		}

		protected void RemoveAllSegmentEnds() {
			for (int i = 0; i < MAX_NUM_SEGMENTS; ++i) {
				SegmentEndGeometries[i] = null;
			}
			NotifyObservers();
		}

		public bool Equals(NodeGeometry otherNodeGeo) {
			if (otherNodeGeo == null) {
				return false;
			}
			return NodeId == otherNodeGeo.NodeId;
		}

		public override bool Equals(object other) {
			if (other == null) {
				return false;
			}
			if (!(other is NodeGeometry)) {
				return false;
			}
			return Equals((NodeGeometry)other);
		}

		public override int GetHashCode() {
			int prime = 31;
			int result = 1;
			result = prime * result + NodeId.GetHashCode();
			return result;
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
			if (nodeGeometries == null) {
				return null;
			}
			return nodeGeometries[nodeId];
		}
	}
}
