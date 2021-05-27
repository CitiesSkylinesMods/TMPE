namespace TrafficManager.Patch._NetManager {
    using HarmonyLib;
    using JetBrains.Annotations;

    [HarmonyPatch(typeof(NetManager), "FinalizeSegment")]
    [UsedImplicitly]
    public static class FinalizeSegmentPatch {
        /// <summary>
        /// Initiates a segment geometry recalculation when a segment is finalized.
        /// </summary>
        [HarmonyPostfix]
        [UsedImplicitly]
        public static void Postfix(NetManager __instance, ushort segment, ref NetSegment data) {
            Constants.ManagerFactory.ExtSegmentManager.Recalculate(segment);
        }
    }
}