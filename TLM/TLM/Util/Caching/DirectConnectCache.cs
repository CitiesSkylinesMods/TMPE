namespace TrafficManager.Util.Caching {
    using TrafficManager.Util;
    using TrafficManager.Manager.Impl;

    using static TrafficManager.Util.Shortcuts;
    using CSUtil.Commons;

    internal class DirectConnectCache {
        private byte[] connections_;

        internal DirectConnectCache(ushort segmentId, ushort nodeId) {
            Recalculate(segmentId, nodeId);
        }

        public bool GetShouldConnect(int segmentIDX, int nodeInfoIDX) =>
            connections_[nodeInfoIDX].GetBit(segmentIDX);

        private void SetShouldConnect(int segmentIDX, int nodeInfoIDX, bool value) =>
            connections_[nodeInfoIDX].SetBit(segmentIDX, value);

        internal void Recalculate(ushort segmentId, ushort nodeId) {
            var m_nodes = segmentId.ToSegment().Info.m_nodes;
            if (connections_ == null || connections_.Length != m_nodes.Length) {
                connections_ = new byte[m_nodes.Length];
            }
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

        private static DirectConnectCache[] DirectConnectArray_;
        private static uint[] segmentBuildIndexes_;

        internal static void Init() {
            segmentBuildIndexes_ = new uint[NetManager.MAX_SEGMENT_COUNT];
            DirectConnectArray_ = new DirectConnectCache[NetManager.MAX_SEGMENT_COUNT * 2];

        }

        internal static void Load() {
            for (ushort segmentId = 0; segmentId < NetManager.MAX_SEGMENT_COUNT; ++segmentId) {
                OnUpdateSegment(segmentId);
            }
        }

        internal static void Relase() {
            DirectConnectArray_ = null;
            segmentBuildIndexes_ = null;
        }

        public static void OnUpdateSegment(ushort segmentId) {
            //Log._Debug("POINT B");
            if (segmentBuildIndexes_[segmentId] == segmentId.ToSegment().m_buildIndex) {
                //Log._Debug($"KIAN DEBUG> build index did not change for segmentId:${segmentId} " +
                //    $"cached:{segmentBuildIndexes_[segmentId]} actual:{segmentId.ToSegment().m_buildIndex}");
                return;
            }
            Log._Debug($"KIAN DEBUG> Updating segmentId:${segmentId} new build index is {segmentId.ToSegment().m_buildIndex}");
            segmentBuildIndexes_[segmentId] = segmentId.ToSegment().m_buildIndex;
            Assert(DirectConnectArray_ != null);
            foreach (var startNode in new[] { false, true }) {
                int index = ExtSegmentEndManager.Instance.GetIndex(segmentId, startNode);
                ushort nodeId = netService.GetSegmentNodeId(segmentId, startNode);
                if (!netService.IsSegmentValid(segmentId) ||
                    !netService.IsNodeValid(nodeId) ||
                    nodeId.ToNode().m_connectCount <= 2) {
                    DirectConnectArray_[index] = null;
                }
                else if (DirectConnectArray_[index] != null) {
                    DirectConnectArray_[index].Recalculate(segmentId, nodeId);
                } else {
                    DirectConnectArray_[index] = new DirectConnectCache(segmentId, nodeId);
                }
                Log._Debug($"KIAN DEBUG> Segment updated: segmentId:${segmentId}-startNode:{startNode}");
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
            OnUpdateSegment(sourceSegmentId);
            Assert(DirectConnectArray_ != null, "SegmentEndArray!=null");
            Assert(DirectConnectArray_?[index] != null, "SegmentEndArray!=null");
            return DirectConnectArray_?[index]?.GetShouldConnect(targetSegmentIDX, nodeInfoIDX) ?? true;
        }
    } // end class
} // end namespace