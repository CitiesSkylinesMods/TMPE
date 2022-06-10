using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TrafficManager.ExtPrefabs {
    partial class ExtNetInfo {

        private class LaneEvaluator {

            private readonly NetInfo.Lane[] lanes;
            private readonly ExtNetInfo extNetInfo;

            public LaneEvaluator(ExtNetInfo extNetInfo, NetInfo.Lane[] lanes) {
                this.lanes = lanes;
                this.extNetInfo = extNetInfo;
            }

            private int GetDirectionalSortKey(ExtLaneFlags flags) {
                switch (flags & LaneGroupDirection) {
                    case ExtLaneFlags.BackwardGroup:
                        return 1;
                    default:
                        return 2;
                    case ExtLaneFlags.ForwardGroup:
                        return 3;
                }
            }

            // TODO: Boundary conditions on sorting need work
            public void CalculateSortedLanes() {
                extNetInfo.m_sortedLanes = extNetInfo.m_extLanes.Select((extInfo, index) => index)
                                            .OrderBy(i => lanes[i].m_position)
                                            .ThenBy(i => GetDirectionalSortKey(extNetInfo.m_extLanes[i].m_extFlags))
                                            .ToArray();
            }

            public LaneConfiguration GetLaneConfiguration() {

                var minForward = float.MaxValue;
                var maxForward = float.MinValue;
                var minBackward = float.MaxValue;
                var maxBackward = float.MinValue;

                for (int i = 0; i < lanes.Length; i++) {
                    var flags = extNetInfo.m_extLanes[i].m_extFlags;
                    if ((flags & ExtLaneFlags.ForwardGroup) != 0) {
                        float position = lanes[i].m_position;
                        minForward = Math.Min(minForward, (float)position);
                        maxForward = Math.Max(maxForward, (float)position);

                    } else if ((flags & ExtLaneFlags.BackwardGroup) != 0) {
                        float position = lanes[i].m_position;
                        minBackward = Math.Min(minBackward, (float)position);
                        maxBackward = Math.Max(maxBackward, (float)position);
                    }
                }

                if (minForward == float.MaxValue || minBackward == float.MaxValue)
                    return LaneConfiguration.OneWay;
                else if (maxForward < minBackward || (minForward < minBackward && maxForward == minBackward))
                    return LaneConfiguration.Inverted;
                else if (minForward < maxBackward)
                    return LaneConfiguration.Complex;
                else
                    return LaneConfiguration.Simple;
            }

            public void FindDisplacedOuter(ExtLaneFlags workingDirection, ref int lastOuterDisplacedIndex) {

                bool backward = workingDirection == ExtLaneFlags.BackwardGroup;

                int scanStart = backward ? lanes.Length - 1 : 0;
                int scanEnd = backward ? -1 : lanes.Length;
                int step = backward ? -1 : 1;

                for (int sortedIndex = scanStart; sortedIndex != scanEnd; sortedIndex += step) {
                    ExtLaneInfo extLane = extNetInfo.m_extLanes[extNetInfo.m_sortedLanes[sortedIndex]];
                    var direction = extLane.m_extFlags & LaneGroupDirection;
                    if ((direction & workingDirection) != 0) {
                        extLane.m_extFlags |= ExtLaneFlags.DisplacedOuter;
                        lastOuterDisplacedIndex = sortedIndex;
                    } else if (direction != 0) {
                        break;
                    }
                }
            }

            public void FindOuterAndDisplacedInner(ExtLaneFlags workingDirection) {

                bool backward = workingDirection == ExtLaneFlags.BackwardGroup;

                var oppositeDirection = backward ? ExtLaneFlags.ForwardGroup : ExtLaneFlags.BackwardGroup;
                int scanStart = backward ? 0 : lanes.Length - 1;
                int scanEnd = backward ? lanes.Length : -1;
                int step = backward ? 1 : -1;

                int sortedIndex;

                // skip opposite direction DisplacedOuter
                for (sortedIndex = scanStart; sortedIndex != scanEnd; sortedIndex += step) {
                    if ((extNetInfo.m_extLanes[extNetInfo.m_sortedLanes[sortedIndex]].m_extFlags & workingDirection) != 0)
                        break;
                }

                // scan for Outer (may convert some to Inner later)
                for (; sortedIndex != scanEnd; sortedIndex += step) {
                    var extLane = extNetInfo.m_extLanes[extNetInfo.m_sortedLanes[sortedIndex]];
                    var direction = extLane.m_extFlags & LaneGroupDirection;
                    if ((direction & workingDirection) != 0)
                        extLane.m_extFlags |= ExtLaneFlags.Outer;
                    if ((direction & oppositeDirection) != 0)
                        break;
                }

                // skip opposite direction
                for (; sortedIndex != scanEnd; sortedIndex += step) {
                    if ((extNetInfo.m_extLanes[extNetInfo.m_sortedLanes[sortedIndex]].m_extFlags & workingDirection) != 0)
                        break;
                }

                // scan for DisplacedInner|ForwardGroup
                for (; sortedIndex != scanEnd; sortedIndex += step) {
                    int laneIndex = extNetInfo.m_sortedLanes[sortedIndex];
                    var extLane = extNetInfo.m_extLanes[laneIndex];
                    if ((extLane.m_extFlags & workingDirection) != 0) {

                        // if we find DisplacedOuter lanes, we've reached the other side and we're done
                        if ((extLane.m_extFlags & ExtLaneFlags.DisplacedOuter) != 0)
                            break;

                        var lane = lanes[laneIndex];
                        extLane.m_extFlags |= ExtLaneFlags.DisplacedInner;
                        if (lane.m_allowConnect)
                            extLane.m_extFlags |= ExtLaneFlags.ForbidControlledLanes;
                        else if (lane.m_laneType == NetInfo.LaneType.Vehicle)
                            extLane.m_extFlags |= ExtLaneFlags.AllowCFI;
                    }
                }
            }

        }
    }
}
