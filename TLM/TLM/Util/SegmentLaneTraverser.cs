using CSUtil.Commons;
using GenericGameBridge.Service;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.Geometry;
using static TrafficManager.Util.SegmentTraverser;

namespace TrafficManager.Util {
	public class SegmentLaneTraverser {
		public delegate bool SegmentLaneVisitor(SegmentLaneVisitData data);

		[Flags]
		public enum LaneStopCriterion {
			/// <summary>
			/// Traversal stops when the whole network has been visited
			/// </summary>
			None = 0,
			/// <summary>
			/// Traversal stops when a segment consists of a different number of filtered lanes than the initial segment
			/// </summary>
			LaneCount = 1
		}

		public class SegmentLaneVisitData {
			/// <summary>
			/// Segment visit data
			/// </summary>
			public SegmentVisitData segVisitData;

			/// <summary>
			/// Iteration index
			/// </summary>
			public int sortedLaneIndex;

			/// <summary>
			/// current traversed lane position
			/// </summary>
			public LanePos curLanePos;

			/// <summary>
			/// matching initial lane position
			/// </summary>
			public LanePos initLanePos;

			public SegmentLaneVisitData(SegmentVisitData segVisitData, int sortedLaneIndex, LanePos curLanePos, LanePos initLanePos) {
				this.segVisitData = segVisitData;
				this.sortedLaneIndex = sortedLaneIndex;
				this.curLanePos = curLanePos;
				this.initLanePos = initLanePos;
			}
		}

		public static void Traverse(ushort initialSegmentId, TraverseDirection direction, LaneStopCriterion laneStopCrit, SegmentStopCriterion segStopCrit, NetInfo.LaneType? laneTypeFilter, VehicleInfo.VehicleType? vehicleTypeFilter, SegmentLaneVisitor laneVisitor) {
			IList<LanePos> initialSortedLanes = null;

			SegmentTraverser.Traverse(initialSegmentId, direction, segStopCrit, delegate(SegmentVisitData segData) {
				bool isInitialSeg = segData.initial;
				bool reverse = !isInitialSeg && segData.viaStartNode == segData.viaInitialStartNode;
				bool ret = false;
				Constants.ServiceFactory.NetService.ProcessSegment(segData.curGeo.SegmentId, delegate (ushort segmentId, ref NetSegment segment) {
					Log._Debug($"SegmentLaneTraverser: Reached segment {segmentId}: isInitialSeg={isInitialSeg} viaStartNode={segData.viaStartNode} viaInitialStartNode={segData.viaInitialStartNode} reverse={reverse}");
					IList <LanePos> sortedLanes = Constants.ServiceFactory.NetService.GetSortedLanes(segmentId, ref segment, null, laneTypeFilter, vehicleTypeFilter, reverse);

					if (isInitialSeg) {
						initialSortedLanes = sortedLanes;
					} else if (initialSortedLanes == null) {
						throw new ApplicationException("Initial list of sorted lanes not set.");
					} else if (sortedLanes.Count != initialSortedLanes.Count && (laneStopCrit & LaneStopCriterion.LaneCount) != LaneStopCriterion.None) {
						Log._Debug($"SegmentLaneTraverser: Stop criterion reached @ {segmentId}: {sortedLanes.Count} current vs. {initialSortedLanes.Count} initial lanes");
						return false;
					}

					for (int i = 0; i < sortedLanes.Count; ++i) {
						Log._Debug($"SegmentLaneTraverser: Traversing segment lane {sortedLanes[i].laneIndex} @ {segmentId} (id {sortedLanes[i].laneId}, pos {sortedLanes[i].position})");

						if (!laneVisitor(new SegmentLaneVisitData(segData, i, sortedLanes[i], initialSortedLanes[i]))) {
							return false;
						}
					}
					ret = true;
					return true;
				});
				return ret;
			});
		}
	}
}
