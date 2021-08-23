namespace TrafficManager.Patch._PathManager {
    using System.Reflection;
    using Custom.PathFinding;
    using HarmonyLib;
    using JetBrains.Annotations;
    using Util;

    [UsedImplicitly]
    [CustomPathFindPatch]
    [HarmonyPatch]
    public class ReleasePathPatch {
        private delegate void TargetDelegate(uint unit);

        [UsedImplicitly]
        public static MethodBase TargetMethod() => TranspilerUtil.DeclaredMethod<TargetDelegate>( typeof(PathManager), nameof(PathManager.ReleasePath));

        [UsedImplicitly]
        public static bool Prefix(uint unit) {
            CustomPathManager._instance.CustomReleasePath(unit);
            return false;
        }
    }
}