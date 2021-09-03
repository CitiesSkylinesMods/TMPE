namespace TrafficManager.Patch._PathFind {
    using System.Reflection;
    using CSUtil.Commons;
    using Custom.PathFinding;
    using HarmonyLib;
    using JetBrains.Annotations;
    using Util;

    [UsedImplicitly]
    [CustomPathFindPatch]
    [HarmonyPatch]
    public class CalculatePathPatch {
        private delegate bool TargetDelegate(uint unit, bool skipQueue);

        [UsedImplicitly]
        public static MethodBase TargetMethod() => TranspilerUtil.DeclaredMethod<TargetDelegate>( typeof(PathFind), nameof(PathFind.CalculatePath));

        [UsedImplicitly]
        public static bool Prefix(PathFind __instance,
                                  ref bool __result,
                                  uint unit,
                                  bool skipQueue) {
            Log.Warning($"PathFind.CalculatePath() called outside of TM:PE! Unit: {unit}");
            if (__instance is CustomPathFind pf) {
                __result = pf.CalculatePath(unit, skipQueue);
            } else {
                __result = false;
            }
            return false;
        }
    }
}