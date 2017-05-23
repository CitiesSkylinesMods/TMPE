using CSUtil.Commons;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using TrafficManager.Geometry;
using TrafficManager.Util;

namespace TrafficManager.Manager {
	public abstract class AbstractSegmentGeometryObservingManager : AbstractCustomManager, IObserver<SegmentGeometry> {
		private Dictionary<ushort, IDisposable> segGeometryUnsubscribers = new Dictionary<ushort, IDisposable>();
		private object geoLock = new object();

		protected virtual bool AllowInvalidSegments { get; } = false;

		protected override void InternalPrintDebugInfo() {
			base.InternalPrintDebugInfo();
			Log._Debug($"Subscribed segment geometries: {segGeometryUnsubscribers.Keys.CollectionToString()}");
		}

		protected void UnsubscribeFromSegmentGeometry(ushort segmentId) {
#if DEBUGCONN
			Log._Debug($"AbstractSegmentGeometryObservingManager.UnsubscribeFromSegmentGeometry({segmentId}) called.");
#endif
			try {
				Monitor.Enter(geoLock);

				IDisposable unsubscriber;
				if (segGeometryUnsubscribers.TryGetValue(segmentId, out unsubscriber)) {
					unsubscriber.Dispose();
					segGeometryUnsubscribers.Remove(segmentId);
				}
#if DEBUGCONN
				Log._Debug($"AbstractSegmentGeometryObservingManager.UnsubscribeFromSegmentGeometry({segmentId}): watched segments: {String.Join(",", segGeometryUnsubscribers.Keys.Select(x => x.ToString()).ToArray())}");
#endif
			} finally {
				Monitor.Exit(geoLock);
			}
		}

		protected void UnsubscribeFromAllSegmentGeometries() {
#if DEBUGCONN
			Log._Debug($"AbstractSegmentGeometryObservingManager.UnsubscribeFromAllSegmentGeometries() called.");
#endif
			List<ushort> segmentIds = new List<ushort>(segGeometryUnsubscribers.Keys);
			foreach (ushort segmentId in segmentIds)
				UnsubscribeFromSegmentGeometry(segmentId);
		}

		protected void SubscribeToSegmentGeometry(ushort segmentId) {
#if DEBUGCONN
			Log._Debug($"AbstractSegmentGeometryObservingManager.SubscribeToSegmentGeometry({segmentId}) called.");
#endif
			try {
				Monitor.Enter(geoLock);

				if (!segGeometryUnsubscribers.ContainsKey(segmentId)) {
					segGeometryUnsubscribers.Add(segmentId, SegmentGeometry.Get(segmentId, AllowInvalidSegments).Subscribe(this));
				}

#if DEBUGCONN
				Log._Debug($"AbstractSegmentGeometryObservingManager.SubscribeToSegmentGeometry({segmentId}): watched segments: {String.Join(",", segGeometryUnsubscribers.Keys.Select(x => x.ToString()).ToArray())}");
#endif
			} finally {
				Monitor.Exit(geoLock);
			}
		}

		public override void OnLevelUnloading() {
			base.OnLevelUnloading();
			UnsubscribeFromAllSegmentGeometries();
		}

		protected abstract void HandleInvalidSegment(SegmentGeometry geometry);
		protected abstract void HandleValidSegment(SegmentGeometry geometry);

		public void OnUpdate(SegmentGeometry geometry) {
			if (!geometry.IsValid()) {
				//Log._Debug($"{this.GetType().Name}.HandleInvalidSegment({geometry.SegmentId})");
				HandleInvalidSegment(geometry);
				if (!AllowInvalidSegments) {
					UnsubscribeFromSegmentGeometry(geometry.SegmentId);
				}
			} else {
				//Log._Debug($"{this.GetType().Name}.HandleValidSegment({geometry.SegmentId})");
				HandleValidSegment(geometry);
			}
		}

		~AbstractSegmentGeometryObservingManager() {
#if DEBUGCONN
			Log._Debug($"~AbstractSegmentGeometryObservingManager() called.");
#endif
			UnsubscribeFromAllSegmentGeometries();
		}
	}
}
