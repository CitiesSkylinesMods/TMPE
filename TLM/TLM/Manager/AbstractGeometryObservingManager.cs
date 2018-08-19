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

namespace TrafficManager.Manager {
	public abstract class AbstractGeometryObservingManager : AbstractCustomManager, IObserver<GeometryUpdate> {
		private IDisposable geoUpdateUnsubscriber = null;

		private object geoLock = new object();

		/// <summary>
		/// Handles an invalid segment
		/// </summary>
		/// <param name="geometry">segment geometry</param>
		protected virtual void HandleInvalidSegment(ISegmentGeometry geometry) { }

		/// <summary>
		/// Handles a valid segment
		/// </summary>
		/// <param name="geometry">segment geometry</param>
		protected virtual void HandleValidSegment(ISegmentGeometry geometry) { }

		/// <summary>
		/// Handles an invalid node
		/// </summary>
		/// <param name="geometry">node geometry</param>
		protected virtual void HandleInvalidNode(INodeGeometry geometry) { }

		/// <summary>
		/// Handles a valid node
		/// </summary>
		/// <param name="geometry">node geometry</param>
		protected virtual void HandleValidNode(INodeGeometry geometry) { }

		/// <summary>
		/// Handles a segment replacement
		/// </summary>
		/// <param name="replacement">segment replacement</param>
		/// <param name="newEndGeo">new segment end geometry</param>
		protected virtual void HandleSegmentEndReplacement(SegmentEndReplacement replacement, ISegmentEndGeometry newEndGeo) { }

		protected override void InternalPrintDebugInfo() {
			base.InternalPrintDebugInfo();
		}

		public override void OnLevelLoading() {
			base.OnLevelLoading();
			geoUpdateUnsubscriber = Constants.ManagerFactory.GeometryManager.Subscribe(this);
		}

		public override void OnLevelUnloading() {
			base.OnLevelUnloading();
			if (geoUpdateUnsubscriber != null) {
				geoUpdateUnsubscriber.Dispose();
			}
		}

		public void OnUpdate(GeometryUpdate update) {
			if (update.segmentGeometry != null) {
				// Handle a segment update
				ISegmentGeometry geometry = update.segmentGeometry;
				if (!geometry.Valid) {
#if DEBUGGEO
					if (GlobalConfig.Instance.Debug.Switches[5])
						Log._Debug($"{this.GetType().Name}.HandleInvalidSegment({geometry.SegmentId})");
#endif
					HandleInvalidSegment(geometry);
				} else {
#if DEBUGGEO
					if (GlobalConfig.Instance.Debug.Switches[5])
						Log._Debug($"{this.GetType().Name}.HandleValidSegment({geometry.SegmentId})");
#endif
					HandleValidSegment(geometry);
				}
			} else if (update.nodeGeometry != null) {
				// Handle a node update
				INodeGeometry geometry = update.nodeGeometry;
				if (!geometry.Valid) {
#if DEBUGGEO
					if (GlobalConfig.Instance.Debug.Switches[5])
						Log._Debug($"{this.GetType().Name}.HandleInvalidNode({geometry.NodeId})");
#endif
					HandleInvalidNode(geometry);
				} else {
#if DEBUGGEO
					if (GlobalConfig.Instance.Debug.Switches[5])
						Log._Debug($"{this.GetType().Name}.HandleValidNode({geometry.NodeId})");
#endif
					HandleValidNode(geometry);
				}
			} else {
				// Handle a segment end replacement
				ISegmentEndGeometry endGeo = SegmentGeometry.Get(update.replacement.newSegmentEndId.SegmentId)?.GetEnd(update.replacement.newSegmentEndId.StartNode);
				if (endGeo != null) {
#if DEBUGGEO
					if (GlobalConfig.Instance.Debug.Switches[5])
						Log._Debug($"{this.GetType().Name}.HandleSegmentReplacement({update.replacement.oldSegmentEndId} -> {update.replacement.newSegmentEndId})");
#endif
					HandleSegmentEndReplacement(update.replacement, endGeo);
				}
			}
		}

		~AbstractGeometryObservingManager() {
			if (geoUpdateUnsubscriber != null) {
				geoUpdateUnsubscriber.Dispose();
			}
		}
	}
}
