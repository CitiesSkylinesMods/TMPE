namespace TrafficManager.Patch._RoadBaseAI {
    using ColossalFramework;
    using HarmonyLib;
    using JetBrains.Annotations;
    using TrafficManager.State;

    [HarmonyPatch(typeof(RoadBaseAI), "UpdateLanes")]
    [UsedImplicitly]
    public class UpdateLanesPatch {
        /// <summary>
        /// Update lane arrows after lane data was updated.
        /// </summary>
        [HarmonyPostfix]
        [UsedImplicitly]
        public static void Postfix(ushort segmentID, ref NetSegment data, bool loading) {
            NetManager netManager = Singleton<NetManager>.instance;
            uint laneId = netManager.m_segments.m_buffer[segmentID].m_lanes;
            while (laneId != 0) {
                if (!Flags.ApplyLaneArrowFlags(laneId)) {
                    Flags.RemoveLaneArrowFlags(laneId);
                }

                laneId = netManager.m_lanes.m_buffer[laneId].m_nextLane;
            }
        }
    }
}