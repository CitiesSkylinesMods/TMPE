namespace TrafficManager.Manager.Impl.LaneConnectionManagerData {
    using CSUtil.Commons;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using TrafficManager.Util;
    using TrafficManager.Util.Extensions;
    using static TrafficManager.Util.Shortcuts;
    internal struct LaneConnectionData {
        public uint LaneId;
        // TODO [issue 354]: add track/car connection type.

        public LaneConnectionData(uint laneId) {
            LaneId = laneId;
        }

        public override string ToString() => $"TargetConnectionData({LaneId})";
    }

    /// <summary>
    /// All calls to this class assumes lanes are valid
    /// </summary>
    internal class ConnectionData {
        private SourceLaneConnectionData[] connections_;

        public ConnectionData() {
            connections_ = new SourceLaneConnectionData[NetManager.instance.m_lanes.m_size];
        }

        private struct SourceLaneConnectionData {
            public LaneConnectionData[] StartConnections;
            public LaneConnectionData[] EndConnections;

            public IEnumerable<LaneConnectionData[]> BothConnections() {
                yield return StartConnections;
                yield return EndConnections;
            }
        }

        internal ref LaneConnectionData[] GetConnections(uint laneId, ushort nodeId) {
            return ref GetConnections(laneId, laneId.ToLane().IsStartNode(nodeId));
        }

        internal ref LaneConnectionData[] GetConnections(uint laneId, bool startNode) {
            if(startNode)
                return ref connections_[laneId].StartConnections;
            else
                return ref connections_[laneId].EndConnections;
        }

        internal bool IsConnectedTo(uint sourceLaneId, uint targetLaneId, ushort nodeId) =>
            IsConnectedTo(sourceLaneId, targetLaneId, sourceLaneId.ToLane().IsStartNode(nodeId));

        /// <param name="sourceStartNode">start node for the segment of the source lane</param>
        internal bool IsConnectedTo(uint sourceLaneId, uint targetLaneId, bool sourceStartNode) {
            var targets = GetConnections(sourceLaneId, sourceStartNode);
            int n = targets?.Length ?? 0;
            for(int i = 0; i < n; ++i) {
                if(targets[i].LaneId == targetLaneId) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Adds a connection from source to target lane at the give node
        /// </summary>
        /// <param name="sourceStartNode">start node for the segment of the source lane</param>
        internal void ConnectTo(uint sourceLaneId, uint targetLaneId, bool sourceStartNode) {
            ref var targets = ref GetConnections(sourceLaneId, sourceStartNode);
            int n = targets?.Length ?? 0;
            for(int i = 0; i < n; ++i) {
                if(targets[i].LaneId == targetLaneId) {
                    return; // existing connection
                }
            }

            targets = targets.AppendOrCreate(new LaneConnectionData(targetLaneId));
        }

        /// <summary>
        /// Adds a connection from source to target lane at the give node
        /// </summary>
        internal void ConnectTo(uint sourceLaneId, uint targetLaneId, ushort nodeId) =>
            ConnectTo(sourceLaneId, targetLaneId, sourceLaneId.ToLane().IsStartNode(nodeId));

        /// <summary>removes the connection from source to target lane at the given node</summary>
        internal bool Disconnect(uint sourceLaneId, uint targetLaneId, ushort nodeId) =>
            Disconnect(sourceLaneId, targetLaneId, sourceLaneId.ToLane().IsStartNode(nodeId));

        /// <summary>removes the connection from source to target lane at the given node</summary>
        /// <param name="sourceStartNode">start node for the segment of the source lane</param>
        internal bool Disconnect(uint sourceLaneId, uint targetLaneId, bool sourceStartNode) {
            bool ret = false;
            ref var targets = ref GetConnections(sourceLaneId, sourceStartNode);
            if(targets != null) {
                for(int i = 0; i < targets.Length; ++i) {
                    if(targets[i].LaneId == targetLaneId) {
                        targets[i].LaneId = 0;
                        ret = true;
                    }
                }

                var newConnections = targets.Where(item => item.LaneId != 0).DefaultIfEmpty();
                targets = newConnections?.ToArray();
            }
            return ret;
        }

        /// <summary>
        /// removes all connections from and to the given lane.
        /// </summary>
        internal void RemoveConnections(uint laneId) {
            connections_[laneId].StartConnections = connections_[laneId].EndConnections = null;
            for(uint laneId2 = 1; laneId2 < connections_.Length; ++laneId2) {
                Disconnect(sourceLaneId: laneId2, targetLaneId: laneId, sourceStartNode: true);
                Disconnect(sourceLaneId: laneId2, targetLaneId: laneId, sourceStartNode: false);
            }
        }

        [Conditional("DEBUG")]
        internal void PrintDebugInfo() {
            Log.Info("------------------------");
            Log.Info("--- LANE CONNECTIONS ---");
            Log.Info("------------------------");
            for(uint sourceLaneId = 0; sourceLaneId < connections_.Length; ++sourceLaneId) {
                ref NetLane netLane = ref sourceLaneId.ToLane();

                ushort segmentId = netLane.m_segment;
                ref NetSegment netSegment = ref segmentId.ToSegment();

                Log.Info($"Lane {sourceLaneId}: valid? {netLane.IsValidWithSegment()}, seg. valid? {netSegment.IsValid()}");

                foreach(bool startNode in new bool[] { false, true }) {
                    var targets = GetConnections(sourceLaneId, startNode);
                    if(targets == null)
                        continue;

                    ushort nodeId = netSegment.GetNodeId(startNode);
                    ref NetNode netNode = ref nodeId.ToNode();
                    Log.Info($"\tstartNode:{startNode} ({nodeId}, seg. {segmentId}): valid? {netNode.IsValid()}");

                    for(int i = 0; i < targets.Length; ++i) {
                        var target = targets[i];
                        ref NetLane netLaneOfConnection = ref target.LaneId.ToLane();
                        Log.Info($"\t\tEntry {i}: {target} (valid? {netLaneOfConnection.IsValidWithSegment()})");
                    }
                }
            }
        }
    }
}
