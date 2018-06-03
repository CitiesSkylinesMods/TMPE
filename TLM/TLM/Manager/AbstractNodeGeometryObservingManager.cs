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
		private IDisposable[] nodeGeometryUnsubscribers = new IDisposable[NetManager.MAX_NODE_COUNT];
		private object geoLock = new object();

		protected virtual bool AllowInvalidNodes { get; } = false;

		protected override void InternalPrintDebugInfo() {
			base.InternalPrintDebugInfo();
			Log._Debug($"Subscribed node geometries: {nodeGeometryUnsubscribers.Select(unsub => unsub != null).ToList().CollectionToString()}");
		}

		protected void UnsubscribeFromNodeGeometry(ushort nodeId) {
#if DEBUGGEO
			if (GlobalConfig.Instance.Debug.Switches[5])
				Log._Debug($"AbstractNodeGeometryObservingManager.UnsubscribeFromNodeGeometry({nodeId}) called.");
#endif

			try {
				Monitor.Enter(geoLock);

				InternalUnsubscribeFromNodeGeometry(nodeId);
#if DEBUGGEO
				if (GlobalConfig.Instance.Debug.Switches[5])
					Log._Debug($"AbstractNodeGeometryObservingManager.UnsubscribeFromNodeGeometry({nodeId}): watched nodes: {nodeGeometryUnsubscribers.Select(unsub => unsub != null).ToList().CollectionToString()}");
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

			try {
				Monitor.Enter(geoLock);

				for (int nodeId = 0; nodeId < NetManager.MAX_NODE_COUNT; ++nodeId) {
					InternalUnsubscribeFromNodeGeometry((ushort)nodeId);
				}
			} finally {
				Monitor.Exit(geoLock);
			}
		}

		private void InternalUnsubscribeFromNodeGeometry(ushort nodeId) {
			IDisposable unsubscriber = nodeGeometryUnsubscribers[nodeId];
			if (unsubscriber != null) {
				unsubscriber.Dispose();
				nodeGeometryUnsubscribers[nodeId] = null;
			}
		}

		protected void SubscribeToNodeGeometry(ushort nodeId) {
#if DEBUGGEO
			if (GlobalConfig.Instance.Debug.Switches[5])
				Log._Debug($"AbstractNodeGeometryObservingManager.SubscribeToNodeGeometry({nodeId}) called.");
#endif

			try {
				Monitor.Enter(geoLock);

				if (nodeGeometryUnsubscribers[nodeId] == null) {
					nodeGeometryUnsubscribers[nodeId] = NodeGeometry.Get(nodeId).Subscribe(this);
				}
			} finally {
				Monitor.Exit(geoLock);
			}

#if DEBUGGEO
			if (GlobalConfig.Instance.Debug.Switches[5])
				Log._Debug($"AbstractNodeGeometryObservingManager.SubscribeToNodeGeometry({nodeId}): watched nodes: {nodeGeometryUnsubscribers.Select(unsub => unsub != null).ToList().CollectionToString()}");
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
				if (!AllowInvalidNodes) {
					UnsubscribeFromNodeGeometry(geometry.NodeId);
				}
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
