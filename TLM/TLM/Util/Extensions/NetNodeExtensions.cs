namespace TrafficManager.Util.Extensions {
    public static class NetNodeExtensions {
        /// <summary>
        /// Checks if the netNode is Created, but neither Collapsed nor Deleted.
        /// </summary>
        /// <param name="netNode">netNode</param>
        /// <returns>True if the netNode is valid, otherwise false.</returns>
        public static bool IsValid(this ref NetNode netNode) =>
            netNode.m_flags.CheckFlags(
                required: NetNode.Flags.Created,
                forbidden: NetNode.Flags.Collapsed | NetNode.Flags.Deleted);
    }
}
