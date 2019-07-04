namespace TrafficManager.UI.SubTools.LaneArrows {
    using System.Collections.Generic;
    using ColossalFramework;
    using CSUtil.Commons;

    /// <summary>
    /// For all allowed ways (Left, Forward, Right, and possibly U-turn) to leave the node.
    /// Stores the collection of segment ids grouped by directions.
    /// </summary>
    public struct PossibleTurnsOut {
        private readonly ushort currentNodeId_;
        private readonly ushort currentSegmentId_;

        /// <summary>
        /// Outgoing segments, grouped by direction
        /// </summary>
        private readonly Dictionary<ArrowDirection, HashSet<ushort>> allTurns_;

        /// <summary>
        /// Outgoing lanes for each outgoing segment
        /// </summary>
        private Dictionary<ushort, HashSet<uint>> lanes_;

        public PossibleTurnsOut(ushort nodeId, ushort segmentId) {
            currentNodeId_ = nodeId;
            currentSegmentId_ = segmentId;
            allTurns_ = new Dictionary<ArrowDirection, HashSet<ushort>>();
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

            if (!allTurns_.ContainsKey(dir)) {
                allTurns_.Add(dir, new HashSet<ushort>());
            }

            allTurns_[dir].Add(segmentId);

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
            return allTurns_.ContainsKey(dir);
        }

        /// <summary>
        /// For node flags, group together all outgoing lanes except the one that we remember.
        /// </summary>
        /// <param name="flags">Given combination of turn bits, extract lanes for those turns</param>
        /// <returns>Set of lane ids</returns>
        public HashSet<uint> GetLanesFor(NetLane.Flags flags) {
            var result = new HashSet<uint>();
            if ((flags & NetLane.Flags.Left) != 0 && allTurns_.ContainsKey(ArrowDirection.Left)) {
                foreach (var outgoingSegmentId in allTurns_[ArrowDirection.Left]) {
                    if (!lanes_.ContainsKey(outgoingSegmentId)) {
                        continue;
                    }

                    result.UnionWith(lanes_[outgoingSegmentId]);
                }
            }

            if ((flags & NetLane.Flags.Right) != 0 && allTurns_.ContainsKey(ArrowDirection.Right)) {
                foreach (var outgoingSegmentId in allTurns_[ArrowDirection.Right]) {
                    if (!lanes_.ContainsKey(outgoingSegmentId)) {
                        continue;
                    }

                    result.UnionWith(lanes_[outgoingSegmentId]);
                }
            }

            if ((flags & NetLane.Flags.Forward) != 0 && allTurns_.ContainsKey(ArrowDirection.Forward)) {
                foreach (var outgoingSegmentId in allTurns_[ArrowDirection.Forward]) {
                    if (!lanes_.ContainsKey(outgoingSegmentId)) {
                        continue;
                    }

                    result.UnionWith(lanes_[outgoingSegmentId]);
                }
            }

            return result;
        }
    }
}