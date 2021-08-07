namespace TrafficManager.Patch._PathManager {
    using System.Reflection;
    using CSUtil.Commons;
    using Custom.PathFinding;
    using Util;

    public class WaitForAllPathsPatch {
        private delegate void TargetDelegate();

        public static MethodBase TargetMethod() => TranspilerUtil.DeclaredMethod<TargetDelegate>( typeof(PathManager), nameof(PathManager.WaitForAllPaths));

        public static bool Prefix() {
            Log.Info("Prefix:WaitForAllPaths()");
            if (CustomPathManager._instance != null) {
                CustomPathManager._instance.WaitForAllPaths();
                return false;
            }

            return true;
        }
    }
}