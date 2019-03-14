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
using TrafficManager.Traffic.Data;
using TrafficManager.Util;

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

		public void OnUpdateSegment(ref ExtSegment seg) {
			MarkAsUpdated(ref seg);
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
									ExtSegment seg = Constants.ManagerFactory.ExtSegmentManager.ExtSegments[segmentId];
									if (firstPass ^ !seg.valid) {
										if (! firstPass) {
											updatesMissing = true;
#if DEBUGGEO
											if (debug)
												Log.Warning($"GeometryManager.SimulationStep(): Detected invalid segment {segmentId} in second pass");
#endif
										}
										continue;
									}
#if DEBUGGEO
									if (debug)
										Log._Debug($"GeometryManager.SimulationStep(): Notifying observers about segment {segmentId}. Valid? {seg.valid} First pass? {firstPass}");
#endif
									NotifyObservers(new GeometryUpdate(ref seg));
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
									bool valid = Services.NetService.IsNodeValid(nodeId);
									if (firstPass ^ !valid) {
										if (!firstPass) {
											updatesMissing = true;
#if DEBUGGEO
											if (debug)
												Log.Warning($"GeometryManager.SimulationStep(): Detected invalid node {nodeId} in second pass");
#endif
										}
										continue;
									}
#if DEBUGGEO
									if (debug)
										Log._Debug($"GeometryManager.SimulationStep(): Notifying observers about node {nodeId}. Valid? {valid} First pass? {firstPass}");
#endif
									NotifyObservers(new GeometryUpdate(nodeId));
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

		public void MarkAsUpdated(ref ExtSegment seg) {
#if DEBUGGEO
			if (GlobalConfig.Instance.Debug.Switches[5])
				Log._Debug($"GeometryManager.MarkAsUpdated(segment {seg.segmentId}): Marking segment as updated");
#endif
			try {
				Monitor.Enter(updateLock);

				updatedSegmentBuckets[seg.segmentId >> 6] |= 1uL << (int)(seg.segmentId & 63);
				stateUpdated = true;

				MarkAsUpdated(Constants.ServiceFactory.NetService.GetSegmentNodeId(seg.segmentId, true));
				MarkAsUpdated(Constants.ServiceFactory.NetService.GetSegmentNodeId(seg.segmentId, false));

				if (! seg.valid) {
					SimulationStep(true);
				}
			} finally {
				Monitor.Exit(updateLock);
			}
		}

		public void MarkAsUpdated(ushort nodeId) {
#if DEBUGGEO
			if (GlobalConfig.Instance.Debug.Switches[5])
				Log._Debug($"GeometryManager.MarkAsUpdated(node {nodeId}): Marking node as updated");
#endif
			try {
				Monitor.Enter(updateLock);

				if (nodeId != 0) {
					updatedNodeBuckets[nodeId >> 6] |= 1uL << (int)(nodeId & 63);
					stateUpdated = true;

					if (! Services.NetService.IsNodeValid(nodeId)) {
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
