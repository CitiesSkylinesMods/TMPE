namespace TrafficManager.Manager.Impl.LaneConnectionManagerData {
    using CSUtil.Commons;
    using System.Collections.Generic;
    using System.Diagnostics;
    using TrafficManager.Util;
    using TrafficManager.Util.Extensions;

    internal struct LaneConnectionData {
        public uint LaneId;
        /// <summary>
        /// for every connection, both forward and backward connection pairs are created.
        /// for bi-directional connection both forward and backward are enabled.
        /// for uni-directional connection only forward connection is enabled.
        /// if there is no connection either way, then there should be no LaneConnectionData entry.
        /// </summary>
        public bool Enabled; 

        // TODO [issue 354]: add track/car connection type.

        public LaneConnectionData(uint laneId, bool enabled) {
            LaneId = laneId;
            Enabled = enabled;
        }

        public override string ToString() => $"LaneConnectionData({LaneId} ,{Enabled})";
    }

    /// <summary>
    /// All calls to this class assumes lanes are valid
    /// </summary>
    internal class ConnectionData {
        private Dictionary<LaneEnd, LaneConnectionData[]> connections_;

        public ConnectionData() {
            connections_ = new ();
        }

        private struct LaneEnd {
            internal uint LaneId;
            internal bool StartNode;

            public LaneEnd(uint laneId, bool startNode) {
                LaneId = laneId;
                StartNode = startNode;
            }
            public LaneEnd(uint laneId, ushort nodeId) {
                LaneId = laneId;
                StartNode = laneId.ToLane().IsStartNode(nodeId);
            }
        }

        internal bool IsConnectedTo(uint sourceLaneId, uint targetLaneId, ushort nodeId) =>
            IsConnectedTo(sourceLaneId, targetLaneId, sourceLaneId.ToLane().IsStartNode(nodeId));

        /// <param name="sourceStartNode">start node for the segment of the source lane</param>
        internal bool IsConnectedTo(uint sourceLaneId, uint targetLaneId, bool sourceStartNode) {
            var targets = GetConnections(sourceLaneId, sourceStartNode);
            int n = targets?.Length ?? 0;
            for(int i = 0; i < n; ++i) {
                if(targets[i].Enabled && targets[i].LaneId == targetLaneId) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Adds a connection from source to target lane at the give node
        /// </summary>
        internal void ConnectTo(uint sourceLaneId, uint targetLaneId, ushort nodeId) {
            AddConnection(sourceLaneId, targetLaneId, nodeId, true); //forward
            AddConnection(targetLaneId, sourceLaneId, nodeId, false); //backward
        }

        /// <summary>
        /// makes a uni-directional connection
        /// </summary>
        /// <param name="enable"><c>true</c> for forward/bi-directional connection,
        /// <c>false</c> for backward connection</param>
        private void AddConnection(uint sourceLaneId, uint targetLaneId, ushort nodeId, bool enable) {
            var key = new LaneEnd(sourceLaneId, nodeId);
            {
                if (connections_.TryGetValue(key, out var targets)) {
                    int n = targets.Length;
                    for (int i = 0; i < n; ++i) {
                        if (targets[i].LaneId == targetLaneId) {
                            // a uni-directional connection already exist
                            targets[i].Enabled |= enable;
                            return;
                        }
                    }
                }
            }

            {
                var newConnection = new LaneConnectionData(targetLaneId, enable);
                if (connections_.TryGetValue(key, out var targets)) {
                    connections_[key] = targets.Append(newConnection);
                } else {
                    connections_[key] = new[] { newConnection };
                }
            }
        }

        /// <summary>removes the connection from source to target lane at the given node</summary>
        internal bool Disconnect(uint sourceLaneId, uint targetLaneId, ushort nodeId) {
            // if backward connection exists (uni-directional) then just disable the connection.
            // otherwise delete both connections.
            bool backward = IsConnectedTo(targetLaneId, sourceLaneId, nodeId);
            if (backward) {
                return DisableConnection(sourceLaneId, targetLaneId, nodeId);
            } else {
               RemoveConnection(targetLaneId, sourceLaneId, nodeId);
               return RemoveConnection(sourceLaneId, targetLaneId, nodeId);
            }
        }

        /// <summary>
        /// disables a single connection
        /// </summary>
        /// <returns><c>true</c> if any connection was disabled. <c>false</c> otherwise. </returns>
        private bool DisableConnection(uint sourceLaneId, uint targetLaneId, ushort nodeId) {
            
            ref var targets = ref GetConnections(sourceLaneId, nodeId);
            if (targets != null) {
                for (int i = 0; i < targets.Length; ++i) {
                    if (targets[i].LaneId == targetLaneId) {
                        bool connectionExisted = targets[i].Enabled;
                        targets[i].Enabled = false;
                        return connectionExisted;
                    }
                }
            }

            return false;
        }

        private bool RemoveConnection(uint sourceLaneId, uint targetLaneId, ushort nodeId) {
            bool sourceStartNode = sourceLaneId.ToLane().IsStartNode(nodeId);
            return RemoveConnection(sourceLaneId, targetLaneId, sourceStartNode);
        }

        /// <summary>
        /// removes a single connection
        /// </summary>
        /// <param name="sourceStartNode">start node for the segment of the source lane</param>
        /// <returns><c>true</c> if any active connection was disabled. <c>false</c> otherwise. </returns>
        private bool RemoveConnection(uint sourceLaneId, uint targetLaneId, bool sourceStartNode) {
            ref var targets = ref GetConnections(sourceLaneId, sourceStartNode);
            if (targets == null)
                return false;

            bool ret = false;
            var newConnections = new List<LaneConnectionData>(targets.Length);
            for (int i = 0; i < targets.Length; ++i) {
                if (targets[i].LaneId != targetLaneId) {
                    newConnections.Add(targets[i]);
                } else {
                    ret = targets[i].Enabled;
                }
            }

            if (newConnections.Count == 0)
                targets = null;
            else
                targets = newConnections.ToArray();

            return ret;
        }
        
        /// <summary>
        /// removes all connections from and to the given lane.
        /// </summary>
        internal void RemoveConnections(uint laneId) {
            /*********************************
             * remove connections to laneId *
             *********************************/
            if (connections_[laneId].StartConnections != null) {
                foreach (var connection in connections_[laneId].StartConnections) {
                    uint laneId2 = connection.LaneId;
                    ushort nodeId = laneId.ToLane().GetNodeId(true /* StartConnections */); 
                    RemoveConnection(laneId2, laneId, nodeId);
                }
            }

            if (connections_[laneId].EndConnections != null) {
                foreach (var connection in connections_[laneId].EndConnections) {
                    uint laneId2 = connection.LaneId;
                    ushort nodeId = laneId.ToLane().GetNodeId(false /* EndConnections */);
                    RemoveConnection(laneId2, laneId, nodeId);
                }
            }

            /*********************************
             * remove connections from laneId *
             *********************************/
            connections_[laneId].StartConnections = connections_[laneId].EndConnections = null;
        }

        [Conditional("DEBUG")]
        internal void PrintDebugInfo() {
            for(uint sourceLaneId = 0; sourceLaneId < connections_.Length; ++sourceLaneId) {
                ref NetLane netLane = ref sourceLaneId.ToLane();

                ushort segmentId = netLane.m_segment;
                ref NetSegment netSegment = ref segmentId.ToSegment();

                if(GetConnections(sourceLaneId, false) == null && GetConnections(sourceLaneId, true) == null) {
                    continue;
                } 

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
