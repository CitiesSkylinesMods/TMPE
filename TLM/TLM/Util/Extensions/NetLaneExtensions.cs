namespace TrafficManager.Util.Extensions {
    public static class NetLaneExtensions {
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
            netLane.m_segment.ToSegment().IsStartnode(nodeId);
    }
}
