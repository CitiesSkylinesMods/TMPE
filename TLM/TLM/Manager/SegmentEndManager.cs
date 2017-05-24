using CSUtil.Commons;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.Geometry;
using TrafficManager.Traffic;

namespace TrafficManager.Manager {
	public class SegmentEndManager : AbstractCustomManager {
		public static readonly SegmentEndManager Instance = new SegmentEndManager();

		private SegmentEnd[] SegmentEnds;

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

		public SegmentEnd GetSegmentEnd(SegmentEndId endId) {
			return GetSegmentEnd(endId.SegmentId, endId.StartNode);
		}

		public SegmentEnd GetSegmentEnd(ushort segmentId, bool startNode) {
			return SegmentEnds[GetIndex(segmentId, startNode)];
		}

		public SegmentEnd GetOrAddSegmentEnd(SegmentEndId endId) {
			return GetOrAddSegmentEnd(endId.SegmentId, endId.StartNode);
		}

		public SegmentEnd GetOrAddSegmentEnd(ushort segmentId, bool startNode) {
			SegmentEnd end = GetSegmentEnd(segmentId, startNode);
			if (end != null) {
				return end;
			}

			return SegmentEnds[GetIndex(segmentId, startNode)] = new SegmentEnd(segmentId, startNode);
		}

		public void RemoveSegmentEnd(SegmentEndId endId) {
			RemoveSegmentEnd(endId.SegmentId, endId.StartNode);
		}

		public void RemoveSegmentEnd(ushort segmentId, bool startNode) {
#if DEBUG
			Log._Debug($"SegmentEndManager.RemoveSegmentEnd({segmentId}, {startNode}) called");
#endif
			DestroySegmentEnd(GetIndex(segmentId, startNode));
		}

		public void RemoveSegmentEnds(ushort segmentId) {
			RemoveSegmentEnd(segmentId, true);
			RemoveSegmentEnd(segmentId, false);
		}

		public void SegmentSimulationStep(ushort segmentId) {
			GetSegmentEnd(segmentId, true)?.SimulationStep();
			GetSegmentEnd(segmentId, false)?.SimulationStep();
		}

		public bool UpdateSegmentEnd(SegmentEndId endId) {
			return UpdateSegmentEnd(endId.SegmentId, endId.StartNode);
		}

		public bool UpdateSegmentEnd(ushort segmentId, bool startNode) {
			SegmentGeometry segGeo = SegmentGeometry.Get(segmentId);
			if (segGeo == null) {
				Log._Debug($"SegmentEndManager.UpdateSegmentEnd({segmentId}, {startNode}): Segment {segmentId} is invalid. Removing all segment ends.");
				RemoveSegmentEnds(segmentId);
				return false;
			}

			SegmentEndGeometry end = segGeo.GetEnd(startNode);
			if (end == null) {
				Log._Debug($"SegmentEndManager.UpdateSegmentEnd({segmentId}, {startNode}): Segment end {segmentId} @ {startNode} is invalid. Removing segment end.");
				RemoveSegmentEnd(segmentId, startNode);
				return false;
			}

			if (TrafficPriorityManager.Instance.HasSegmentPrioritySign(segmentId, startNode) ||
				TrafficLightSimulationManager.Instance.HasTimedSimulation(end.NodeId())) {
				Log._Debug($"SegmentEndManager.UpdateSegmentEnd({segmentId}, {startNode}): Segment {segmentId} @ {startNode} has timed light or priority sign. Adding segment end {segmentId} @ {startNode}");
				GetOrAddSegmentEnd(segmentId, startNode).Update();
				return true;
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
