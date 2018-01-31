using CSUtil.Commons;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.Geometry;
using TrafficManager.Geometry.Impl;
using TrafficManager.State;
using TrafficManager.Traffic;
using TrafficManager.Traffic.Impl;
using TrafficManager.TrafficLight;

namespace TrafficManager.Manager.Impl {
	public class SegmentEndManager : AbstractCustomManager, ISegmentEndManager {
		public static readonly SegmentEndManager Instance = new SegmentEndManager();

		private ISegmentEnd[] SegmentEnds;

		private SegmentEndManager() {
			SegmentEnds = new SegmentEnd[2 * NetManager.MAX_SEGMENT_COUNT];
		}

		protected override void InternalPrintDebugInfo() {
			base.InternalPrintDebugInfo();
			Log._Debug($"Segment ends:");
			for (int i = 0; i < SegmentEnds.Length; ++i) {
				if (SegmentEnds[i] == null) {
					continue;
				}
				Log._Debug($"Segment end {i}: {SegmentEnds[i]}");
			}
		}

		public ISegmentEnd GetSegmentEnd(ISegmentEndId endId) {
			return GetSegmentEnd(endId.SegmentId, endId.StartNode);
		}

		public ISegmentEnd GetSegmentEnd(ushort segmentId, bool startNode) {
			return SegmentEnds[GetIndex(segmentId, startNode)];
		}

		public ISegmentEnd GetOrAddSegmentEnd(ISegmentEndId endId) {
			return GetOrAddSegmentEnd(endId.SegmentId, endId.StartNode);
		}

		public ISegmentEnd GetOrAddSegmentEnd(ushort segmentId, bool startNode) {
			ISegmentEnd end = GetSegmentEnd(segmentId, startNode);
			if (end != null) {
				return end;
			}

			SegmentGeometry segGeo = SegmentGeometry.Get(segmentId);
			if (segGeo == null) {
				Log.Warning($"SegmentEndManager.GetOrAddSegmentEnd({segmentId}, {startNode}): Refusing to add segment end for invalid segment.");
				return null;
			}

			SegmentEndGeometry endGeo = segGeo.GetEnd(startNode);
			if (endGeo == null) {
				Log.Warning($"SegmentEndManager.GetOrAddSegmentEnd({segmentId}, {startNode}): Refusing to add segment end for invalid segment end.");
				return null;
			}

			return SegmentEnds[GetIndex(segmentId, startNode)] = new SegmentEnd(segmentId, startNode);
		}

		public void RemoveSegmentEnd(ISegmentEndId endId) {
			RemoveSegmentEnd(endId.SegmentId, endId.StartNode);
		}

		public void RemoveSegmentEnd(ushort segmentId, bool startNode) {
			Log._Debug($"SegmentEndManager.RemoveSegmentEnd({segmentId}, {startNode}) called");
			DestroySegmentEnd(GetIndex(segmentId, startNode));
		}

		public void RemoveSegmentEnds(ushort segmentId) {
			RemoveSegmentEnd(segmentId, true);
			RemoveSegmentEnd(segmentId, false);
		}

		public bool UpdateSegmentEnd(ISegmentEndId endId) {
			return UpdateSegmentEnd(endId.SegmentId, endId.StartNode);
		}

		public bool UpdateSegmentEnd(ushort segmentId, bool startNode) {
			SegmentGeometry segGeo = SegmentGeometry.Get(segmentId);
			if (segGeo == null) {
				Log._Debug($"SegmentEndManager.UpdateSegmentEnd({segmentId}, {startNode}): Segment {segmentId} is invalid. Removing all segment ends.");
				RemoveSegmentEnds(segmentId);
				return false;
			}

			SegmentEndGeometry endGeo = segGeo.GetEnd(startNode);
			if (endGeo == null) {
				Log._Debug($"SegmentEndManager.UpdateSegmentEnd({segmentId}, {startNode}): Segment end {segmentId} @ {startNode} is invalid. Removing segment end.");
				RemoveSegmentEnd(segmentId, startNode);
				return false;
			}

			if (TrafficPriorityManager.Instance.HasSegmentPrioritySign(segmentId, startNode) ||
				TrafficLightSimulationManager.Instance.HasTimedSimulation(endGeo.NodeId())) {
				Log._Debug($"SegmentEndManager.UpdateSegmentEnd({segmentId}, {startNode}): Segment {segmentId} @ {startNode} has timed light or priority sign. Adding segment end {segmentId} @ {startNode}");
				ISegmentEnd end = GetOrAddSegmentEnd(segmentId, startNode);
				if (end == null) {
					Log.Warning($"SegmentEndManager.UpdateSegmentEnd({segmentId}, {startNode}): Failed to add segment end.");
					return false;
				} else {
					Log._Debug($"SegmentEndManager.UpdateSegmentEnd({segmentId}, {startNode}): Added segment end. Updating now.");
					end.Update();
					Log._Debug($"SegmentEndManager.UpdateSegmentEnd({segmentId}, {startNode}): Update of segment end finished.");
					return true;
				}
			} else {
				Log._Debug($"SegmentEndManager.UpdateSegmentEnd({segmentId}, {startNode}): Segment {segmentId} @ {startNode} neither has timed light nor priority sign. Removing segment end {segmentId} @ {startNode}");
				RemoveSegmentEnd(segmentId, startNode);
				return false;
			}
		}

		private int GetIndex(ushort segmentId, bool startNode) {
			return (int)segmentId + (startNode ? 0 : NetManager.MAX_SEGMENT_COUNT);
		}

		/*protected override void HandleInvalidSegment(SegmentGeometry geometry) {
			RemoveSegmentEnds(geometry.SegmentId);
		}

		protected override void HandleValidSegment(SegmentGeometry geometry) {
			RemoveSegmentEnds(geometry.SegmentId);
		}*/

		protected void DestroySegmentEnd(int index) {
#if DEBUG
			Log._Debug($"SegmentEndManager.DestroySegmentEnd({index}) called");
#endif
			SegmentEnds[index]?.Destroy();
			SegmentEnds[index] = null;
		}

		public override void OnLevelUnloading() {
			base.OnLevelUnloading();
			for (int i = 0; i < SegmentEnds.Length; ++i) {
				DestroySegmentEnd(i);
			}
		}
	}
}
