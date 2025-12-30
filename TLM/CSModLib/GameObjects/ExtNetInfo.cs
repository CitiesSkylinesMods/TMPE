using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CSModLib.GameObjects {
    public partial class ExtNetInfo : ExtPrefabInfo<ExtNetInfo, NetInfo> {

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

        public LaneConfiguration m_roadLaneConfiguration;

        public ExtNetInfo(NetInfo info)
            : this(info.m_lanes) {
        }

        public enum LaneConfiguration {
            Undefined = 0,
            TwoWay = 1 << 0,
            OneWay = 1 << 1,
            Inverted = 1 << 2,
            Complex = 1 << 3,

            InvertedOneWay = OneWay | Inverted,
        }

        private const LaneConfiguration GeneralTwoWayConfiguration = LaneConfiguration.TwoWay | LaneConfiguration.Complex;

        public ExtNetInfo(NetInfo.Lane[] lanes) {

            m_extLanes = lanes.Select(l => new ExtLaneInfo(l)).ToArray();

            var inspector = new LaneInspector(this, lanes);

            inspector.CalculateSortedLanes();

            m_roadLaneConfiguration = inspector.GetLaneConfiguration();

            var lastDisplacedOuterBackward = lanes.Length;
            var lastDisplacedOuterForward = -1;

            if (m_roadLaneConfiguration != LaneConfiguration.Complex) {
                for (int i = 0; i < m_extLanes.Length; i++) {
                    if (lanes[i].IsRoadLane())
                        m_extLanes[i].m_extFlags |= ExtLaneFlags.Outer;
                }
            } else {

                inspector.FindDisplacedOuter(ExtLaneFlags.ForwardGroup, ref lastDisplacedOuterForward);
                inspector.FindDisplacedOuter(ExtLaneFlags.BackwardGroup, ref lastDisplacedOuterBackward);

                inspector.FindOuterAndDisplacedInner(ExtLaneFlags.ForwardGroup);
                inspector.FindOuterAndDisplacedInner(ExtLaneFlags.BackwardGroup);
            }

            if ((m_roadLaneConfiguration & GeneralTwoWayConfiguration) != 0) {

                inspector.FindInner(ExtLaneFlags.ForwardGroup);
                inspector.FindInner(ExtLaneFlags.BackwardGroup);
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

        public class ExtLaneInfo {

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

        public class LaneGroupInfo {

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
