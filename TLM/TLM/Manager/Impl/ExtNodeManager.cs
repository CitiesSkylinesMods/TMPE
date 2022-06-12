namespace TrafficManager.Manager.Impl {
    using CSUtil.Commons;
    using TrafficManager.API.Geometry;
    using TrafficManager.API.Manager;
    using TrafficManager.API.Traffic.Data;
    using TrafficManager.Geometry.Impl;
    using TrafficManager.Util;
    using ColossalFramework;
    using TrafficManager.Util.Extensions;
    using TrafficManager.Util.Iterators;

    public class ExtNodeManager
        : AbstractCustomManager,
          IExtNodeManager
    {
        static ExtNodeManager() {
            Instance = new ExtNodeManager();
        }

        private ExtNodeManager() {
            ExtNodes = new ExtNode[NetManager.MAX_NODE_COUNT];

            for (uint i = 0; i < ExtNodes.Length; ++i) {
                ExtNodes[i] = new ExtNode((ushort)i);
            }
        }

        public static ExtNodeManager Instance { get; }

        /// <summary>
        /// All additional data for nodes
        /// </summary>
        public ExtNode[] ExtNodes { get; }

        /// <summary>
        /// assuming highway rules are on, does the junction follow highway rules?
        /// </summary>
        /// <param name="nodeId">NodeId of the node to test.</param>
        /// <returns></returns>
        public static bool JunctionHasHighwayRules(ushort nodeId) {
            return JunctionHasOnlyHighwayRoads(nodeId) && !LaneConnection.LaneConnectionManager.Instance.Road.HasNodeConnections(nodeId);
        }

        public GetNodeSegmentIdsEnumerable GetNodeSegmentIds(ushort nodeId, ClockDirection clockDirection) {
            ref NetNode netNode = ref nodeId.ToNode();
            var initialSegmentId = GetInitialSegment(ref netNode);
            var segmentBuffer = Singleton<NetManager>.instance.m_segments.m_buffer;
            return new GetNodeSegmentIdsEnumerable(nodeId, initialSegmentId, clockDirection, segmentBuffer);
        }

        /// <summary>
        /// Gets the initial segment.
        /// </summary>
        /// <param name="node">The node with the segments.</param>
        /// <returns>First non 0 segmentId.</returns>
        public ushort GetInitialSegment(ref NetNode node) {
            for (int i = 0; i < 8; ++i) {
                var segmentId = node.GetSegment(i);
                if (segmentId != 0) {
                    return segmentId;
                }
            }

            return 0;
        }

        /// <summary>
        /// Are all segments at nodeId highways?
        /// </summary>
        /// <param name="nodeId"></param>
        /// <returns></returns>
        public static bool JunctionHasOnlyHighwayRoads(ushort nodeId) {
            IExtSegmentManager segMan = Constants.ManagerFactory.ExtSegmentManager;
            ref NetNode node = ref nodeId.ToNode();
            for (int i = 0; i < 8; ++i) {
                ushort segmentId = node.GetSegment(i);
                if (segmentId != 0) {
                    if(!segMan.CalculateIsHighway(segmentId)) {
                        return false;
                    }
                }
            }

            return true;
        }

        public void AddSegment(ushort nodeId, ushort segmentId) {
            if (ExtNodes[nodeId].segmentIds.Add(segmentId) &&
                ExtNodes[nodeId].removedSegmentEndId != null)
            {
                var replacement = new SegmentEndReplacement {
                    oldSegmentEndId = ExtNodes[nodeId].removedSegmentEndId,
                    newSegmentEndId = new SegmentEndId(segmentId, nodeId),
                };

                ExtNodes[nodeId].removedSegmentEndId = null;
                Constants.ManagerFactory.GeometryManager.OnSegmentEndReplacement(replacement);
            }
        }

        public void RemoveSegment(ushort nodeId, ushort segmentId) {
            if (ExtNodes[nodeId].segmentIds.Remove(segmentId)) {
                ExtNodes[nodeId].removedSegmentEndId = new SegmentEndId(segmentId, nodeId);
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
                ref NetNode netNode = ref ((ushort)i).ToNode();
                if (!netNode.IsValid()) {
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