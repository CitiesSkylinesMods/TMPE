namespace TrafficManager.Util {
    using CSUtil.Commons;
    using System.Collections.Generic;
    using System;
    using TrafficManager.API.Manager;
    using TrafficManager.API.Traffic.Data;
    using TrafficManager.Manager.Impl;
    using TrafficManager.Util.Extensions;

    public class SegmentTraverser {
        [Flags]
        public enum TraverseDirection {
            None = 0,
            Incoming = 1,
            Outgoing = 1 << 1,
            AnyDirection = Incoming | Outgoing,
        }

        [Flags]
        public enum TraverseSide {
            None = 0,
            Left = 1,
            Straight = 1 << 1,
            Right = 1 << 2,
            AnySide = Left | Straight | Right,
        }

        public delegate bool SegmentVisitor(SegmentVisitData data);

        [Flags]
        public enum SegmentStopCriterion {
            /// <summary>
            /// Traversal stops when the whole network has been visited
            /// </summary>
            None = 0,

            /// <summary>
            /// Traversal stops when a node with more than two connected segments has been reached
            /// </summary>
            Junction = 1,
        }

        public class SegmentVisitData {
            /// <summary>
            /// Previously visited ext. segment
            /// </summary>
            public ExtSegment PrevSeg;

            /// <summary>
            /// Current ext. segment
            /// </summary>
            public ExtSegment CurSeg;

            /// <summary>
            /// If true the current segment geometry has been reached on a path via the initial segment's start node
            /// </summary>
            public bool ViaInitialStartNode;

            /// <summary>
            /// If true the current segment geometry has been reached on a path via the current segment's start node
            /// </summary>
            public bool ViaStartNode;

            /// <summary>
            /// If true this is the initial segment
            /// </summary>
            public bool Initial;

            public SegmentVisitData(ref ExtSegment prevSeg,
                                    ref ExtSegment curSeg,
                                    bool viaInitialStartNode,
                                    bool viaStartNode,
                                    bool initial) {
                PrevSeg = prevSeg;
                CurSeg = curSeg;
                ViaInitialStartNode = viaInitialStartNode;
                ViaStartNode = viaStartNode;
                Initial = initial;
            }

            public NetInfo CurSegmentInfo => CurSeg.segmentId.ToSegment().Info;

            /// <summary>
            /// Determines if input segment is in reverse direction WRT initial segment.
            /// takes into account invert flag and start/end nodes.
            /// </summary>
            /// <param name="initialSegmentId"></param>
            /// <returns></returns>
            public bool IsReversed(ushort initialSegmentId) {
                if (Initial) {
                    return false;
                }
                bool reverse = ViaStartNode == ViaInitialStartNode;
                bool invert1 = CurSeg.segmentId.ToSegment().m_flags.IsFlagSet(NetSegment.Flags.Invert);
                bool invert2 = initialSegmentId.ToSegment().m_flags.IsFlagSet(NetSegment.Flags.Invert);
                return reverse ^ invert1 ^ invert2;
            }
        }

        /// <summary>
        /// Performs a Depth-First traversal over the cached segment geometry structure. At each
        /// traversed segment, the given `visitor` is notified. It then can update the current `state`.
        /// </summary>
        /// <param name="initialSegmentGeometry">Specifies the segment at which the traversal
        ///     should start.</param>
        /// <param name="nextNodeIsStartNode">Specifies if the next node to traverse is the start
        ///     node of the initial segment.</param>
        /// <param name="direction">Specifies if traffic should be able to flow towards the initial
        ///     segment (Incoming) or should be able to flow from the initial segment (Outgoing) or
        ///     in both directions (Both).</param>
        /// <param name="maximumDepth">Specifies the maximum depth to explore. At a depth of 0, no
        ///     segment will be traversed (event the initial segment will be omitted).</param>
        /// <param name="visitorFun">Specifies the stateful visitor that should be notified as soon as
        ///     a traversable segment (which has not been traversed before) is found.</param>
        public static IEnumerable<ushort> Traverse(ushort initialSegmentId,
                                    TraverseDirection direction,
                                    TraverseSide side,
                                    SegmentStopCriterion stopCrit,
                                    SegmentVisitor visitorFun)
        {
            ExtSegment initialSeg = Constants.ManagerFactory.ExtSegmentManager.ExtSegments[initialSegmentId];
            if (!initialSeg.valid) {
                return null;
            }

            // Log._Debug($"SegmentTraverser: Traversing initial segment {initialSegmentId}");
            if (!visitorFun(
                    new SegmentVisitData(
                        ref initialSeg,
                        ref initialSeg,
                        false,
                        false,
                        true))) {
                return null;
            }

            HashSet<ushort> visitedSegmentIds = new HashSet<ushort>();
            visitedSegmentIds.Add(initialSegmentId);

            IExtSegmentEndManager extSegEndMan = Constants.ManagerFactory.ExtSegmentEndManager;

            ref NetSegment initialSegment = ref initialSegmentId.ToSegment();

            ushort startNodeId = initialSegment.m_startNode;
            TraverseRec(
                ref initialSeg,
                ref extSegEndMan.ExtSegmentEnds[extSegEndMan.GetIndex(initialSegmentId, true)],
                ref startNodeId.ToNode(),
                true,
                direction,
                side,
                stopCrit,
                visitorFun,
                visitedSegmentIds);

            ushort endNodeId = initialSegment.m_endNode;
            TraverseRec(
                ref initialSeg,
                ref extSegEndMan.ExtSegmentEnds[extSegEndMan.GetIndex(initialSegmentId, false)],
                ref endNodeId.ToNode(),
                false,
                direction,
                side,
                stopCrit,
                visitorFun,
                visitedSegmentIds);

            // Log._Debug($"SegmentTraverser: Traversal finished.");
            return visitedSegmentIds;
        }

        private static void TraverseRec(ref ExtSegment prevSeg,
                                        ref ExtSegmentEnd prevSegEnd,
                                        ref NetNode node,
                                        bool viaInitialStartNode,
                                        TraverseDirection direction,
                                        TraverseSide side,
                                        SegmentStopCriterion stopCrit,
                                        SegmentVisitor visitorFun,
                                        HashSet<ushort> visitedSegmentIds)
        {
            // Log._Debug($"SegmentTraverser: Traversing segment {prevSegEnd.segmentId}");
            // collect next segment ids to traverse
            if (direction == TraverseDirection.None) {
                throw new ArgumentException($"Invalid direction {direction} given.");
            }

            if (side == TraverseSide.None) {
                throw new ArgumentException($"Invalid side {side} given.");
            }

            ExtSegmentManager extSegmentManager = ExtSegmentManager.Instance;
            IExtSegmentEndManager extSegEndMan = Constants.ManagerFactory.ExtSegmentEndManager;

            HashSet<ushort> nextSegmentIds = new HashSet<ushort>();
            for (int segmentIndex = 0; segmentIndex < Constants.MAX_SEGMENTS_OF_NODE; ++segmentIndex) {
                ushort nextSegmentId = node.GetSegment(segmentIndex);
                if (nextSegmentId == 0 || nextSegmentId == prevSegEnd.segmentId) {
                    continue;
                }

                bool nextIsStartNode = nextSegmentId.ToSegment().IsStartNode(prevSegEnd.nodeId);
                ExtSegmentEnd nextSegEnd =
                    extSegEndMan.ExtSegmentEnds[extSegEndMan.GetIndex(nextSegmentId, nextIsStartNode)];

                if (direction == TraverseDirection.AnyDirection ||
                    (direction == TraverseDirection.Incoming && nextSegEnd.incoming) ||
                    (direction == TraverseDirection.Outgoing && nextSegEnd.outgoing))
                {
                    if (side == TraverseSide.AnySide) {
                        nextSegmentIds.Add(nextSegmentId);
                    } else {
                        ArrowDirection dir = extSegEndMan.GetDirection(
                            ref prevSegEnd,
                            nextSegmentId);
                        if (((side & TraverseSide.Left) != TraverseSide.None &&
                             dir == ArrowDirection.Left) ||
                            ((side & TraverseSide.Straight) != TraverseSide.None &&
                             dir == ArrowDirection.Forward) ||
                            ((side & TraverseSide.Right) != TraverseSide.None &&
                             dir == ArrowDirection.Right)) {
                            nextSegmentIds.Add(nextSegmentId);
                        }
                    }
                }
            }

            nextSegmentIds.Remove(0);
            // Log._Debug($"SegmentTraverser: Fetched next segments to traverse:
            //     {nextSegmentIds.CollectionToString()}");
            if (nextSegmentIds.Count >= 2 && (stopCrit & SegmentStopCriterion.Junction) !=
                SegmentStopCriterion.None) {
                // Log._Debug($"SegmentTraverser: Stop criterion reached @ {prevSegEnd.segmentId}:
                //    {nextSegmentIds.Count} connected segments");
                return;
            }

            // explore next segments
            foreach (ushort nextSegmentId in nextSegmentIds) {
                if (visitedSegmentIds.Contains(nextSegmentId)) {
                    continue;
                }

                visitedSegmentIds.Add(nextSegmentId);

                // Log._Debug($"SegmentTraverser: Traversing segment {nextSegmentId}");
                ushort nextStartNodeId = nextSegmentId.ToSegment().m_startNode;

                if (!visitorFun(
                        new SegmentVisitData(
                            ref prevSeg,
                            ref extSegmentManager.ExtSegments[nextSegmentId],
                            viaInitialStartNode,
                            prevSegEnd.nodeId == nextStartNodeId,
                            false))) {
                    continue;
                }

                bool nextNodeIsStartNode = nextStartNodeId != prevSegEnd.nodeId;

                ExtSegmentEnd nextSegEnd
                    = extSegEndMan.ExtSegmentEnds[extSegEndMan.GetIndex( nextSegmentId, nextNodeIsStartNode)];

                TraverseRec(
                    ref extSegmentManager.ExtSegments[nextSegmentId],
                    ref nextSegEnd,
                    ref nextSegEnd.nodeId.ToNode(),
                    viaInitialStartNode,
                    direction,
                    side,
                    stopCrit,
                    visitorFun,
                    visitedSegmentIds);
            } // end foreach
        }
    } // end class
}
