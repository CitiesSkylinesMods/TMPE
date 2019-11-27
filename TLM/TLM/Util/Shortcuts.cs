namespace TrafficManager.Util {
    using System;
    using ColossalFramework;
    using GenericGameBridge.Service;
    using TrafficManager.API.Manager;
    using TrafficManager.API.Traffic.Data;

    public static class Shortcuts {
        private static NetNode[] _nodeBuffer = Singleton<NetManager>.instance.m_nodes.m_buffer;

        private static NetSegment[] _segBuffer = Singleton<NetManager>.instance.m_segments.m_buffer;

        public static ref NetNode GetNode(ushort nodeId) => ref _nodeBuffer[nodeId];

        public static ref NetSegment GetSeg(ushort segmentId) => ref _segBuffer[segmentId];

        public static IExtSegmentEndManager segEndMan = Constants.ManagerFactory.ExtSegmentEndManager;

        public static IExtSegmentManager segMan = Constants.ManagerFactory.ExtSegmentManager;

        public static ref ExtSegmentEnd GetSegEnd(ushort segmentId, ushort nodeId) =>
            ref segEndMan.ExtSegmentEnds[segEndMan.GetIndex(segmentId, nodeId)];

        public static ref ExtSegmentEnd GetSegEnd(ushort segmentId, bool startNode) =>
            ref segEndMan.ExtSegmentEnds[segEndMan.GetIndex(segmentId, startNode)];

        public static INetService netService = Constants.ServiceFactory.NetService;

        public static bool HasJunctionFlag(ushort nodeId) => HasJunctionFlag(ref GetNode(nodeId));

        public static bool HasJunctionFlag(ref NetNode node) =>
            (node.m_flags & NetNode.Flags.Junction) != NetNode.Flags.None;

        public static Func<bool, int> Int = (bool b) => b ? 1 : 0;
    }
}
