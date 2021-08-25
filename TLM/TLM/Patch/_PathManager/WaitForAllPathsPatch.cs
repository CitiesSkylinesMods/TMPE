namespace TrafficManager.Patch._PathManager {
    using Custom.PathFinding;
    using HarmonyLib;
    using JetBrains.Annotations;

    [UsedImplicitly]
    [CustomPathFindPatch]
    [HarmonyPatch(typeof(PathManager), nameof(PathManager.WaitForAllPaths))]
    public class WaitForAllPathsPatch {

        [UsedImplicitly]
        public static bool Prefix() {
            if (CustomPathManager._instance != null) {
                CustomPathManager._instance.WaitForAllPaths();
                return false;
            }

            return true;
        }
    }
}