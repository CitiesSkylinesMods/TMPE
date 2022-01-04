namespace TrafficManager.Util.Extensions {
    using ColossalFramework;
    public static class NetSegmentExtensions {
        private static NetSegment[] _segBuffer = Singleton<NetManager>.instance.m_segments.m_buffer;

        public static ref NetSegment ToSegment(this ushort segmentId) => ref _segBuffer[segmentId];

        public static ushort GetNodeId(this ref NetSegment segment, bool startNode) =>
            startNode ? segment.m_startNode : segment.m_endNode;

        public static ushort GetHeadNode(this ref NetSegment segment) {
            // tail node>-------->head node
            bool invert = (segment.m_flags & NetSegment.Flags.Invert) != NetSegment.Flags.None;
            invert = invert ^ (Singleton<SimulationManager>.instance.m_metaData.m_invertTraffic == SimulationMetaData.MetaBool.True);
            if (invert) {
                return segment.m_startNode;
            } else {
                return segment.m_endNode;
            }
        }

        public static ushort GetTailNode(this ref NetSegment segment) {
            bool invert = (segment.m_flags & NetSegment.Flags.Invert) != NetSegment.Flags.None;
            invert = invert ^ (Singleton<SimulationManager>.instance.m_metaData.m_invertTraffic == SimulationMetaData.MetaBool.True);
            if (!invert) {
                return segment.m_startNode;
            } else {
                return segment.m_endNode;
            }//endif
        }

        public static bool? IsStartNode(this ref NetSegment netSegment, ushort nodeId) {
            if (netSegment.m_startNode == nodeId) {
                return true;
            } else if (netSegment.m_endNode == nodeId) {
                return false;
            } else {
                return null;
            }
        }

        /// <summary>
        /// Checks if the netSegment is Created, but neither Collapsed nor Deleted.
        /// </summary>
        /// <param name="netSegment">netSegment</param>
        /// <returns>True if the netSegment is valid, otherwise false.</returns>
        public static bool IsValid(this ref NetSegment netSegment) =>
            netSegment.m_flags.CheckFlags(
                required: NetSegment.Flags.Created,
                forbidden: NetSegment.Flags.Collapsed | NetSegment.Flags.Deleted);
    }
}
