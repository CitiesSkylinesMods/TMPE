using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.Util.Extensions;

namespace TrafficManager.ExtPrefabs {
    internal class ExtNetInfo : ExtPrefabInfo<ExtNetInfo, NetInfo> {

        [Flags]
        public enum ExtLaneFlags {

            None = 0,

            /// <summary>
            /// Lanes outside a median (if any) that divides lanes going the same direction.
            /// In the absence of such a median, these are simply lanes that are not displaced.
            /// </summary>
            Outer = 1 << 0,

            /// <summary>
            /// Lanes inside a median that divides lanes going the same direction.
            /// </summary>
            Inner = 1 << 1,

            /// <summary>
            /// Displaced lanes that are between lanes going the opposite direction.
            /// </summary>
            DisplacedInner = 1 << 2,

            /// <summary>
            /// Displaced lanes that are located on the far side of the road from what is normal for their direction.
            /// </summary>
            DisplacedOuter = 1 << 3,

            /// <summary>
            /// Lanes in a group whose prevailing direction is forward.
            /// </summary>
            ForwardGroup = 1 << 4,

            /// <summary>
            /// Lanes in a group whose prevailing direction is backward.
            /// </summary>
            BackwardGroup = 1 << 5,

            /// <summary>
            /// Lanes that may be treated as service lanes in controlled lane routing.
            /// </summary>
            AllowServiceLane = 1 << 6,

            /// <summary>
            /// Lanes that may be treated as express lanes in controlled lane routing.
            /// </summary>
            AllowExpressLane = 1 << 7,

            /// <summary>
            /// Lanes that may be treated as displaced far turn lanes in controlled lane routing.
            /// </summary>
            AllowCFI = 1 << 8,

            /// <summary>
            /// Lanes whose properties cause controlled lane routing to be disabled for the segment.
            /// </summary>
            ForbidControlledLanes = 1 << 9,

            OuterForward = Outer | ForwardGroup,
            InnerForward = Inner | ForwardGroup,
            DisplacedInnerForward = DisplacedInner | ForwardGroup,
            DisplacedOuterForward = DisplacedOuter | ForwardGroup,

            OuterBackward = Outer | BackwardGroup,
            InnerBackward = Inner | BackwardGroup,
            DisplacedInnerBackward = DisplacedInner | BackwardGroup,
            DisplacedOuterBackward = DisplacedOuter | BackwardGroup,
        }

        /// <summary>
        /// Mask to obtain the lane grouping key.
        /// </summary>
        public const ExtLaneFlags LaneGroupingKey =
                ExtLaneFlags.Outer | ExtLaneFlags.Inner
                | ExtLaneFlags.DisplacedInner | ExtLaneFlags.DisplacedOuter
                | ExtLaneFlags.ForwardGroup | ExtLaneFlags.BackwardGroup;

        /// <summary>
        /// Mask to test a segment for the presence of service lanes.
        /// </summary>
        public const ExtLaneFlags ServiceLaneRule = ExtLaneFlags.AllowServiceLane | ExtLaneFlags.ForbidControlledLanes;

        /// <summary>
        /// Mask to test a segment for the presence of express lanes.
        /// </summary>
        public const ExtLaneFlags ExpressLaneRule = ExtLaneFlags.AllowExpressLane | ExtLaneFlags.ForbidControlledLanes;

        /// <summary>
        /// Mask to test a segment for the presence of displaced far turn lanes.
        /// </summary>
        public const ExtLaneFlags CFIRule = ExtLaneFlags.AllowCFI | ExtLaneFlags.ForbidControlledLanes;

        /// <summary>
        /// Mask to test the prevailing direction of a lane group.
        /// </summary>
        public const ExtLaneFlags LaneGroupDirection = ExtLaneFlags.ForwardGroup | ExtLaneFlags.BackwardGroup;

        /// <summary>
        /// Mask to test a segment for the presence of any displaced lanes.
        /// </summary>
        public const ExtLaneFlags DisplacedLanes = ExtLaneFlags.DisplacedInner | ExtLaneFlags.DisplacedOuter;

