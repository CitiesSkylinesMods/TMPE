namespace TrafficManager.Manager.Impl {
    using API.Manager;
    using CSUtil.Commons;
    using Geometry;
    using Geometry.Impl;
    using Traffic.Data;

    public class ExtNodeManager : AbstractCustomManager, IExtNodeManager {
        public static ExtNodeManager Instance { get; }

        static ExtNodeManager() {
            Instance = new ExtNodeManager();
        }

        /// <summary>
        /// All additional data for nodes
        /// </summary>
        public ExtNode[] ExtNodes { get; }

        private ExtNodeManager() {
            ExtNodes = new ExtNode[NetManager.MAX_NODE_COUNT];

            for (uint i = 0; i < ExtNodes.Length; ++i) {
                ExtNodes[i] = new ExtNode((ushort)i);
            }
        }

        public bool IsValid(ushort nodeId) {
            return Services.NetService.IsNodeValid(nodeId);
        }

        public void AddSegment(ushort nodeId, ushort segmentId) {
            if (ExtNodes[nodeId].segmentIds.Add(segmentId) &&
                ExtNodes[nodeId].removedSegmentEndId != null)
            {
                var replacement = new SegmentEndReplacement {
                    oldSegmentEndId = ExtNodes[nodeId].removedSegmentEndId,
                    newSegmentEndId = new SegmentEndId(
                        segmentId,
                        (bool)Services.NetService.IsStartNode(segmentId, nodeId))
                };
                ExtNodes[nodeId].removedSegmentEndId = null;
                Constants.ManagerFactory.GeometryManager.OnSegmentEndReplacement(replacement);
            }
        }

        public void RemoveSegment(ushort nodeId, ushort segmentId) {
            if (ExtNodes[nodeId].segmentIds.Remove(segmentId)) {
                ExtNodes[nodeId].removedSegmentEndId = new SegmentEndId(
                    segmentId,
                    (bool)Services.NetService.IsStartNode(segmentId, nodeId));
            }
        }

        public void Reset(ushort nodeId) {
            Reset(ref ExtNodes[nodeId]);
        }

        private void Reset(ref ExtNode node) {
            node.Reset();
        }

        protected override void InternalPrintDebugInfo() {
            base.InternalPrintDebugInfo();
            Log._Debug($"Extended node data:");

            for (uint i = 0; i < ExtNodes.Length; ++i) {
                if (!IsValid((ushort)i)) {
                    continue;
                }

                Log._Debug($"Node {i}: {ExtNodes[i]}");
            }
        }

        public override void OnLevelUnloading() {
            base.OnLevelUnloading();

            for (int i = 0; i < ExtNodes.Length; ++i) {
                Reset(ref ExtNodes[i]);
            }
        }
    }
}