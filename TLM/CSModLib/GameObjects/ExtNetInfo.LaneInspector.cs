using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CSModLib.GameObjects {
    partial class ExtNetInfo {

        private class LaneInspector {

            private readonly NetInfo.Lane[] lanes;
            private readonly ExtNetInfo extNetInfo;

            public LaneInspector(ExtNetInfo extNetInfo, NetInfo.Lane[] lanes) {
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

                if (minBackward == float.MaxValue)
                    return minForward == float.MaxValue ? LaneConfiguration.Undefined : LaneConfiguration.OneWay;
                else if (minForward == float.MaxValue)
                    return LaneConfiguration.InvertedOneWay;
                else if (maxForward < minBackward || (minForward < minBackward && maxForward == minBackward))
                    return LaneConfiguration.Inverted;
                else if (minForward < maxBackward)
                    return LaneConfiguration.Complex;
                else
                    return LaneConfiguration.TwoWay;
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

            public void FindInner(ExtLaneFlags workingDirection) {

                bool backward = workingDirection == ExtLaneFlags.BackwardGroup;

                var oppositeDirection = backward ? ExtLaneFlags.ForwardGroup : ExtLaneFlags.BackwardGroup;
                int scanStart = backward ? 0 : lanes.Length - 1;
                int scanEnd = backward ? lanes.Length : -1;
                int step = backward ? 1 : -1;

                // The position multiplier causes lane positions to appear to this logic in ascending order
                // regardless of the direction in which we are evaluating them. This keeps the spacial
                // calculations simple on median detection.
                int positionMultiplier = backward ? 1 : -1;

                int sortedIndex;

                // advance to first Outer

                float laneWidth = float.MaxValue;
                float laneEdge = float.MaxValue;
                float laneElevation = float.MaxValue;
                float medianEdge = float.MaxValue;
                float medianElevation = float.MinValue;

                int firstOuterLane;
                int firstInnerLane = -1;

                for (firstOuterLane = scanStart; firstOuterLane != scanEnd; firstOuterLane += step) {

                    var laneIndex = extNetInfo.m_sortedLanes[firstOuterLane];
                    if ((extNetInfo.m_extLanes[laneIndex].m_extFlags & workingDirection) != 0) {
                        var lane = lanes[laneIndex];
                        laneWidth = lane.m_width;
                        laneEdge = lane.m_position * positionMultiplier + laneWidth / 2;
                        laneElevation = lane.m_verticalOffset;
                        break;
                    }
                }

                // scan for median

                for (sortedIndex = firstOuterLane + step; sortedIndex != scanEnd; sortedIndex += step) {
                    var laneIndex = extNetInfo.m_sortedLanes[sortedIndex];
                    var extLane = extNetInfo.m_extLanes[laneIndex];
                    var lane = lanes[laneIndex];
                    var position = lane.m_position * positionMultiplier;
                    if ((extLane.m_extFlags & workingDirection) != 0) {

                        if (((position - lane.m_width / 2) >= medianEdge
                                    && lane.m_verticalOffset < medianElevation
                                    && medianElevation - lane.m_verticalOffset < 3f)
                                || (position - lane.m_width - laneWidth) >= laneEdge) {
                            firstInnerLane = sortedIndex;
                            break;
                        }

                        medianEdge = float.MaxValue;
                        medianElevation = float.MinValue;
                        laneWidth = lane.m_width;
                        laneEdge = position + laneWidth / 2;
                        laneElevation = lane.m_verticalOffset;
                    } else if ((extLane.m_extFlags & oppositeDirection) != 0) {
                        break;
                    } else {
                        if (!lane.IsRoadLane() && lane.m_verticalOffset > laneElevation && lane.m_verticalOffset - laneElevation < 3f) {
                            if ((position - lane.m_width / 2) >= laneEdge) {
                                medianEdge = Math.Min(medianEdge, position + lane.m_width / 2);
                                medianElevation = Math.Max(medianElevation, lane.m_verticalOffset);
                            }
                        }
                    }
                }

                if (firstInnerLane >= 0) {
                    // apply AllowServiceLane

                    for (sortedIndex = scanStart; sortedIndex != firstInnerLane; sortedIndex += step) {
                        var laneIndex = extNetInfo.m_sortedLanes[sortedIndex];
                        var extLane = extNetInfo.m_extLanes[laneIndex];
                        if ((extLane.m_extFlags & (ExtLaneFlags.Outer | workingDirection)) == (ExtLaneFlags.Outer | workingDirection)
                                && lanes[laneIndex].IsCarLane()) {
                            extLane.m_extFlags |= ExtLaneFlags.AllowServiceLane;
                        }
                    }

                    // apply Inner

                    for (; sortedIndex != scanEnd; sortedIndex += step) {
                        var laneIndex = extNetInfo.m_sortedLanes[sortedIndex];
                        var extLane = extNetInfo.m_extLanes[laneIndex];
                        if ((extLane.m_extFlags & (ExtLaneFlags.Outer | workingDirection)) == (ExtLaneFlags.Outer | workingDirection)) {
                            extLane.m_extFlags &= ~ExtLaneFlags.Outer;
                            extLane.m_extFlags |= ExtLaneFlags.Inner;
                            var lane = lanes[laneIndex];
                            if (lane.m_allowConnect)
                                extLane.m_extFlags |= ExtLaneFlags.ForbidControlledLanes;
                            else if (lane.IsCarLane())
                                extLane.m_extFlags |= ExtLaneFlags.AllowExpressLane;
                        }
                    }
                }
            }
        }
    }
}