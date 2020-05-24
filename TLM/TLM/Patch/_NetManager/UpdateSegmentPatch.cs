namespace TrafficManager.Patch._NetManager {
    using HarmonyLib;
    using JetBrains.Annotations;

    [HarmonyPatch(
        typeof(NetManager),
        "UpdateSegment",
        new[] { typeof(ushort), typeof(ushort), typeof(int) })]
    [UsedImplicitly]
    public static class UpdateSegmentPatch {
        /// <summary>
        /// Initiates a segment geometry recalculation when a segment is updated.
        /// </summary>
        [HarmonyPostfix]
        [UsedImplicitly]
        public static void Postfix(NetManager __instance,
                                   ushort segment,
                                   ushort fromNode,
                                   int level) {
            Constants.ManagerFactory.ExtSegmentManager.Recalculate(segment);
        }
    }
}