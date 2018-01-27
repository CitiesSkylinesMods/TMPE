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

namespace TrafficManager.Manager {
	public abstract class AbstractNodeGeometryObservingManager : AbstractCustomManager, IObserver<NodeGeometry> {
		private IDictionary<ushort, IDisposable> nodeGeometryUnsubscribers = new Dictionary<ushort, IDisposable>();
		private object geoLock = new object();

		protected override void InternalPrintDebugInfo() {
			base.InternalPrintDebugInfo();
			Log._Debug($"Subscribed node geometries: {nodeGeometryUnsubscribers.Keys.CollectionToString()}");
		}

		protected void UnsubscribeFromNodeGeometry(ushort nodeId) {
#if DEBUGGEO
			if (GlobalConfig.Instance.Debug.Switches[5])
				Log._Debug($"AbstractNodeGeometryObservingManager.UnsubscribeFromNodeGeometry({nodeId}) called.");
#endif

			try {
				Monitor.Enter(geoLock);

				IDisposable unsubscriber;
				if (nodeGeometryUnsubscribers.TryGetValue(nodeId, out unsubscriber)) {
					unsubscriber.Dispose();
					nodeGeometryUnsubscribers.Remove(nodeId);
				}
#if DEBUGGEO
				if (GlobalConfig.Instance.Debug.Switches[5])
					Log._Debug($"AbstractNodeGeometryObservingManager.UnsubscribeFromNodeGeometry({nodeId}): watched nodes: {String.Join(",", nodeGeometryUnsubscribers.Keys.Select(x => x.ToString()).ToArray())}");
#endif
			} finally {
				Monitor.Exit(geoLock);
			}
		}

		protected void UnsubscribeFromAllNodeGeometries() {
#if DEBUGGEO
			if (GlobalConfig.Instance.Debug.Switches[5])
				Log._Debug($"AbstractNodeGeometryObservingManager.UnsubscribeFromAllNodeGeometries() called.");
#endif

			List<ushort> nodeIds = new List<ushort>(nodeGeometryUnsubscribers.Keys);
			foreach (ushort nodeId in nodeIds)
				UnsubscribeFromNodeGeometry(nodeId);
		}

		protected void SubscribeToNodeGeometry(ushort nodeId) {
#if DEBUGGEO
			if (GlobalConfig.Instance.Debug.Switches[5])
				Log._Debug($"AbstractNodeGeometryObservingManager.SubscribeToNodeGeometry({nodeId}) called.");
#endif

			try {
				Monitor.Enter(geoLock);

				if (!nodeGeometryUnsubscribers.ContainsKey(nodeId)) {
					nodeGeometryUnsubscribers.Add(nodeId, NodeGeometry.Get(nodeId).Subscribe(this));
				}
			} finally {
				Monitor.Exit(geoLock);
			}

#if DEBUGGEO
			if (GlobalConfig.Instance.Debug.Switches[5])
				Log._Debug($"AbstractNodeGeometryObservingManager.SubscribeToNodeGeometry({nodeId}): watched nodes: {String.Join(",", nodeGeometryUnsubscribers.Keys.Select(x => x.ToString()).ToArray())}");
#endif
		}

		public override void OnLevelUnloading() {
			UnsubscribeFromAllNodeGeometries();
		}

		protected abstract void HandleInvalidNode(NodeGeometry geometry);
		protected abstract void HandleValidNode(NodeGeometry geometry);

		public void OnUpdate(IObservable<NodeGeometry> observable) {
			NodeGeometry geometry = (NodeGeometry)observable;
			if (!geometry.IsValid()) {
#if DEBUGGEO
				if (GlobalConfig.Instance.Debug.Switches[5])
					Log._Debug($"{this.GetType().Name}.HandleInvalidNode({geometry.NodeId})");
#endif
				HandleInvalidNode(geometry);
				UnsubscribeFromNodeGeometry(geometry.NodeId);
			} else {
#if DEBUGGEO
				if (GlobalConfig.Instance.Debug.Switches[5])
					Log._Debug($"{this.GetType().Name}.HandleValidNode({geometry.NodeId})");
#endif
				HandleValidNode(geometry);
			}
		}

		~AbstractNodeGeometryObservingManager() {
#if DEBUGGEO
			if (GlobalConfig.Instance.Debug.Switches[5])
				Log._Debug($"~AbstractNodeGeometryObservingManager() called.");
#endif
			UnsubscribeFromAllNodeGeometries();
		}
	}
}
