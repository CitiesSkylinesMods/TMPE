namespace TrafficManager.Manager.Impl {
    using CSUtil.Commons;
    using TrafficManager.Manager;
    using TrafficManager.Util;
    using TrafficManager.State.ConfigData;
    using static TrafficManager.Util.Shortcuts;

    internal class DirectConnectCacheManger
        : AbstractCustomManager {
        public static DirectConnectCacheManger Instance { get; }

        static DirectConnectCacheManger() {
            Instance = new DirectConnectCacheManger();
        }

        static bool verbose_ = false;
        struct ConnectionData {
            private byte[] connections_;

            internal void Clear() {
                if(verbose_ && connections_ != null)
                    Log._Debug("Released memomry");
                connections_ = null;
            }

            public bool GetShouldConnect(int segmentIDX, int nodeInfoIDX) =>
                connections_?[nodeInfoIDX].GetBit(segmentIDX) ?? true;

            private void SetShouldConnect(int segmentIDX, int nodeInfoIDX, bool value) =>
                connections_[nodeInfoIDX].SetBit(segmentIDX, value);

            internal void Recalculate(ushort segmentId, ushort nodeId) {
                var m_nodes = segmentId.ToSegment().Info.m_nodes;
                if (connections_ == null || connections_.Length != m_nodes.Length) {
                    if(verbose_)
                        Log._Debug($"Allocating new connections_ memory for {segmentId} {nodeId}");
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
        }

        private ConnectionData[] DirectConnectArray_;

        public override void OnBeforeLoadData() {
            DirectConnectArray_ = new ConnectionData[NetManager.MAX_SEGMENT_COUNT * 2];
        }

        public override void OnLevelLoading() {
            // TODO is this necessary? doesn't the game update all segments anyway?
            for (ushort segmentId = 0; segmentId < NetManager.MAX_SEGMENT_COUNT; ++segmentId) {
                foreach (var startNode in Constants.ALL_BOOL ) {
                    ushort nodeId = netService.GetSegmentNodeId(segmentId, startNode);
                    OnUpdateSegmentEnd(segmentId, nodeId, startNode);
                }
            }
            verbose_ = true;
        }
        
        public override void OnReleased() {
            base.OnReleased();
            DirectConnectArray_ = null; // This should be done LONG after detours have been reverted to prevent race conditions.
        }

        public void OnUpdateSegment(ushort segmentId) {
            Assert(DirectConnectArray_ != null);
            foreach (var startNode in Constants.ALL_BOOL) {
                ushort nodeId = netService.GetSegmentNodeId(segmentId, startNode);
                OnUpdateSegmentEnd(segmentId, nodeId, startNode);
                // No need to call OnUpdateNode because the the game traverses through connected nodes and segment by itself.
            }
        }

        public void OnUpdateNode(ushort nodeId) {
            AssertNotNull(DirectConnectArray_,"DirectConnectArray_");
            for(int segmentIDX = 0; segmentIDX < 8; ++segmentIDX) {
                ushort segmentId = nodeId.ToNode().GetSegment(segmentIDX);
                if (segmentId == 0)
                    continue;
                bool ?startNode = netService.IsStartNode(segmentId, nodeId);
                AssertNotNull(startNode, "startNode");
                OnUpdateSegmentEnd(segmentId, nodeId, (bool)startNode);
            }
        }

        public void OnUpdateSegmentEnd(ushort segmentId, ushort nodeId, bool startNode) {
            if(verbose_)
                Log._Debug($"OnUpdateSegmentEnd({segmentId},{nodeId},{startNode}) called.\n");
            int index = Constants.ManagerFactory.ExtSegmentEndManager.GetIndex(segmentId, startNode);
            if (ShouldCache(segmentId, nodeId)) {
                DirectConnectArray_[index].Recalculate(segmentId, nodeId);
            } else {
                DirectConnectArray_[index].Clear();
            }
        }

        public static bool ShouldCache(ushort segmentId, ushort nodeId) {
            if (verbose_) {
#pragma warning disable
                Log._Debug($"ShouldCache({segmentId},{nodeId}):" +
                    " isNodeValid=" + netService.IsNodeValid(nodeId) +
                    " m_connectCount=" + nodeId.ToNode().m_connectCount +
                    " IsSegmentValid=" + netService.IsSegmentValid(segmentId) +
                    " m_requireDirectRenderers=" + nodeId.ToNode().Info.m_requireDirectRenderers);
#pragma warning restore
            }
            return
                netService.IsNodeValid(nodeId) &&
                nodeId.ToNode().m_connectCount > 2 &&
                netService.IsSegmentValid(segmentId) &&
                segmentId.ToSegment().Info.m_requireDirectRenderers;
        }

        /// <summary>
        /// retrives cached data to determine if direct connect mesh should be rendered.
        /// </summary>
        internal bool GetShouldConnectTracks(
            ushort sourceSegmentId,
            int targetSegmentIDX,
            ushort nodeId,
            int nodeInfoIDX) {
            bool? startNode = netService.IsStartNode(sourceSegmentId, nodeId);
            AssertNotNull(startNode, "startNode");
            int index = Constants.ManagerFactory.ExtSegmentEndManager.GetIndex(sourceSegmentId, (bool)startNode);
            AssertNotNull(DirectConnectArray_ , "DirectConnectArray_"); 
            return DirectConnectArray_[index].GetShouldConnect(targetSegmentIDX, nodeInfoIDX);
        }
    } // end class
} // end namespace