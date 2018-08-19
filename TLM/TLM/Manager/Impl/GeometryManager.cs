using ColossalFramework;
using CSUtil.Commons;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using TrafficManager.Geometry;
using TrafficManager.Geometry.Impl;
using TrafficManager.State;
using TrafficManager.Util;
using static TrafficManager.Geometry.Impl.NodeGeometry;

namespace TrafficManager.Manager.Impl {
	public class GeometryManager : AbstractCustomManager, IGeometryManager {
		public static GeometryManager Instance { get; private set; } = new GeometryManager();

		public class GeometryUpdateObservable : GenericObservable<GeometryUpdate> {

		}

		private bool stateUpdated;
		private ulong[] updatedSegmentBuckets;
		private ulong[] updatedNodeBuckets;
		private object updateLock;
		private Queue<SegmentEndReplacement> segmentReplacements;
		private GeometryUpdateObservable geometryUpdateObservable;

		private GeometryManager() {
			stateUpdated = false;
			updatedSegmentBuckets = new ulong[576];
			updatedNodeBuckets = new ulong[512];
			updateLock = new object();
			segmentReplacements = new Queue<SegmentEndReplacement>();
			geometryUpdateObservable = new GeometryUpdateObservable();
		}

		public override void OnBeforeLoadData() {
			base.OnBeforeLoadData();
			segmentReplacements.Clear();
			SimulationStep();
		}

		public void OnUpdateSegment(ISegmentGeometry geo) {
			MarkAsUpdated(geo);
		}

		public void SimulationStep(bool onlyFirstPass=false) {
#if DEBUGGEO
			bool debug = GlobalConfig.Instance.Debug.Switches[5];
#endif
			if (!stateUpdated) {
				return;
			}

			NetManager netManager = Singleton<NetManager>.instance;
			if (!onlyFirstPass && (netManager.m_segmentsUpdated || netManager.m_nodesUpdated)) { // TODO maybe refactor NetManager use (however this could influence performance)
#if DEBUGGEO
				if (debug)
					Log._Debug($"GeometryManager.SimulationStep(): Skipping! stateUpdated={stateUpdated}, m_segmentsUpdated={netManager.m_segmentsUpdated}, m_nodesUpdated={netManager.m_nodesUpdated}");
#endif
				return;
			}

			try {
				Monitor.Enter(updateLock);

				bool updatesMissing = onlyFirstPass;
				for (int pass = 0; pass < (onlyFirstPass ? 1 : 2); ++pass) {
					bool firstPass = pass == 0;

					int len = updatedSegmentBuckets.Length;
					for (int i = 0; i < len; i++) {
						ulong segMask = updatedSegmentBuckets[i];
						if (segMask != 0uL) {
							for (int m = 0; m < 64; m++) {
								if ((segMask & 1uL << m) != 0uL) {
									ushort segmentId = (ushort)(i << 6 | m);
									SegmentGeometry segmentGeometry = SegmentGeometry.Get(segmentId, true);
									if (firstPass ^ !segmentGeometry.Valid) {
										if (! firstPass) {
											updatesMissing = true;
#if DEBUGGEO
											if (debug)
												Log.Warning($"GeometryManager.SimulationStep(): Detected invalid segment {segmentGeometry.SegmentId} in second pass");
#endif
										}
										continue;
									}
#if DEBUGGEO
									if (debug)
										Log._Debug($"GeometryManager.SimulationStep(): Notifying observers about segment {segmentGeometry.SegmentId}. Valid? {segmentGeometry.Valid} First pass? {firstPass}");
#endif
									NotifyObservers(new GeometryUpdate(segmentGeometry));
									updatedSegmentBuckets[i] &= ~(1uL << m);
								}
							}
						}
					}

					len = updatedNodeBuckets.Length;
					for (int i = 0; i < len; i++) {
						ulong nodeMask = updatedNodeBuckets[i];
						if (nodeMask != 0uL) {
							for (int m = 0; m < 64; m++) {
								if ((nodeMask & 1uL << m) != 0uL) {
									ushort nodeId = (ushort)(i << 6 | m);
									NodeGeometry nodeGeometry = NodeGeometry.Get(nodeId);
									if (firstPass ^ !nodeGeometry.Valid) {
										if (!firstPass) {
											updatesMissing = true;
#if DEBUGGEO
											if (debug)
												Log.Warning($"GeometryManager.SimulationStep(): Detected invalid node {nodeGeometry.NodeId} in second pass");
#endif
										}
										continue;
									}
#if DEBUGGEO
									if (debug)
										Log._Debug($"GeometryManager.SimulationStep(): Notifying observers about node {nodeGeometry.NodeId}. Valid? {nodeGeometry.Valid} First pass? {firstPass}");
#endif
									NotifyObservers(new GeometryUpdate(nodeGeometry));
									updatedNodeBuckets[i] &= ~(1uL << m);
								}
							}
						}
					}
				}

				if (! updatesMissing) {
					while (segmentReplacements.Count > 0) {
						SegmentEndReplacement replacement = segmentReplacements.Dequeue();
#if DEBUGGEO
						if (debug)
							Log._Debug($"GeometryManager.SimulationStep(): Notifying observers about segment end replacement {replacement}");
#endif
						NotifyObservers(new GeometryUpdate(replacement));
					}

					stateUpdated = false;
				}
			} finally {
				Monitor.Exit(updateLock);
			}
		}

