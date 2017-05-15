using CSUtil.Commons;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using TrafficManager.Geometry;
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
			try {
				Monitor.Enter(geoLock);

				IDisposable unsubscriber;
				if (nodeGeometryUnsubscribers.TryGetValue(nodeId, out unsubscriber)) {
					unsubscriber.Dispose();
					nodeGeometryUnsubscribers.Remove(nodeId);
				}
			} finally {
				Monitor.Exit(geoLock);
			}
		}

		protected void UnsubscribeFromAllNodeGeometries() {
			List<ushort> nodeIds = new List<ushort>(nodeGeometryUnsubscribers.Keys);
			foreach (ushort nodeId in nodeIds)
				UnsubscribeFromNodeGeometry(nodeId);
		}

		protected void SubscribeToNodeGeometry(ushort nodeId) {
			try {
				Monitor.Enter(geoLock);

				if (!nodeGeometryUnsubscribers.ContainsKey(nodeId)) {
					nodeGeometryUnsubscribers.Add(nodeId, NodeGeometry.Get(nodeId).Subscribe(this));
				}
			} finally {
				Monitor.Exit(geoLock);
			}
		}

		public override void OnLevelUnloading() {
			UnsubscribeFromAllNodeGeometries();
		}

		protected abstract void HandleInvalidNode(NodeGeometry geometry);
		protected abstract void HandleValidNode(NodeGeometry geometry);

		public void OnUpdate(NodeGeometry geometry) {
			if (!geometry.IsValid()) {
				Log._Debug($"{this.GetType().Name}.HandleInvalidNode({geometry.NodeId})");
				HandleInvalidNode(geometry);
				UnsubscribeFromNodeGeometry(geometry.NodeId);
			} else {
				Log._Debug($"{this.GetType().Name}.HandleValidNode({geometry.NodeId})");
				HandleValidNode(geometry);
			}
		}

		~AbstractNodeGeometryObservingManager() {
			UnsubscribeFromAllNodeGeometries();
		}
	}
}
