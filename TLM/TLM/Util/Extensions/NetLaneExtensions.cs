namespace TrafficManager.Util.Extensions {
    using ColossalFramework;

    public static class NetLaneExtensions {
        private static NetLane[] _laneBuffer = Singleton<NetManager>.instance.m_lanes.m_buffer;

        internal static ref NetLane ToLane(this uint laneId) => ref _laneBuffer[laneId];

        /// <summary>
        /// Checks if the netLane is Created, but not Deleted.
        /// </summary>
        /// <param name="netLane">netLane</param>
        /// <returns>True if the netLane is valid, otherwise false.</returns>
        public static bool IsValid(this ref NetLane netLane) =>
            ((NetLane.Flags)netLane.m_flags).CheckFlags(
                required: NetLane.Flags.Created,
                forbidden: NetLane.Flags.Deleted);

        /// <summary>
        /// Checks if the netLane is Created, but not Deleted and if its netSegment is Created, but neither Collapsed nor Deleted.
        /// </summary>
        /// <param name="netLane">netLane</param>
        /// <returns>True if the netLane and its netSegment is valid, otherwise false.</returns>
        public static bool IsValidWithSegment(this ref NetLane netLane) {
            return netLane.IsValid()
                && netLane.m_segment.ToSegment().IsValid();
        }

        public static bool IsStartNode(this ref NetLane netLane, ushort nodeId) =>
            netLane.m_segment.ToSegment().IsStartNode(nodeId);

        public static ushort GetNodeId(this ref NetLane netLane, bool startNode) =>
            netLane.m_segment.ToSegment().GetNodeId(startNode);
    }
}
