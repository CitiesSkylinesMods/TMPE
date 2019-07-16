﻿using CSUtil.Commons;
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
	using State.ConfigData;

	[Obsolete("should be removed when implementing issue #240")]
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

			if (! Services.NetService.IsSegmentValid(segmentId)) {
				Log.Warning($"SegmentEndManager.GetOrAddSegmentEnd({segmentId}, {startNode}): Refusing to add segment end for invalid segment.");
				return null;
			}

			return SegmentEnds[GetIndex(segmentId, startNode)] = new SegmentEnd(segmentId, startNode);
		}

		public void RemoveSegmentEnd(ISegmentEndId endId) {
			RemoveSegmentEnd(endId.SegmentId, endId.StartNode);
		}

		public void RemoveSegmentEnd(ushort segmentId, bool startNode) {
#if DEBUG
			bool debug = DebugSwitch.PriorityRules.Get() && (DebugSettings.SegmentId <= 0 || segmentId == DebugSettings.SegmentId);
			if (debug) {
				Log._Debug($"SegmentEndManager.RemoveSegmentEnd({segmentId}, {startNode}) called");
			}
#endif
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
#if DEBUG
			bool debug = DebugSwitch.PriorityRules.Get() && (DebugSettings.SegmentId <= 0 || segmentId == DebugSettings.SegmentId);
#endif

			if (! Services.NetService.IsSegmentValid(segmentId)) {
#if DEBUG
				if (debug) {
					Log._Debug($"SegmentEndManager.UpdateSegmentEnd({segmentId}, {startNode}): Segment {segmentId} is invalid. Removing all segment ends.");
				}
#endif

				RemoveSegmentEnds(segmentId);
				return false;
			}

			if (TrafficPriorityManager.Instance.HasSegmentPrioritySign(segmentId, startNode) ||
				TrafficLightSimulationManager.Instance.HasTimedSimulation(Services.NetService.GetSegmentNodeId(segmentId, startNode))) {
#if DEBUG
				if (debug) {
					Log._Debug($"SegmentEndManager.UpdateSegmentEnd({segmentId}, {startNode}): Segment {segmentId} @ {startNode} has timed light or priority sign. Adding segment end {segmentId} @ {startNode}");
				}
#endif
					ISegmentEnd end = GetOrAddSegmentEnd(segmentId, startNode);
				if (end == null) {
					Log.Warning($"SegmentEndManager.UpdateSegmentEnd({segmentId}, {startNode}): Failed to add segment end.");
					return false;
				} else {
#if DEBUG
					if (debug) {
						Log._Debug($"SegmentEndManager.UpdateSegmentEnd({segmentId}, {startNode}): Added segment end. Updating now.");
					}
#endif
					end.Update();
#if DEBUG
					if (debug) {
						Log._Debug($"SegmentEndManager.UpdateSegmentEnd({segmentId}, {startNode}): Update of segment end finished.");
					}
#endif
					return true;
				}
			} else {
#if DEBUG
				if (debug) {
					Log._Debug($"SegmentEndManager.UpdateSegmentEnd({segmentId}, {startNode}): Segment {segmentId} @ {startNode} neither has timed light nor priority sign. Removing segment end {segmentId} @ {startNode}");
				}
#endif
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
			//Log._Debug($"SegmentEndManager.DestroySegmentEnd({index}) called");
#endif
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
