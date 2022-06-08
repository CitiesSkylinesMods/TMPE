using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.Util.Extensions;

namespace TrafficManager.ExtPrefabs {
    internal class ExtNetInfo : ExtPrefabInfo<ExtNetInfo, NetInfo> {

        [Flags]
        public enum ExtLaneFlags {

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

            /// <summary>
            /// Mask to obtain the lane grouping key.
            /// </summary>
            LaneGroupingKey = Outer | Inner | DisplacedInner | DisplacedOuter | ForwardGroup | BackwardGroup,

            /// <summary>
            /// Mask to test a segment for the presence of service lanes.
            /// </summary>
            ServiceLaneRule = AllowServiceLane | ForbidControlledLanes,

            /// <summary>
            /// Mask to test a segment for the presence of express lanes.
            /// </summary>
            ExpressLaneRule = AllowExpressLane | ForbidControlledLanes,

            /// <summary>
            /// Mask to test a segment for the presence of displaced far turn lanes.
            /// </summary>
            CFIRule = AllowCFI | ForbidControlledLanes,

            /// <summary>
            /// Mask to test the prevailing direction of a lane group.
            /// </summary>
            GroupDirection = ForwardGroup | BackwardGroup,

            /// <summary>
            /// Mask to test a segment for the presence of any displaced lanes.
            /// </summary>
            Displaced = DisplacedInner | DisplacedOuter,
        }

        public ExtLaneInfo[] m_extLanes;

        public LaneGroupInfo[] m_laneGroups;

        public int[] m_sortedLanes;

        public ExtLaneFlags m_forwardExtFlags;
        public ExtLaneFlags m_backwardExtFlags;

        public ExtLaneFlags m_extFlags;

        public ExtNetInfo(NetInfo info)
            : this(info.m_lanes) {
        }

