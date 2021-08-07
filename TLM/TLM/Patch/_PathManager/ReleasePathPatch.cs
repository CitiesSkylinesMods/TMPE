namespace TrafficManager.Patch._PathManager {
    using System.Reflection;
    using Custom.PathFinding;
    using Util;

    public class ReleasePathPatch {
        private delegate void TargetDelegate(uint unit);

        public static MethodBase TargetMethod() => TranspilerUtil.DeclaredMethod<TargetDelegate>( typeof(PathManager), nameof(PathManager.ReleasePath));

        public static bool Prefix(uint unit) {
            CustomPathManager._instance.CustomReleasePath(unit);
            return false;
        }
    }
}