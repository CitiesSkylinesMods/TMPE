using ColossalFramework;
using CSUtil.Commons;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using TrafficManager.Manager;
using TrafficManager.State;
using TrafficManager.Traffic;
using TrafficManager.Traffic.Enums;
using TrafficManager.TrafficLight;
using TrafficManager.Util;

namespace TrafficManager.Geometry.Impl {
	public class NodeGeometry : INodeGeometry, IEquatable<NodeGeometry> {
		private const byte MAX_NUM_SEGMENTS = 8;

		private static NodeGeometry[] nodeGeometries;

		public static void PrintDebugInfo() {
			string buf = 
			"-----------------------\n" +
			"--- NODE GEOMETRIES ---\n" +
			"-----------------------";
			buf += $"Total: {nodeGeometries.Length}\n";
			foreach (NodeGeometry nodeGeo in nodeGeometries) {
				if (nodeGeo.Valid) {
					buf += nodeGeo.ToString() + "\n" +
					"-------------------------\n";
				}
			}
			Log.Info(buf);
		}

		public ushort NodeId {
			get; private set;
		} = 0;

		public bool SimpleJunction {
			get {
				return NumIncomingSegments == 1 || NumOutgoingSegments == 1;
			}
		}

		private ISegmentEndId lastRemovedSegmentEndId = null;

		public int NumIncomingSegments { get; private set; } = 0;
		public int NumOutgoingSegments { get; private set; } = 0;

		public SegmentEndReplacement CurrentSegmentReplacement = default(SegmentEndReplacement);

		/// <summary>
		/// Connected segment end geometries.
		/// WARNING: Individual entries may be null
		/// </summary>
		public ISegmentEndGeometry[] SegmentEndGeometries {
			get; private set;
		} = new SegmentEndGeometry[MAX_NUM_SEGMENTS];

		public byte NumSegmentEnds { get; private set; } = 0;

		public override string ToString() {
			return $"[NodeGeometry ({NodeId})\n" +
				"\t" + $"IsValid() = {Valid}\n" +
				"\t" + $"IsSimpleJunction = {SimpleJunction}\n" +
				"\t" + $"IncomingSegments = {NumIncomingSegments}\n" +
				"\t" + $"OutgoingSegments = {NumOutgoingSegments}\n" +
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

		public bool Valid {
			get {
				return Constants.ServiceFactory.NetService.IsNodeValid(NodeId);
			}
		}
		
		public void AddSegmentEnd(ISegmentEndGeometry segEndGeo, GeometryCalculationMode calcMode) {
#if DEBUGGEO
			if (GlobalConfig.Instance.Debug.Switches[5])
				Log._Debug($">>> NodeGeometry: Add segment end {segEndGeo.SegmentId}, start? {segEndGeo.StartNode} @ node {NodeId}");
#endif
			if (!Valid) {
				//Log.Error($"NodeGeometry: Trying to add segment {segmentId} @ invalid node {NodeId}");
				Invalidate();
				return;
			}

			bool found = false;
			int freeIndex = -1;
			for (int i = 0; i < MAX_NUM_SEGMENTS; ++i) {
				ISegmentEndGeometry storedEndGeo = SegmentEndGeometries[i];
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

		public void RemoveSegmentEnd(ISegmentEndGeometry segmentEndGeo, GeometryCalculationMode calcMode) {
#if DEBUGGEO
			if (GlobalConfig.Instance.Debug.Switches[5])
				Log._Debug($">>> NodeGeometry: Remove segment end {segmentEndGeo.SegmentId} @ {NodeId}, calcMode? {calcMode}");
#endif

			if (calcMode == GeometryCalculationMode.Init) {
				return;
			}

			if (!Valid) {
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
			NumIncomingSegments = 0;
			NumOutgoingSegments = 0;
			NumSegmentEnds = 0;
		}

		private void RecalculateSegments(ushort? ignoreSegmentId= null) {
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
				SegmentGeometry.Get(SegmentEndGeometries[i].SegmentId, true).StartRecalculation(GeometryCalculationMode.NoPropagate);
			}
		}

		public void Recalculate() {
#if DEBUGGEO
			if (GlobalConfig.Instance.Debug.Switches[5])
				Log._Debug($">>> NodeGeometry: Recalculate @ {NodeId}");
#endif

			Cleanup();

			// check if node is valid
			if (!Valid) {
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

					if (SegmentEndGeometries[i].Incoming)
						++incomingSegments;
					if (SegmentEndGeometries[i].Outgoing)
						++outgoingSegments;
				}

				NumIncomingSegments = incomingSegments;
				NumOutgoingSegments = outgoingSegments;
#if DEBUGGEO
				if (GlobalConfig.Instance.Debug.Switches[5])
					Log._Debug($"NodeGeometry.Recalculate: Node {NodeId} has {incomingSegments} incoming and {outgoingSegments} outgoing segments.");
#endif
				NotifyGeomentryManager();
			}
		}

		protected void Invalidate() {
			for (int i = 0; i < MAX_NUM_SEGMENTS; ++i) {
				SegmentEndGeometries[i] = null;
			}
			lastRemovedSegmentEndId = null;
			CurrentSegmentReplacement = default(SegmentEndReplacement);
			NotifyGeomentryManager();
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

		private void NotifyGeomentryManager() {
			if (CurrentSegmentReplacement.IsDefined()) {
				Constants.ManagerFactory.GeometryManager.OnSegmentEndReplacement(CurrentSegmentReplacement);
			}

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