        /// <summary>
        /// Extended lane prefab data, directly corresponding to <see cref="NetInfo.m_lanes"/>.
        /// </summary>
        public ExtLaneInfo[] m_extLanes;

        /// <summary>
        /// Lane group prefab data.
        /// </summary>
        public LaneGroupInfo[] m_laneGroups;

        /// <summary>
        /// Lane indices sorted by position, then by direction.
        /// </summary>
        public int[] m_sortedLanes;

        /// <summary>
        /// Aggregate lane flags for forward groups.
        /// </summary>
        public ExtLaneFlags m_forwardExtLaneFlags;

        /// <summary>
        /// Aggregate lane flags for backward groups.
        /// </summary>
        public ExtLaneFlags m_backwardExtLaneFlags;

        /// <summary>
        /// Aggregate of all lane flags.
        /// </summary>
        public ExtLaneFlags m_extLaneFlags;

        public ExtNetInfo(NetInfo info)
            : this(info.m_lanes) {
        }

        public ExtNetInfo(NetInfo.Lane[] lanes) {

            int GetDirectionalSortKey(ExtLaneFlags flags) {
                switch (flags & LaneGroupDirection) {
                    case ExtLaneFlags.BackwardGroup:
                        return 1;
                    default:
                        return 2;
                    case ExtLaneFlags.ForwardGroup:
                        return 3;
                }
            }

            m_extLanes = lanes.Select(l => new ExtLaneInfo(l)).ToArray();

            // TODO: Boundary conditions on sorting need work
            m_sortedLanes = m_extLanes.Select((extInfo, index) => index)
                                        .OrderBy(i => lanes[i].m_position)
                                        .ThenBy(i => GetDirectionalSortKey(m_extLanes[i].m_extFlags))
                                        .ToArray();

            var minForward = float.MaxValue;
            var maxForward = float.MinValue;
            var minBackward = float.MaxValue;
            var maxBackward = float.MinValue;

            for (int i = 0; i < lanes.Length; i++) {
                var flags = m_extLanes[i].m_extFlags;
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

            bool oneWay = minForward == float.MaxValue || minBackward == float.MaxValue;
            bool inverted = !oneWay && maxForward <= minBackward;

            var lastDisplacedOuterBackward = lanes.Length;
            var lastDisplacedOuterForward = -1;

            int sortedIndex;

            // bypass displaced scans for conventional, inverted, and one-way roads
            if (oneWay || inverted || maxBackward <= minForward) {
                for (int i = 0; i < m_extLanes.Length; i++) {
                    if (lanes[i].IsRoadLane())
                        m_extLanes[i].m_extFlags |= ExtLaneFlags.Outer;
                }
            } else {

                // scan for DisplacedOuter|ForwardGroup
                for (sortedIndex = 0; sortedIndex < lanes.Length; sortedIndex++) {
                    ExtLaneInfo extLane = m_extLanes[m_sortedLanes[sortedIndex]];
                    var direction = extLane.m_extFlags & LaneGroupDirection;
                    if ((direction & ExtLaneFlags.ForwardGroup) != 0) {
                        extLane.m_extFlags |= ExtLaneFlags.DisplacedOuter;
                        lastDisplacedOuterForward = sortedIndex;
                    }
                    if (direction != 0)
                        break;
                }

                // scan for DisplacedOuter|BackwardGroup
                for (sortedIndex = lanes.Length - 1; sortedIndex >= 0; sortedIndex--) {
                    ExtLaneInfo extLane = m_extLanes[m_sortedLanes[sortedIndex]];
                    var direction = extLane.m_extFlags & LaneGroupDirection;
                    if ((direction & ExtLaneFlags.BackwardGroup) != 0) {
                        extLane.m_extFlags |= ExtLaneFlags.DisplacedOuter;
                        lastDisplacedOuterBackward = sortedIndex;
                    }
                    if (direction != 0)
                        break;
                }

                // scan for Outer|ForwardGroup (may convert some to Inner later)
                for (sortedIndex = lastDisplacedOuterBackward - 1; sortedIndex > lastDisplacedOuterForward; sortedIndex--) {
                    var extLane = m_extLanes[m_sortedLanes[sortedIndex]];
                    var direction = extLane.m_extFlags & LaneGroupDirection;
                    if ((direction & ExtLaneFlags.ForwardGroup) != 0)
                        extLane.m_extFlags |= ExtLaneFlags.Outer;
                    if ((direction & ExtLaneFlags.BackwardGroup) != 0)
                        break;
                }

                // skip BackwardGroup
                for (; sortedIndex > lastDisplacedOuterForward; sortedIndex--) {
                    if ((m_extLanes[m_sortedLanes[sortedIndex]].m_extFlags & ExtLaneFlags.ForwardGroup) != 0)
                        break;
                }

                // scan for DisplacedInner|ForwardGroup
                for (; sortedIndex > lastDisplacedOuterForward; sortedIndex--) {
                    int laneIndex = m_sortedLanes[sortedIndex];
                    var extLane = m_extLanes[laneIndex];
                    if ((extLane.m_extFlags & ExtLaneFlags.ForwardGroup) != 0) {
                        var lane = lanes[laneIndex];
                        extLane.m_extFlags |= ExtLaneFlags.DisplacedInner;
                        if (lane.m_allowConnect)
                            extLane.m_extFlags |= ExtLaneFlags.ForbidControlledLanes;
                        else if (lane.m_laneType == NetInfo.LaneType.Vehicle)
                            extLane.m_extFlags |= ExtLaneFlags.AllowCFI;
                    }
                }

                // scan for Outer|BackwardGroup (may convert some to Inner later)
                for (sortedIndex = lastDisplacedOuterForward + 1; sortedIndex < lastDisplacedOuterBackward; sortedIndex++) {
                    var extLane = m_extLanes[m_sortedLanes[sortedIndex]];
                    var direction = extLane.m_extFlags & LaneGroupDirection;
                    if ((direction & ExtLaneFlags.BackwardGroup) != 0)
                        extLane.m_extFlags |= ExtLaneFlags.Outer;
                    if ((direction & ExtLaneFlags.ForwardGroup) != 0)
                        break;
                }

                // skip ForwardGroup
                for (; sortedIndex < lastDisplacedOuterBackward; sortedIndex++) {
                    if ((m_extLanes[m_sortedLanes[sortedIndex]].m_extFlags & ExtLaneFlags.BackwardGroup) != 0)
                        break;
                }

                // scan for DisplacedInner|BackwardGroup
                for (; sortedIndex < lastDisplacedOuterBackward; sortedIndex++) {
                    int laneIndex = m_sortedLanes[sortedIndex];
                    var extLane = m_extLanes[laneIndex];
                    if ((extLane.m_extFlags & ExtLaneFlags.BackwardGroup) != 0) {
                        var lane = lanes[laneIndex];
                        extLane.m_extFlags |= ExtLaneFlags.DisplacedInner;
                        if (lane.m_allowConnect)
                            extLane.m_extFlags |= ExtLaneFlags.ForbidControlledLanes;
                        else if (lane.IsCarLane())
                            extLane.m_extFlags |= ExtLaneFlags.AllowCFI;
                    }
                }

            }

            if (!(oneWay || inverted)) {

                // advance to first Outer|ForwardGroup

                float laneWidth = float.MaxValue;
                float laneEdge = float.MinValue;
                float laneElevation = float.MaxValue;
                float medianEdge = float.MinValue;
                float medianElevation = float.MinValue;

                int firstOuterLane;
                int firstInnerLane = -1;

                for (firstOuterLane = lastDisplacedOuterBackward - 1; firstOuterLane > lastDisplacedOuterForward; firstOuterLane--) {

                    var laneIndex = m_sortedLanes[firstOuterLane];
                    if ((m_extLanes[laneIndex].m_extFlags & ExtLaneFlags.ForwardGroup) != 0) {
                        var lane = lanes[laneIndex];
                        laneWidth = lane.m_width;
                        laneEdge = lane.m_position - laneWidth / 2;
                        laneElevation = lane.m_verticalOffset;
                        break;
                    }
                }

                // scan for forward median

                for (sortedIndex = firstOuterLane; sortedIndex > lastDisplacedOuterForward; sortedIndex--) {
                    var laneIndex = m_sortedLanes[sortedIndex];
                    var extLane = m_extLanes[laneIndex];
                    var lane = lanes[laneIndex];
                    if ((extLane.m_extFlags & ExtLaneFlags.ForwardGroup) != 0) {
                        if (((lane.m_position + lane.m_width / 2) <= medianEdge && lane.m_verticalOffset < medianElevation)
                                || (lane.m_position + lane.m_width / 2 + lane.m_width + laneWidth) <= laneEdge) {
                            firstInnerLane = sortedIndex;
                            break;
                        }
                        medianEdge = medianElevation = float.MinValue;
                        laneWidth = lane.m_width;
                        laneEdge = lane.m_position - laneWidth / 2;
                        laneElevation = lane.m_verticalOffset;
                    } else if ((extLane.m_extFlags & ExtLaneFlags.BackwardGroup) != 0) {
                        break;
                    } else {
                        if (!lane.IsRoadLane() && lane.m_verticalOffset > laneElevation && (lane.m_position + lane.m_width / 2) <= laneEdge) {
                            medianEdge = Math.Max(medianEdge, lane.m_position - lane.m_width / 2);
                            medianElevation = Math.Max(medianElevation, lane.m_verticalOffset);
                        }
                    }
                }

                if (firstInnerLane >= 0) {
                    // apply AllowServiceLane to forward lanes

                    for (sortedIndex = lastDisplacedOuterBackward - 1; sortedIndex > firstInnerLane; sortedIndex--) {
                        var laneIndex = m_sortedLanes[sortedIndex];
                        var extLane = m_extLanes[laneIndex];
                        if ((extLane.m_extFlags & ExtLaneFlags.OuterForward) == ExtLaneFlags.OuterForward && lanes[laneIndex].IsCarLane()) {
                            extLane.m_extFlags |= ExtLaneFlags.AllowServiceLane;
                        }
                    }

                    // apply Inner to forward lanes

                    for (; sortedIndex > lastDisplacedOuterForward; sortedIndex--) {
                        var laneIndex = m_sortedLanes[sortedIndex];
                        var extLane = m_extLanes[laneIndex];
                        if ((extLane.m_extFlags & ExtLaneFlags.OuterForward) == ExtLaneFlags.OuterForward) {
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

                // advance to first Outer|BackwardGroup

                laneWidth = float.MaxValue;
                laneEdge = float.MaxValue;
                laneElevation = float.MaxValue;
                medianEdge = float.MaxValue;
                medianElevation = float.MinValue;

                firstInnerLane = -1;

                for (firstOuterLane = lastDisplacedOuterForward + 1; firstOuterLane < lastDisplacedOuterBackward; firstOuterLane++) {

                    var laneIndex = m_sortedLanes[firstOuterLane];
                    if ((m_extLanes[laneIndex].m_extFlags & ExtLaneFlags.BackwardGroup) != 0) {
                        var lane = lanes[laneIndex];
                        laneWidth = lane.m_width;
                        laneEdge = lane.m_position + laneWidth / 2;
                        laneElevation = lane.m_verticalOffset;
                        break;
                    }
                }

                // scan for backward raised median

                for (sortedIndex = firstOuterLane; sortedIndex < lastDisplacedOuterBackward; sortedIndex++) {
                    var laneIndex = m_sortedLanes[sortedIndex];
                    var extLane = m_extLanes[laneIndex];
                    var lane = lanes[laneIndex];
                    if ((extLane.m_extFlags & ExtLaneFlags.BackwardGroup) != 0) {
                        if (((lane.m_position - lane.m_width / 2) >= medianEdge && lane.m_verticalOffset < medianElevation)
                                || (lane.m_position - lane.m_width / 2 - lane.m_width - laneWidth) >= laneEdge) {
                            firstInnerLane = sortedIndex;
                            break;
                        }
                        medianEdge = float.MaxValue;
                        medianElevation = float.MinValue;
                        laneWidth = lane.m_width;
                        laneEdge = lane.m_position + laneWidth / 2;
                        laneElevation = lane.m_verticalOffset;
                    } else if ((extLane.m_extFlags & ExtLaneFlags.ForwardGroup) != 0) {
                        break;
                    } else {
                        if (!lane.IsRoadLane() && lane.m_verticalOffset > laneElevation && (lane.m_position - lane.m_width / 2) >= laneEdge) {
                            medianEdge = Math.Min(medianEdge, lane.m_position + lane.m_width / 2);
                            medianElevation = Math.Max(medianElevation, lane.m_verticalOffset);
                        }
                    }
                }

                if (firstInnerLane >= 0) {
                    // apply AllowServiceLane to backward lanes

                    for (sortedIndex = lastDisplacedOuterForward + 1; sortedIndex < firstInnerLane; sortedIndex++) {
                        var laneIndex = m_sortedLanes[sortedIndex];
                        var extLane = m_extLanes[laneIndex];
                        if ((extLane.m_extFlags & ExtLaneFlags.OuterBackward) == ExtLaneFlags.OuterBackward
                                && lanes[laneIndex].IsCarLane()) {
                            extLane.m_extFlags |= ExtLaneFlags.AllowServiceLane;
                        }
                    }

                    // apply Inner to backward lanes

                    for (; sortedIndex < lastDisplacedOuterBackward; sortedIndex++) {
                        var laneIndex = m_sortedLanes[sortedIndex];
                        var extLane = m_extLanes[laneIndex];
                        if ((extLane.m_extFlags & ExtLaneFlags.OuterBackward) == ExtLaneFlags.OuterBackward) {
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

            m_laneGroups = Enumerable.Range(0, lanes.Length)
                            .Select(index => new { index, lane = lanes[index], extLane = m_extLanes[index] })
                            .GroupBy(l => l.extLane.m_extFlags & LaneGroupingKey)
                            .Where(g => g.Key != 0)
                            .Select(g => new LaneGroupInfo {
                                m_extLaneFlags = g.Select(l => l.extLane.m_extFlags).Aggregate((ExtLaneFlags x, ExtLaneFlags y) => x | y),
                                m_sortedLanes = g.OrderBy(g => g.lane.m_position).Select(g => g.index).ToArray(),
                            })
                            .OrderBy(lg => lanes[lg.m_sortedLanes[0]].m_position)
                            .ToArray();

            m_forwardExtLaneFlags = m_extLanes.Select(l => l.m_extFlags).Where(f => (f & ExtLaneFlags.ForwardGroup) != 0).DefaultIfEmpty().Aggregate((x, y) => x | y);
            m_backwardExtLaneFlags = m_extLanes.Select(l => l.m_extFlags).Where(f => (f & ExtLaneFlags.BackwardGroup) != 0).DefaultIfEmpty().Aggregate((x, y) => x | y);

            m_extLaneFlags = m_forwardExtLaneFlags | m_backwardExtLaneFlags;
        }

        internal class ExtLaneInfo {

            public ExtLaneFlags m_extFlags;

            public ExtLaneInfo(NetInfo.Lane lane) {
                if (lane.IsRoadLane()) {

                    switch (lane.m_direction) {
                        case NetInfo.Direction.Forward:
                        case NetInfo.Direction.AvoidBackward:
                            m_extFlags |= ExtLaneFlags.ForwardGroup;
                            break;

                        case NetInfo.Direction.Backward:
                        case NetInfo.Direction.AvoidForward:
                            m_extFlags |= ExtLaneFlags.BackwardGroup;
                            break;

                        case NetInfo.Direction.Both:
                        case NetInfo.Direction.AvoidBoth:
                            m_extFlags |= ExtLaneFlags.ForwardGroup | ExtLaneFlags.BackwardGroup;
                            break;
                    }
                }
            }
        }

        internal class LaneGroupInfo {

            /// <summary>
            /// Indices of the lanes in this group, sorted by position.
            /// </summary>
            public int[] m_sortedLanes;

            /// <summary>
            /// Aggregate lane flags.
            /// </summary>
            public ExtLaneFlags m_extLaneFlags;
        }
    }
}
