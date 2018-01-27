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
		private Dictionary<ushort, IDisposable> segGeometryUnsubscribers = new Dictionary<ushort, IDisposable>();
		private object geoLock = new object();

		protected virtual bool AllowInvalidSegments { get; } = false;

		protected override void InternalPrintDebugInfo() {
			base.InternalPrintDebugInfo();
			Log._Debug($"Subscribed segment geometries: {segGeometryUnsubscribers.Keys.CollectionToString()}");
		}

		protected void UnsubscribeFromSegmentGeometry(ushort segmentId) {
#if DEBUGGEO
			if (GlobalConfig.Instance.Debug.Switches[5])
				Log._Debug($"AbstractSegmentGeometryObservingManager.UnsubscribeFromSegmentGeometry({segmentId}) called.");
#endif
			try {
				Monitor.Enter(geoLock);

				IDisposable unsubscriber;
				if (segGeometryUnsubscribers.TryGetValue(segmentId, out unsubscriber)) {
					unsubscriber.Dispose();
					segGeometryUnsubscribers.Remove(segmentId);
				}
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
			List<ushort> segmentIds = new List<ushort>(segGeometryUnsubscribers.Keys);
			foreach (ushort segmentId in segmentIds)
				UnsubscribeFromSegmentGeometry(segmentId);
		}

		protected void SubscribeToSegmentGeometry(ushort segmentId) {
#if DEBUGGEO
			if (GlobalConfig.Instance.Debug.Switches[5])
				Log._Debug($"AbstractSegmentGeometryObservingManager.SubscribeToSegmentGeometry({segmentId}) called.");
#endif
			try {
				Monitor.Enter(geoLock);

				if (!segGeometryUnsubscribers.ContainsKey(segmentId)) {
					SegmentGeometry geo = SegmentGeometry.Get(segmentId, AllowInvalidSegments);
					if (geo != null) {
						segGeometryUnsubscribers.Add(segmentId, geo.Subscribe(this));
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
