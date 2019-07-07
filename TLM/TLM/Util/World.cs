namespace TrafficManager.Util {
    using System.Runtime.CompilerServices;
    using ColossalFramework;
    using UnityEngine;

    /// <summary>
    /// A helper holding references to the world geometry for convenience.
    /// </summary>
    internal class World {
        private static NetManager netManager_;
        private static NetLane[] laneBuffer_;
        private static NetNode[] nodeBuffer_;
        private static NetSegment[] segmentBuffer_;

        private World() { } // do not instantiate

        /// <summary>
        /// Hook this to OnLevelLoaded
        /// </summary>
        public static void Setup() {
            netManager_ = Singleton<NetManager>.instance;
            laneBuffer_ = netManager_.m_lanes.m_buffer;
            nodeBuffer_ = netManager_.m_nodes.m_buffer;
            segmentBuffer_ = netManager_.m_segments.m_buffer;
        }

        /// <summary>
        /// Hook this to OnLevelUnloaded
        /// </summary>
        public static void TearDown() {
            netManager_ = null;
            laneBuffer_ = null;
            nodeBuffer_ = null;
            segmentBuffer_ = null;
        }

        // TODO: Inline below

        public static NetLane Lane(uint index) {
#if DEBUG
            Debug.Assert(Constants.ServiceFactory.NetService.IsLaneValid(index));
#endif
            return laneBuffer_[index];
        }

        public static NetSegment Segment(ushort index) {
#if DEBUG
            Debug.Assert(Constants.ServiceFactory.NetService.IsSegmentValid(index));
#endif
            return segmentBuffer_[index];
        }

        public static NetNode Node(ushort index) {
#if DEBUG
            Debug.Assert(Constants.ServiceFactory.NetService.IsNodeValid(index));
#endif
            return nodeBuffer_[index];
        }

        /// <summary>
        /// Returns ref to world node, use where `ref nodes[index]` is needed.
        /// </summary>
        /// <param name="index">Node index</param>
        /// <returns>Reference to the node</returns>
        public static ref NetNode NodeRef(ushort index) {
#if DEBUG
            Debug.Assert(Constants.ServiceFactory.NetService.IsNodeValid(index));
#endif
            return ref nodeBuffer_[index];
        }
    }
}