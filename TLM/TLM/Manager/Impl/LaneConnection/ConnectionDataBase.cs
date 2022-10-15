namespace TrafficManager.Manager.Impl.LaneConnection {
    using CSUtil.Commons;
    using System.Collections.Generic;
    using System.Diagnostics;
    using TrafficManager.Util;
    using TrafficManager.Util.Extensions;

    internal class ConnectionDataBase : Dictionary<LaneEnd, LaneConnectionData[]> {
        public ConnectionDataBase() : base(LaneEnd.LaneIdStartNodeComparer) { }

        internal bool IsConnectedTo(uint sourceLaneId, uint targetLaneId, ushort nodeId) =>
            IsConnectedTo(sourceLaneId, targetLaneId, sourceLaneId.ToLane().IsStartNode(nodeId));

        /// <param name="sourceStartNode">start node for the segment of the source lane</param>
        internal bool IsConnectedTo(uint sourceLaneId, uint targetLaneId, bool sourceStartNode) {
            LaneEnd key = new(sourceLaneId, sourceStartNode);
            if (this.TryGetValue(key, out var targets)) {
                int n = targets.Length;
                for (int i = 0; i < n; ++i) {
                    if (targets[i].Enabled && targets[i].LaneId == targetLaneId) {
                        return true;
                    }
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
            LaneEnd key = new(sourceLaneId, nodeId);

            bool hasConnections = this.TryGetValue(key, out var targets);

            if (hasConnections) {
                int n = targets.Length;
                for (int i = 0; i < n; ++i) {
                    if (targets[i].LaneId == targetLaneId) {
                        // a uni-directional connection already exist
                        targets[i].Enabled |= enable;
                        return;
                    }
                }
            }

            // no such lane connection exists yet. create new connection.
            var newConnection = new LaneConnectionData(targetLaneId, enable);
            if (hasConnections) {
                this[key] = targets.Append(newConnection);
            } else {
                this[key] = new[] { newConnection };
            }
        }

        /// <summary>removes the connection from source to target lane at the given node</summary>
        internal bool Disconnect(uint sourceLaneId, uint targetLaneId, ushort nodeId) {
            if(sourceLaneId == targetLaneId) {
                return RemoveConnection(sourceLaneId, targetLaneId, nodeId);
            }

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
            LaneEnd key = new(sourceLaneId, nodeId);
            if (this.TryGetValue(key, out var targets)) {
                int n = targets.Length;
                for (int i = 0; i < n; ++i) {
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
            bool ret = false;
            LaneEnd key = new(sourceLaneId, sourceStartNode);
            if (this.TryGetValue(key, out var targets)) {
                int n = targets.Length;
                var newConnections = new List<LaneConnectionData>(n);
                for (int i = 0; i < n; ++i) {
                    if (targets[i].LaneId != targetLaneId) {
                        newConnections.Add(targets[i]);
                    } else {
                        ret = targets[i].Enabled;
                    }
                }

                if (newConnections.Count == 0)
                    this.Remove(key);
                else
                    this[key] = newConnections.ToArray();
            }

            return ret;
        }

        /// <summary>
        /// removes all connections from and to the given lane.
        /// </summary>
        internal void RemoveConnections(uint laneId) {
            var laneStart = new LaneEnd(laneId, true);
            var laneEnd = new LaneEnd(laneId, false);
            if(this.TryGetValue(laneStart,out var startConnections)) {
                foreach (var connection in startConnections) {
                    uint laneId2 = connection.LaneId;
                    ushort nodeId = laneId.ToLane().GetNodeId(startNode: true); /* StartConnections */
                    RemoveConnection(laneId2, laneId, nodeId);
                }
                this.Remove(laneStart);
            }

            if (this.TryGetValue(laneEnd, out var endConnections)) {
                foreach (var connection in endConnections) {
                    uint laneId2 = connection.LaneId;
                    ushort nodeId = laneId.ToLane().GetNodeId(startNode: false); /* EndConnections */
                    RemoveConnection(laneId2, laneId, nodeId);
                }
                this.Remove(laneEnd);
            }
        }

        internal void ResetConnectionsDatabase() {
            this.Clear();
        }

        [Conditional("DEBUG")]
        internal void PrintDebugInfo() {
            for(uint sourceLaneId = 0; sourceLaneId < NetManager.instance.m_laneCount; ++sourceLaneId) {
                ref NetLane netLane = ref sourceLaneId.ToLane();

                ushort segmentId = netLane.m_segment;
                ref NetSegment netSegment = ref segmentId.ToSegment();

                var laneStart = new LaneEnd(sourceLaneId, true);
                var laneEnd = new LaneEnd(sourceLaneId, false);
                if (this.ContainsKey(laneStart) || this.ContainsKey(laneEnd)) {

                    Log.Info($"Lane {sourceLaneId}: valid? {netLane.IsValidWithSegment()}, seg. valid? {netSegment.IsValid()}");

                    foreach (bool startNode in new bool[] { false, true }) {
                        LaneEnd key = new(sourceLaneId, startNode);
                        if (this.TryGetValue(key, out var targets)) {
                            ushort nodeId = netSegment.GetNodeId(startNode);
                            ref NetNode netNode = ref nodeId.ToNode();
                            Log.Info($"\tstartNode:{startNode} ({nodeId}, seg. {segmentId}): valid? {netNode.IsValid()}");
                            for (int i = 0; i < targets.Length; ++i) {
                                var target = targets[i];
                                ref NetLane netLaneOfConnection = ref target.LaneId.ToLane();
                                Log.Info($"\t\tEntry {i}: {target} (valid? {netLaneOfConnection.IsValidWithSegment()})");
                            }
                        }
                    }
                }
            }
        }
    }
}
