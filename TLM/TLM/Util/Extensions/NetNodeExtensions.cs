namespace TrafficManager.Util.Extensions {
    using ColossalFramework;
    public static class NetNodeExtensions {
        private static NetNode[] _nodeBuffer = Singleton<NetManager>.instance.m_nodes.m_buffer;

        internal static ref NetNode ToNode(this ushort nodeId) => ref _nodeBuffer[nodeId];
        internal static bool IsJunction(this ref NetNode netNode) => netNode.m_flags.IsFlagSet(NetNode.Flags.Junction);
        internal static bool IsMiddle(this ref NetNode netNode) => netNode.m_flags.IsFlagSet(NetNode.Flags.Middle);

        /// <summary>
        /// Checks if the netNode is Created, but neither Collapsed nor Deleted.
        /// </summary>
        /// <param name="netNode">netNode</param>
        /// <returns>True if the netNode is valid, otherwise false.</returns>
        public static bool IsValid(this ref NetNode netNode) =>
            netNode.m_flags.CheckFlags(
                required: NetNode.Flags.Created,
                forbidden: NetNode.Flags.Collapsed | NetNode.Flags.Deleted);

        internal static bool IsUnderground(this ref NetNode netNode) =>
            netNode.m_flags.IsFlagSet(NetNode.Flags.Underground);
    }
}
