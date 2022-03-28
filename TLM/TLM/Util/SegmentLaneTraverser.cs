namespace TrafficManager.Util {
    using System;
    using System.Collections.Generic;
    using TrafficManager.Manager.Impl;
    using TrafficManager.Util.Extensions;
    using static SegmentTraverser;

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
            LaneCount = 1,
        }

        public class SegmentLaneVisitData {
            /// <summary>
            /// Segment visit data
            /// </summary>
            public SegmentVisitData SegVisitData;

            /// <summary>
            /// Iteration index
            /// </summary>
            public int SortedLaneIndex;

            /// <summary>
            /// current traversed lane position
            /// </summary>
            public LanePos CurLanePos;

            /// <summary>
            /// matching initial lane position
            /// </summary>
            public LanePos InitLanePos;

            public SegmentLaneVisitData(SegmentVisitData segVisitData,
                                        int sortedLaneIndex,
                                        LanePos curLanePos,
                                        LanePos initLanePos) {
                SegVisitData = segVisitData;
                SortedLaneIndex = sortedLaneIndex;
                CurLanePos = curLanePos;
                InitLanePos = initLanePos;
            }

            public NetInfo.Lane CurLaneInfo =>
                SegVisitData.CurSegmentInfo.m_lanes[CurLanePos.laneIndex];
        }

        public static void Traverse(ushort initialSegmentId,
                                    TraverseDirection direction,
                                    TraverseSide side,
                                    LaneStopCriterion laneStopCrit,
                                    SegmentStopCriterion segStopCrit,
                                    NetInfo.LaneType? laneTypeFilter,
                                    VehicleInfo.VehicleType? vehicleTypeFilter,
                                    SegmentLaneVisitor laneVisitor) {

            IList<LanePos> initialSortedLanes = null;

            //-------------------------------------
            // Function applies via SegmentTraverser
            //-------------------------------------
            bool VisitorFun(SegmentVisitData segData) {
                bool isInitialSeg = segData.Initial;
                bool reverse = !isInitialSeg && segData.ViaStartNode == segData.ViaInitialStartNode;
                bool ret = false;

                //-------------------------------------
                // Function applies for each segment
                //-------------------------------------
                bool VisitorProcessFun(ref NetSegment segment) {
                    // Log._Debug($"SegmentLaneTraverser: Reached segment {segmentId}:
                    //     isInitialSeg={isInitialSeg} viaStartNode={segData.viaStartNode}
                    //     viaInitialStartNode={segData.viaInitialStartNode} reverse={reverse}");

                    var sortedLanes = segment.GetSortedLanes(null, laneTypeFilter, vehicleTypeFilter, reverse);

                    if (isInitialSeg) {
                        initialSortedLanes = sortedLanes;
                    } else if (initialSortedLanes == null) {
                        throw new ApplicationException("Initial list of sorted lanes not set.");
                    } else if (sortedLanes.Count != initialSortedLanes.Count &&
                               (laneStopCrit & LaneStopCriterion.LaneCount) !=
                               LaneStopCriterion.None) {
                        // Log._Debug($"SegmentLaneTraverser: Stop criterion reached @ {segmentId}:
                        //     {sortedLanes.Count} current vs. {initialSortedLanes.Count} initial lanes");
                        return false;
                    }

                    for (int i = 0; i < sortedLanes.Count; ++i) {
                        // Log._Debug($"SegmentLaneTraverser: Traversing segment lane
                        //     {sortedLanes[i].laneIndex} @ {segmentId} (id {sortedLanes[i].laneId},
                        //     pos {sortedLanes[i].position})");

                        if (!laneVisitor(
                                new SegmentLaneVisitData(
                                    segData,
                                    i,
                                    sortedLanes[i],
                                    initialSortedLanes[i]))) {
                            return false;
                        }
                    }

                    ret = true;
                    return true;
                }

                ushort currentSegmentId = segData.CurSeg.segmentId;
                VisitorProcessFun(ref currentSegmentId.ToSegment());

                return ret;
            }

            SegmentTraverser.Traverse(initialSegmentId, direction, side, segStopCrit, VisitorFun);
        }
    }
}