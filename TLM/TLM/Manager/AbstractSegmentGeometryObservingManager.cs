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
	public abstract class AbstractSegmentGeometryObservingManager : AbstractCustomManager, IObserver<SegmentGeometry> {
		private IDisposable[] segGeometryUnsubscribers = new IDisposable[NetManager.MAX_SEGMENT_COUNT];
		private object geoLock = new object();

		protected virtual bool AllowInvalidSegments { get; } = false;

		protected override void InternalPrintDebugInfo() {
			base.InternalPrintDebugInfo();
			Log._Debug($"Subscribed segment geometries: {segGeometryUnsubscribers.Select(unsub => unsub != null).ToList().CollectionToString()}");
		}

		protected void UnsubscribeFromSegmentGeometry(ushort segmentId) {
#if DEBUGGEO
			if (GlobalConfig.Instance.Debug.Switches[5])
				Log._Debug($"AbstractSegmentGeometryObservingManager.UnsubscribeFromSegmentGeometry({segmentId}) called.");
#endif
			try {
				Monitor.Enter(geoLock);

				InternalUnsubscribeFromSegmentGeometry(segmentId);
#if DEBUGGEO
				if (GlobalConfig.Instance.Debug.Switches[5])
					Log._Debug($"AbstractSegmentGeometryObservingManager.UnsubscribeFromSegmentGeometry({segmentId}): watched segments: {String.Join(",", segGeometryUnsubscribers.Keys.Select(x => x.ToString()).ToArray())}");
#endif
			} finally {
				Monitor.Exit(geoLock);
			}
		}

		protected void UnsubscribeFromAllSegmentGeometries() {
#if DEBUGGEO
			if (GlobalConfig.Instance.Debug.Switches[5])
				Log._Debug($"AbstractSegmentGeometryObservingManager.UnsubscribeFromAllSegmentGeometries() called.");
#endif
			try {
				Monitor.Enter(geoLock);

				for (int segmentId = 0; segmentId < NetManager.MAX_SEGMENT_COUNT; ++segmentId) {
					InternalUnsubscribeFromSegmentGeometry((ushort)segmentId);
				}
			} finally {
				Monitor.Exit(geoLock);
			}
		}

		private void InternalUnsubscribeFromSegmentGeometry(ushort segmentId) {
			IDisposable unsubscriber = segGeometryUnsubscribers[segmentId];
			if (unsubscriber != null) {
				unsubscriber.Dispose();
				segGeometryUnsubscribers[segmentId] = null;
			}
		}

		protected void SubscribeToSegmentGeometry(ushort segmentId) {
#if DEBUGGEO
			if (GlobalConfig.Instance.Debug.Switches[5])
				Log._Debug($"AbstractSegmentGeometryObservingManager.SubscribeToSegmentGeometry({segmentId}) called.");
#endif
			try {
				Monitor.Enter(geoLock);

				if (segGeometryUnsubscribers[segmentId] == null) {
					SegmentGeometry geo = SegmentGeometry.Get(segmentId, AllowInvalidSegments);
					if (geo != null) {
						segGeometryUnsubscribers[segmentId] = geo.Subscribe(this);
					} else {
#if DEBUGGEO
						if (GlobalConfig.Instance.Debug.Switches[5])
							Log.Warning($"AbstractSegmentGeometryObservingManager.SubscribeToSegmentGeometry({segmentId}): geometry is null.");
#endif
					}
				}
			} finally {
				Monitor.Exit(geoLock);
			}

#if DEBUGGEO
			if (GlobalConfig.Instance.Debug.Switches[5])
				Log._Debug($"AbstractSegmentGeometryObservingManager.SubscribeToSegmentGeometry({segmentId}): watched segments: {String.Join(",", segGeometryUnsubscribers.Keys.Select(x => x.ToString()).ToArray())}");
#endif
		}

		public override void OnLevelUnloading() {
			base.OnLevelUnloading();
			UnsubscribeFromAllSegmentGeometries();
		}

		protected abstract void HandleInvalidSegment(SegmentGeometry geometry);
		protected abstract void HandleValidSegment(SegmentGeometry geometry);

		public void OnUpdate(IObservable<SegmentGeometry> observable) {
			SegmentGeometry geometry = (SegmentGeometry)observable;
			if (!geometry.IsValid()) {
#if DEBUGGEO
				if (GlobalConfig.Instance.Debug.Switches[5])
					Log._Debug($"{this.GetType().Name}.HandleInvalidSegment({geometry.SegmentId})");
#endif
				HandleInvalidSegment(geometry);
				if (!AllowInvalidSegments) {
					UnsubscribeFromSegmentGeometry(geometry.SegmentId);
				}
			} else {
#if DEBUGGEO
				if (GlobalConfig.Instance.Debug.Switches[5])
					Log._Debug($"{this.GetType().Name}.HandleValidSegment({geometry.SegmentId})");
#endif
				HandleValidSegment(geometry);
			}
		}

		~AbstractSegmentGeometryObservingManager() {
#if DEBUGGEO
			if (GlobalConfig.Instance.Debug.Switches[5])
				Log._Debug($"~AbstractSegmentGeometryObservingManager() called.");
#endif
			UnsubscribeFromAllSegmentGeometries();
		}
	}
}
