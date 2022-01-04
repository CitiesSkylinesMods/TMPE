using TrafficManager.Util.Extensions;

namespace TrafficManager.Util {
    public static class LaneUtil {
        /// <summary>
        /// gets the index of lane id or -1 if lane was not found
        /// </summary>
        public static int GetLaneIndex(uint laneId) {
            ref NetLane netLane = ref laneId.ToLane();
            ref NetSegment netSegment = ref netLane.m_segment.ToSegment();
            var lanes = netSegment.Info?.m_lanes;
            if (lanes != null) {
                uint curLaneId = netSegment.m_lanes;
                for (int laneIndex = 0; laneIndex < lanes.Length && curLaneId != 0; ++laneIndex) {
                    if (curLaneId == laneId) {
                        return laneIndex;
                    }
                    laneIndex++;
                    curLaneId = curLaneId.ToLane().m_nextLane;
                }
            }

            return -1;
        }
    }
}