namespace TrafficManager.Util.Caching {
    using TrafficManager.Util;
    using TrafficManager.Manager.Impl;

    using static TrafficManager.Util.Shortcuts;

    internal class DirectConnectCache {
        internal readonly ushort SegmentID;
        internal readonly ushort NodeID;
        internal NetInfo Info { get; private set; }

        private byte[] connections_;

        internal DirectConnectCache(ushort segmentId, ushort nodeId) {
            SegmentID = segmentId;
            NodeID = nodeId;
            Recalculate();
        }

        public bool GetShouldConnect(int segmentIDX, int nodeInfoIDX) =>
            connections_[(int)nodeInfoIDX].GetBit(segmentIDX);

        private void SetShouldConnect(int segmentIDX, int nodeInfoIDX, bool value) =>
            connections_[nodeInfoIDX].SetBit(segmentIDX, value);

        internal void Recalculate() {
            Info = SegmentID.ToSegment().Info;
            if (connections_?.Length != Info.m_nodes.Length) {
                connections_ = new byte[Info.m_nodes.Length];
            }
            for (int segmentIDX = 0; segmentIDX < 8; ++segmentIDX) {
                ushort targetSegmentId = NodeID.ToNode().GetSegment(segmentIDX);
                for (int nodeInfoIDX = 0; nodeInfoIDX < Info.m_nodes.Length; ++nodeInfoIDX) {
                    bool connect = DirectConnectUtil.HasDirectConnect(
                        SegmentID,
                        targetSegmentId,
                        NodeID,
                        nodeInfoIDX);
                    SetShouldConnect(segmentIDX, nodeInfoIDX, connect);
                }
            }
        }

        private static DirectConnectCache[] DirectConnectArray_;

        private static void Init() {
            DirectConnectArray_ = new DirectConnectCache[NetManager.MAX_SEGMENT_COUNT * 2];
            for (ushort segmentId = 0; segmentId < NetManager.MAX_SEGMENT_COUNT; ++segmentId) {
                OnUpdateSegment(segmentId);
            }
        }

        internal static void Relase() {
            DirectConnectArray_ = null;
        }

        public static void OnUpdateSegment(ushort segmentId) {
            if (DirectConnectArray_ == null) {
                Init();
            }
            foreach (var startNode in new[] { false, true }) {
                int index = ExtSegmentEndManager.Instance.GetIndex(segmentId, startNode);
                if (!netService.IsSegmentValid(segmentId)) {
                    DirectConnectArray_[index] = null;
                } else {
                    ushort nodeId = startNode ? segmentId.ToSegment().m_startNode : segmentId.ToSegment().m_endNode;
                    if (DirectConnectArray_[index] != null) {
                        DirectConnectArray_[index].Recalculate();
                    } else {
                        DirectConnectArray_[index] = new DirectConnectCache(segmentId, nodeId);
                    }
                }
            }
        }

        public static bool GetShouldConnectTracks(
            ushort sourceSegmentId,
            int targetSegmentIDX,
            ushort nodeId,
            int nodeInfoIDX) {
            bool? startNode = (bool)netService.IsStartNode(sourceSegmentId, nodeId);
            Assert(startNode != null, "startNode != null");
            int index = ExtSegmentEndManager.Instance.GetIndex(sourceSegmentId, (bool)startNode);
            return DirectConnectArray_?[index]?.GetShouldConnect(targetSegmentIDX, nodeInfoIDX) ?? true;
        }
    } // end class
} // end namespace