		public void MarkAsUpdated(ISegmentGeometry geometry) {
#if DEBUGGEO
			if (GlobalConfig.Instance.Debug.Switches[5])
				Log._Debug($"GeometryManager.MarkAsUpdated(segment {geometry.SegmentId}): Marking segment as updated");
#endif
			try {
				Monitor.Enter(updateLock);

				ushort segmentId = geometry.SegmentId;

				updatedSegmentBuckets[segmentId >> 6] |= 1uL << (int)(segmentId & 63);
				stateUpdated = true;

				MarkAsUpdated(NodeGeometry.Get(geometry.StartNodeId));
				MarkAsUpdated(NodeGeometry.Get(geometry.EndNodeId));

				if (! geometry.Valid) {
					SimulationStep(true);
				}
			} finally {
				Monitor.Exit(updateLock);
			}
		}

		public void MarkAsUpdated(INodeGeometry geometry) {
#if DEBUGGEO
			if (GlobalConfig.Instance.Debug.Switches[5])
				Log._Debug($"GeometryManager.MarkAsUpdated(node {geometry.NodeId}): Marking node as updated");
#endif
			try {
				Monitor.Enter(updateLock);

				ushort nodeId = geometry.NodeId;
				if (nodeId != 0) {
					updatedNodeBuckets[nodeId >> 6] |= 1uL << (int)(nodeId & 63);
					stateUpdated = true;

					if (!geometry.Valid) {
						SimulationStep(true);
					}
				}
			} finally {
				Monitor.Exit(updateLock);
			}
		}

		public void OnSegmentEndReplacement(SegmentEndReplacement replacement) {
#if DEBUGGEO
			if (GlobalConfig.Instance.Debug.Switches[5])
				Log._Debug($"GeometryManager.OnSegmentEndReplacement(): Detected segment replacement: {replacement.oldSegmentEndId.SegmentId} -> {replacement.newSegmentEndId.SegmentId}");
#endif
			try {
				Monitor.Enter(updateLock);

				segmentReplacements.Enqueue(replacement);
				stateUpdated = true;
			} finally {
				Monitor.Exit(updateLock);
			}
		}

		public IDisposable Subscribe(IObserver<GeometryUpdate> observer) {
#if DEBUGGEO
			if (GlobalConfig.Instance.Debug.Switches[5])
				Log._Debug($"GeometryManager.Subscribe(): Subscribing observer {observer.GetType().Name}");
#endif
			return geometryUpdateObservable.Subscribe(observer);
		}

		protected void NotifyObservers(GeometryUpdate geometryUpdate) {
			geometryUpdateObservable.NotifyObservers(geometryUpdate);
		}
	}
}
