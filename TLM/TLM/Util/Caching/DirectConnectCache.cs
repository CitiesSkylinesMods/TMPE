namespace TrafficManager.Util.Caching {
    using TrafficManager.Util;
    using TrafficManager.Manager.Impl;

    using static TrafficManager.Util.Shortcuts;
    using CSUtil.Commons;
    using TrafficManager.State.ConfigData;

    internal class DirectConnectCache {
        static bool verbose_ => DebugSwitch.LaneConnections.Get();
        struct ConnectionData {
            private byte[] connections_;

            internal void Clear() => connections_ = null;

            internal ConnectionData(ushort segmentId, ushort nodeId) {
                var m_nodes = segmentId.ToSegment().Info.m_nodes;
                connections_ = new byte[m_nodes.Length];
                RecalculateHelper(segmentId, nodeId);
            }

            public bool GetShouldConnect(int segmentIDX, int nodeInfoIDX) =>
                connections_?[nodeInfoIDX].GetBit(segmentIDX) ?? true;

            private void SetShouldConnect(int segmentIDX, int nodeInfoIDX, bool value) =>
                connections_[nodeInfoIDX].SetBit(segmentIDX, value);

            internal void Recalculate(ushort segmentId, ushort nodeId) {
                var m_nodes = segmentId.ToSegment().Info.m_nodes;
                if (connections_ == null || connections_.Length != m_nodes.Length) {
                    Log._Debug($"Allocating new connections_ memory for {segmentId} {nodeId}");
                    connections_ = new byte[m_nodes.Length];
                }
                Recalculate(segmentId, nodeId);
            }

            private void RecalculateHelper(ushort segmentId, ushort nodeId) {
                var m_nodes = segmentId.ToSegment().Info.m_nodes;
                for (int segmentIDX = 0; segmentIDX < 8; ++segmentIDX) {
                    ushort targetSegmentId = nodeId.ToNode().GetSegment(segmentIDX);
                    for (int nodeInfoIDX = 0; nodeInfoIDX < m_nodes.Length; ++nodeInfoIDX) {
                        bool connect = DirectConnectUtil.HasDirectConnect(
                            segmentId,
                            targetSegmentId,
                            nodeId,
                            nodeInfoIDX);
                        SetShouldConnect(segmentIDX, nodeInfoIDX, connect);
                    }
                }
            }
        }

        private static ConnectionData[] DirectConnectArray_;
        private static uint[] segmentBuildIndexes_;

        internal static void Init() {
            segmentBuildIndexes_ = new uint[NetManager.MAX_SEGMENT_COUNT];
            DirectConnectArray_ = new ConnectionData[NetManager.MAX_SEGMENT_COUNT * 2];
        }

        internal static void Load() {
            Log._Debug("Loading DirectConnectCache ... ");
            for (ushort segmentId = 0; segmentId < NetManager.MAX_SEGMENT_COUNT; ++segmentId) {
                foreach (var startNode in Constants.ALL_BOOL ) {
                    ushort nodeId = netService.GetSegmentNodeId(segmentId, startNode);
                    OnUpdateSegmentEnd(segmentId,nodeId, startNode);
                }
            }
            Log._Debug("DirectConnectCache loaded.");

        }

        internal static void Relase() {
            DirectConnectArray_ = null;
            segmentBuildIndexes_ = null;
        }

        public static void OnUpdateSegmentEnd(ushort segmentId, ushort nodeId, bool startNode) {
            int index = ExtSegmentEndManager.Instance.GetIndex(segmentId, startNode);
            if (!netService.IsNodeValid(nodeId) ||
                nodeId.ToNode().m_connectCount <= 2 ||
                !netService.IsSegmentValid(segmentId) ||
                !segmentId.ToSegment().Info.m_requireDirectRenderers) {
                DirectConnectArray_[index].Clear();
            } else {
                DirectConnectArray_[index].Recalculate(segmentId, nodeId);
            }
        }

        public static void OnUpdateSegment(ushort segmentId) {
            Assert(DirectConnectArray_ != null);
            foreach (var startNode in Constants.ALL_BOOL) {
                ushort nodeId = netService.GetSegmentNodeId(segmentId, startNode);
                OnUpdateNode(nodeId);
            }
        }

        public static void OnUpdateNode(ushort nodeId) {
            Assert(DirectConnectArray_ != null, "DirectConnectArray_ != null");
            for(int segmentIDX = 0; segmentIDX < 8; ++segmentIDX) {
                ushort segmentId = nodeId.ToNode().GetSegment(segmentIDX);
                if (segmentId == 0)
                    continue;
                bool ?startNode = netService.IsStartNode(segmentId, nodeId);
                Assert(startNode != null, "startNode!=null");
                OnUpdateSegmentEnd(segmentId, nodeId, (bool)startNode);
            }
        }

        public static bool GetShouldConnectTracks(
            ushort sourceSegmentId,
            int targetSegmentIDX,
            ushort nodeId,
            int nodeInfoIDX) {
            if (InSimulationThread()) {
                Log._Debug("System.Environment.StackTrace");
                return true;
            }
            bool? startNode = netService.IsStartNode(sourceSegmentId, nodeId);
            Assert(startNode != null, "startNode != null");
            int index = ExtSegmentEndManager.Instance.GetIndex(sourceSegmentId, (bool)startNode);
            Assert(DirectConnectArray_ != null, "DirectConnectArray_!=null");
            return DirectConnectArray_?[index].GetShouldConnect(targetSegmentIDX, nodeInfoIDX) ?? true;
        }
    } // end class
} // end namespace