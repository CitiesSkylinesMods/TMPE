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

namespace TrafficManager.Geometry.Impl {
	public class NodeGeometry : GenericObservable<NodeGeometry>, IEquatable<NodeGeometry> {
		public struct SegmentEndReplacement {
			public ISegmentEndId oldSegmentEndId;
			public ISegmentEndId newSegmentEndId;

			public override string ToString() {
				return $"[SegmentEndReplacement\n" +
					"\t" + $"oldSegmentEndId = {oldSegmentEndId}\n" +
					"\t" + $"newSegmentEndId = {newSegmentEndId}\n" +
					"SegmentEndReplacement]";
			}
		}

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
			get {
				return IncomingSegments == 1 || OutgoingSegments == 1;
			}
		}

		private ISegmentEndId lastRemovedSegmentEndId = null;

		public int IncomingSegments { get; private set; } = 0;
		public int OutgoingSegments { get; private set; } = 0;

		public SegmentEndReplacement CurrentSegmentReplacement = default(SegmentEndReplacement);

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
		//private List<IObserver<NodeGeometry>> observers = new List<IObserver<NodeGeometry>>();

		/// <summary>
		/// Lock object. Acquire this before accessing the HashSets.
		/// </summary>
		//public readonly object Lock = new object();

		public override string ToString() {
			return $"[NodeGeometry ({NodeId})\n" +
				"\t" + $"IsValid() = {IsValid()}\n" +
				"\t" + $"IsSimpleJunction = {IsSimpleJunction}\n" +
				"\t" + $"IncomingSegments = {IncomingSegments}\n" +
				"\t" + $"OutgoingSegments = {OutgoingSegments}\n" +
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
		/*public IDisposable Subscribe(IObserver<NodeGeometry> observer) {
			try {
				Monitor.Enter(Lock);
				observers.Add(observer);
			} finally {
				Monitor.Exit(Lock);
			}
			return new GenericUnsubscriber<NodeGeometry>(observers, observer, Lock);
		}*/
		
		internal void AddSegmentEnd(SegmentEndGeometry segEndGeo, GeometryCalculationMode calcMode) {
#if DEBUGGEO
			if (GlobalConfig.Instance.Debug.Switches[5])
				Log._Debug($">>> NodeGeometry: Add segment end {segEndGeo.SegmentId}, start? {segEndGeo.StartNode} @ node {NodeId}");
#endif
			if (!IsValid()) {
				//Log.Error($"NodeGeometry: Trying to add segment {segmentId} @ invalid node {NodeId}");
				Invalidate();
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

			if (!found && lastRemovedSegmentEndId != null) {
				CurrentSegmentReplacement.oldSegmentEndId = lastRemovedSegmentEndId;
				CurrentSegmentReplacement.newSegmentEndId = segEndGeo;
				lastRemovedSegmentEndId = null;
			}
			Recalculate();
		}

		internal void RemoveSegmentEnd(SegmentEndGeometry segmentEndGeo, GeometryCalculationMode calcMode) {
#if DEBUGGEO
			if (GlobalConfig.Instance.Debug.Switches[5])
				Log._Debug($">>> NodeGeometry: Remove segment end {segmentEndGeo.SegmentId} @ {NodeId}, calcMode? {calcMode}");
#endif

			if (calcMode == GeometryCalculationMode.Init) {
				return;
			}

			if (!IsValid()) {
				//Log.Warning($"NodeGeometry: Trying to remove segment {segmentId} @ invalid node {NodeId}");
				Invalidate();
				return;
			}

			for (int i = 0; i < MAX_NUM_SEGMENTS; ++i) {
				if (segmentEndGeo.Equals(SegmentEndGeometries[i])) {
					SegmentEndGeometries[i] = null;
					lastRemovedSegmentEndId = segmentEndGeo;
				}
			}

			if (calcMode == GeometryCalculationMode.Propagate) {
				RecalculateSegments(segmentEndGeo.SegmentId);
			}
			Recalculate();
		}

		private void Cleanup() {
			IncomingSegments = 0;
			OutgoingSegments = 0;
			NumSegmentEnds = 0;
		}

		internal void RecalculateSegments(ushort? ignoreSegmentId= null) {
#if DEBUGGEO
			if (GlobalConfig.Instance.Debug.Switches[5])
				Log._Debug($"NodeGeometry: Propagate @ {NodeId}. ignoreSegmentId={ignoreSegmentId}");
#endif

			// recalculate (other) segments
			for (int i = 0; i < MAX_NUM_SEGMENTS; ++i) {
				if (SegmentEndGeometries[i] == null)
					continue;
				if (ignoreSegmentId != null && SegmentEndGeometries[i].SegmentId == ignoreSegmentId)
					continue;
#if DEBUGGEO
				if (GlobalConfig.Instance.Debug.Switches[5])
					Log._Debug($"NodeGeometry: Recalculating segment {SegmentEndGeometries[i].SegmentId} @ {NodeId}");
#endif
				SegmentEndGeometries[i].GetSegmentGeometry(true).StartRecalculation(GeometryCalculationMode.NoPropagate);
			}
		}

		internal void Recalculate() {
#if DEBUGGEO
			if (GlobalConfig.Instance.Debug.Switches[5])
				Log._Debug($">>> NodeGeometry: Recalculate @ {NodeId}");
#endif

			Cleanup();

			// check if node is valid
			if (!IsValid()) {
				Invalidate();
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
					if (GlobalConfig.Instance.Debug.Switches[5])
						Log._Debug($"NodeGeometry.Recalculate: Iterating over segment end {SegmentEndGeometries[i].SegmentId} @ node {NodeId}");
#endif

					bool startNode = SegmentEndGeometries[i].StartNode;
					if (SegmentEndGeometries[i].GetSegmentGeometry(true).IsIncoming(startNode))
						++incomingSegments;
					if (SegmentEndGeometries[i].GetSegmentGeometry(true).IsOutgoing(startNode))
						++outgoingSegments;
				}

				IncomingSegments = incomingSegments;
				OutgoingSegments = outgoingSegments;
#if DEBUGGEO
				if (GlobalConfig.Instance.Debug.Switches[5])
					Log._Debug($"NodeGeometry.Recalculate: Node {NodeId} has {incomingSegments} incoming and {outgoingSegments} outgoing segments.");
#endif
				NotifyObservers();
			}
		}

		protected void Invalidate() {
			for (int i = 0; i < MAX_NUM_SEGMENTS; ++i) {
				SegmentEndGeometries[i] = null;
			}
			lastRemovedSegmentEndId = null;
			CurrentSegmentReplacement = default(SegmentEndReplacement);
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

		public override void NotifyObservers() {
			//Log.Warning($"NodeGeometry.NotifyObservers(): CurrentSegmentReplacement={CurrentSegmentReplacement}");

			/*List<IObserver<NodeGeometry>> myObservers = new List<IObserver<NodeGeometry>>(observers); // in case somebody unsubscribes while iterating over subscribers
			foreach (IObserver<NodeGeometry> observer in myObservers) {
				try {
					observer.OnUpdate(this);
				} catch (Exception e) {
					Log.Error($"SegmentGeometry.NotifyObservers: An exception occured while notifying an observer of node {NodeId}: {e}");
				}
			}*/

			base.NotifyObservers();

			CurrentSegmentReplacement.oldSegmentEndId = null;
			CurrentSegmentReplacement.newSegmentEndId = null;
		}

		// static methods

		internal static void OnBeforeLoadData() {
			nodeGeometries = new NodeGeometry[NetManager.MAX_NODE_COUNT];
#if DEBUGGEO
			Log._Debug($"Building {nodeGeometries.Length} node geometries...");
#endif
			for (int i = 0; i < nodeGeometries.Length; ++i) {
				nodeGeometries[i] = new NodeGeometry((ushort)i);
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
