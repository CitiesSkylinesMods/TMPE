namespace TrafficManager.Patch._RoadBaseAI {
    using HarmonyLib;
    using JetBrains.Annotations;
    using TrafficManager.State;
    using TrafficManager.Util;
    using TrafficManager.Util.Extensions;

    [HarmonyPatch(typeof(RoadBaseAI), "UpdateLanes")]
    [UsedImplicitly]
    public class UpdateLanesPatch {

        /// <summary>
        /// Update lane arrows after lane data was updated.
        /// </summary>
        [UsedImplicitly]
        static void Postfix(ushort segmentID) {
            if (SavedGameOptions.Instance.DedicatedTurningLanes) {
                BuiltIn(segmentID);
            }

            Forced(segmentID);
        }

        /// <summary>
        /// Apply default Lane arrows according to policy.
        /// </summary>
        private static void BuiltIn(ushort segmentId) {
            SeparateTurningLanesUtil.SeparateSegmentLanesBuiltIn(
                segmentId: segmentId,
                nodeId: segmentId.ToSegment().m_startNode);
            SeparateTurningLanesUtil.SeparateSegmentLanesBuiltIn(
                segmentId: segmentId,
                nodeId: segmentId.ToSegment().m_endNode);
        }

        /// <summary>
        /// Apply user lane arrows.
        /// </summary>
        private static void Forced(ushort segmentId) {
            uint laneId = segmentId.ToSegment().m_lanes;
            while (laneId != 0) {
                if (!Flags.ApplyLaneArrowFlags(laneId)) {
                    Flags.RemoveLaneArrowFlags(laneId);
                }

                laneId = laneId.ToLane().m_nextLane;
            }
        }
    }
}