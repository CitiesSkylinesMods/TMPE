namespace TrafficManager.UI.SubTools.LaneArrows {
    using System.Collections.Generic;
    using System.Security.Policy;
    using ColossalFramework;
    using CSUtil.Commons;

    /// <summary>
    /// For all allowed ways (Left, Forward, Right, and possibly U-turn) to leave the node.
    /// Stores the collection of segment ids grouped by directions.
    /// </summary>
    public struct OutgoingTurnsCollection {
        private readonly ushort currentNodeId_;
        private readonly ushort currentSegmentId_;

        /// <summary>
        /// Outgoing segments, grouped by direction
        /// </summary>
        public readonly Dictionary<ArrowDirection, HashSet<ushort>> AllTurns;

        /// <summary>
        /// Outgoing lanes for each outgoing segment
        /// </summary>
        private Dictionary<ushort, HashSet<uint>> lanes_;

        public OutgoingTurnsCollection(ushort nodeId, ushort segmentId) {
            currentNodeId_ = nodeId;
            currentSegmentId_ = segmentId;
            AllTurns = new Dictionary<ArrowDirection, HashSet<ushort>>();
            lanes_ = new Dictionary<ushort, HashSet<uint>>();
        }

        /// <summary>
        /// Insert an outgoing segment, group by outgoing direction
        /// </summary>
        /// <param name="dir">Direction for leaving this node</param>
        /// <param name="segmentId">Segment for leaving this node</param>
        public void AddTurn(ArrowDirection dir, ushort segmentId) {
            if (segmentId == currentSegmentId_) {
                return;
            }

            if (!AllTurns.ContainsKey(dir)) {
                AllTurns.Add(dir, new HashSet<ushort>());
            }

            AllTurns[dir].Add(segmentId);

            // Extract outgoing lane ids and store them as NetLane's
            var segmentBuffer = Singleton<NetManager>.instance.m_segments.m_buffer;
            var segment = segmentBuffer[segmentId];

            // The other end of the segment, for getting incoming lanes into it
            var otherNodeId = segment.m_startNode == currentNodeId_
                                  ? segment.m_endNode
                                  : segment.m_startNode;

            if (!lanes_.ContainsKey(segmentId)) {
                lanes_.Add(segmentId, new HashSet<uint>());
            }

            foreach (var ln in LaneArrowTool.GetIncomingLaneList(segmentId, otherNodeId)) {
                lanes_[segmentId].Add(ln.laneId);
            }
        }

        public bool Contains(ArrowDirection dir) {
            return AllTurns.ContainsKey(dir);
        }

        /// <summary>
        /// For node flags, group together all outgoing lanes except the one that we remember.
        /// </summary>
        /// <param name="flags">Given combination of turn bits, extract lanes for those turns</param>
        /// <returns>Set of lane ids</returns>
        public Dictionary<ArrowDirection, HashSet<uint>> GetLanesFor(NetLane.Flags flags) {
            var resultLeft = new HashSet<uint>();
            var resultForward = new HashSet<uint>();
            var resultRight = new HashSet<uint>();

            if ((flags & NetLane.Flags.Left) != 0 && AllTurns.ContainsKey(ArrowDirection.Left)) {
                foreach (var outgoingSegmentId in AllTurns[ArrowDirection.Left]) {
                    if (!lanes_.ContainsKey(outgoingSegmentId)) {
                        continue;
                    }

                    resultLeft.UnionWith(lanes_[outgoingSegmentId]);
                }
            }

            if ((flags & NetLane.Flags.Right) != 0 && AllTurns.ContainsKey(ArrowDirection.Right)) {
                foreach (var outgoingSegmentId in AllTurns[ArrowDirection.Right]) {
                    if (!lanes_.ContainsKey(outgoingSegmentId)) {
                        continue;
                    }

                    resultRight.UnionWith(lanes_[outgoingSegmentId]);
                }
            }

            if ((flags & NetLane.Flags.Forward) != 0 && AllTurns.ContainsKey(ArrowDirection.Forward)) {
                foreach (var outgoingSegmentId in AllTurns[ArrowDirection.Forward]) {
                    if (!lanes_.ContainsKey(outgoingSegmentId)) {
                        continue;
                    }

                    resultForward.UnionWith(lanes_[outgoingSegmentId]);
                }
            }

            var result = new Dictionary<ArrowDirection, HashSet<uint>>();
            result.Add(ArrowDirection.Left, resultLeft);
            result.Add(ArrowDirection.Forward, resultForward);
            result.Add(ArrowDirection.Right, resultRight);
            return result;
        }
    }
}