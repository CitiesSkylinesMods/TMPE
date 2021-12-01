namespace TrafficManager.API.Manager {
    public interface ILaneConnectionManager {
        // TODO define me!

        public NetInfo.LaneType LaneTypes { get; }

        public VehicleInfo.VehicleType VehicleTypes { get; }

        /// <summary>
        /// Determines whether u-turn connections exist for the given segment end.
        /// </summary>
        /// <param name="segmentId">segment id</param>
        /// <param name="startNode">at start node?</param>
        /// <returns><code>true</code> if u-turn connections exist, <code>false</code> otherwise.</returns>
        bool HasUturnConnections(ushort segmentId, bool startNode);

        /// <summary>
        /// Determines if there exist custom lane connections at the specified node.
        /// </summary>
        bool HasNodeConnections(ushort nodeId);

        /// <summary>
        /// Determines if the given lane has outgoing connections.
        /// </summary>
        /// <param name="startNode">start node for the segment of the lane.</param>
        public bool HasConnections(uint sourceLaneId, bool startNode);

        /// <summary>
        /// Checks if traffic may flow from source lane to target lane according to setup lane connections
        /// </summary>
        /// <param name="sourceStartNode">check at start node of the segment of the source lane?</param>
        public bool AreLanesConnected(uint sourceLaneId, uint targetLaneId, bool sourceStartNode);
    }
}