        public ExtNetInfo(NetInfo.Lane[] lanes) {

            int GetDirectionalSortKey(ExtLaneFlags flags) {
                switch (flags & ExtLaneFlags.GroupDirection) {
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

            // bypass displaced scans for conventional, inverted, and one-way roads
            if (oneWay || inverted || maxBackward <= minForward) {
                for (int i = 0; i < m_extLanes.Length; i++)
                    m_extLanes[i].m_extFlags |= ExtLaneFlags.Outer;
            } else {

                var lastDisplacedOuterBackward = -1;
                var lastDisplacedOuterForward = lanes.Length;

                int si;

                // scan for DisplacedOuter|ForwardGroup
                for (si = 0; si < lanes.Length; si++) {
                    var direction = m_extLanes[m_sortedLanes[si]].m_extFlags & ExtLaneFlags.GroupDirection;
                    if ((direction & ExtLaneFlags.ForwardGroup) != 0)
                        lastDisplacedOuterForward = si;
                    if (direction != 0)
                        break;
                }

                // scan for DisplacedOuter|BackwardGroup
                for (si = lanes.Length - 1; si >= 0; si--) {
                    var direction = m_extLanes[m_sortedLanes[si]].m_extFlags & ExtLaneFlags.GroupDirection;
                    if ((direction & ExtLaneFlags.BackwardGroup) != 0)
                        lastDisplacedOuterBackward = si;
                    if (direction != 0)
                        break;
                }

                // scan for Outer|ForwardGroup (may convert some to Inner later)
                for (si = lastDisplacedOuterBackward - 1; si > lastDisplacedOuterForward; si--) {
                    var extLane = m_extLanes[m_sortedLanes[si]];
                    var direction = extLane.m_extFlags & ExtLaneFlags.GroupDirection;
                    if ((direction & ExtLaneFlags.ForwardGroup) != 0)
                        extLane.m_extFlags |= ExtLaneFlags.Outer;
                    if ((direction & ExtLaneFlags.BackwardGroup) != 0)
                        break;
                }

                // skip BackwardGroup
                for (; si > lastDisplacedOuterForward; si--) {
                    if ((m_extLanes[m_sortedLanes[si]].m_extFlags & ExtLaneFlags.ForwardGroup) != 0)
                        break;
                }

                // scan for DisplacedInner|ForwardGroup
                for (; si > lastDisplacedOuterForward; si--) {
                    int i = m_sortedLanes[si];
                    var extLane = m_extLanes[i];
                    if ((extLane.m_extFlags & ExtLaneFlags.ForwardGroup) != 0) {
                        var lane = lanes[i];
                        extLane.m_extFlags |= ExtLaneFlags.DisplacedInner;
                        if (lane.m_allowConnect)
                            extLane.m_extFlags |= ExtLaneFlags.ForbidControlledLanes;
                        else if (lane.m_laneType == NetInfo.LaneType.Vehicle)
                            extLane.m_extFlags |= ExtLaneFlags.AllowCFI;
                    }
                }

                // scan for Outer|BackwardGroup (may convert some to Inner later)
                for (si = lastDisplacedOuterForward + 1; si < lastDisplacedOuterBackward; si++) {
                    var extLane = m_extLanes[m_sortedLanes[si]];
                    var direction = extLane.m_extFlags & ExtLaneFlags.GroupDirection;
                    if ((direction & ExtLaneFlags.BackwardGroup) != 0)
                        extLane.m_extFlags |= ExtLaneFlags.Outer;
                    if ((direction & ExtLaneFlags.ForwardGroup) != 0)
                        break;
                }

                // skip ForwardGroup
                for (; si < lastDisplacedOuterBackward; si++) {
                    if ((m_extLanes[m_sortedLanes[si]].m_extFlags & ExtLaneFlags.BackwardGroup) != 0)
                        break;
                }

                // scan for DisplacedInner|BackwardGroup
                for (; si < lastDisplacedOuterBackward; si++) {
                    int i = m_sortedLanes[si];
                    var extLane = m_extLanes[i];
                    if ((extLane.m_extFlags & ExtLaneFlags.BackwardGroup) != 0) {
                        var lane = lanes[i];
                        extLane.m_extFlags |= ExtLaneFlags.DisplacedInner;
                        if (lane.m_allowConnect)
                            extLane.m_extFlags |= ExtLaneFlags.ForbidControlledLanes;
                        else if (lane.m_laneType == NetInfo.LaneType.Vehicle)
                            extLane.m_extFlags |= ExtLaneFlags.AllowCFI;
                    }
                }

                var forwardFlags = m_extLanes.Select(l => l.m_extFlags).Where(f => (f & ExtLaneFlags.ForwardGroup) != 0).Aggregate((x, y) => x | y);
                var backwardFlags = m_extLanes.Select(l => l.m_extFlags).Where(f => (f & ExtLaneFlags.BackwardGroup) != 0).Aggregate((x, y) => x | y);

                // TODO: Scan medians and express lanes here

                // if applicable, set Outer|ForwardGroup|AllowServiceLane
                if ((forwardFlags & ExtLaneFlags.DisplacedInner) != 0) {
                    for (int i = 0; i < m_extLanes.Length; i++) {
                        var extLane = m_extLanes[i];
                        const ExtLaneFlags matchingFlags = ExtLaneFlags.Outer | ExtLaneFlags.ForwardGroup;
                        if ((extLane.m_extFlags & matchingFlags) == matchingFlags)
                            extLane.m_extFlags |= ExtLaneFlags.AllowServiceLane;
                    }
                }

                // if applicable, set Outer|BackwardGroup|AllowServiceLane
                if ((backwardFlags & ExtLaneFlags.DisplacedInner) != 0) {
                    for (int i = 0; i < m_extLanes.Length; i++) {
                        var extLane = m_extLanes[i];
                        const ExtLaneFlags matchingFlags = ExtLaneFlags.Outer | ExtLaneFlags.BackwardGroup;
                        if ((extLane.m_extFlags & matchingFlags) == matchingFlags)
                            extLane.m_extFlags |= ExtLaneFlags.AllowServiceLane;
                    }
                }
            }

            m_laneGroups = Enumerable.Range(0, lanes.Length)
                            .Select(index => new { index, lane = lanes[index], extLane = m_extLanes[index] })
                            .GroupBy(l => l.extLane.m_extFlags & ExtLaneFlags.LaneGroupingKey)
                            .Where(g => g.Key != 0)
                            .Select(g => new LaneGroupInfo {
                                m_extFlags = g.Select(l => l.extLane.m_extFlags).Aggregate((ExtLaneFlags x, ExtLaneFlags y) => x | y),
                                m_sortedLanes = g.OrderBy(g => g.lane.m_position).Select(g => g.index).ToArray(),
                            })
                            .OrderBy(lg => lanes[lg.m_sortedLanes[0]].m_position)
                            .ToArray();

            m_forwardExtFlags = m_extLanes.Select(l => l.m_extFlags).Where(f => (f & ExtLaneFlags.ForwardGroup) != 0).Aggregate((x, y) => x | y);
            m_backwardExtFlags = m_extLanes.Select(l => l.m_extFlags).Where(f => (f & ExtLaneFlags.BackwardGroup) != 0).Aggregate((x, y) => x | y);

            m_extFlags = m_forwardExtFlags | m_backwardExtFlags;
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

            public int[] m_sortedLanes;

            public ExtLaneFlags m_extFlags;
        }
    }
}
