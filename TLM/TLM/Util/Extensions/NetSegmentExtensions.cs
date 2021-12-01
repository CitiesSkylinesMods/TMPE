namespace TrafficManager.Util.Extensions {
    public static class NetSegmentExtensions {
        /// <summary>
        /// Checks if the netSegment is Created, but neither Collapsed nor Deleted.
        /// </summary>
        /// <param name="netSegment">netSegment</param>
        /// <returns>True if the netSegment is valid, otherwise false.</returns>
        public static bool IsValid(this ref NetSegment netSegment) =>
            netSegment.m_flags.CheckFlags(
                required: NetSegment.Flags.Created,
                forbidden: NetSegment.Flags.Collapsed | NetSegment.Flags.Deleted);

        /// <returns><c>true</c> if nodeId is start node.
        /// <c>false</c> if nodeId is end node.
        /// Undetermined if segment does not have nodeId</returns>
        public static bool IsStartnode(this ref NetSegment netSegment, ushort nodeId) =>
            netSegment.m_startNode == nodeId;
    }
}
