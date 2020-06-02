namespace TrafficManager.Patch._NetSegment {
    using HarmonyLib;
    using JetBrains.Annotations;
    using TrafficManager.Manager.Impl;

    [HarmonyPatch(typeof(NetSegment), nameof(NetSegment.CalculateSegment))]
    [UsedImplicitly]
    public class CalculateSegmentPatch {
        [UsedImplicitly]
        public static void Postfix(ushort segmentID) {
            ExtSegmentEndManager.Instance.CalculateCorners(segmentID, false);
            ExtSegmentEndManager.Instance.CalculateCorners(segmentID, true);
        }
    }
}
