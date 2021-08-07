namespace TrafficManager.Lifecycle {
    using CSUtil.Commons;
    using HarmonyLib;
    using System;
    using CitiesHarmony.API;
    using System.Runtime.CompilerServices;
    using System.Reflection;
    using TrafficManager.Util;
    using System.Linq;
    using Patch._PathFind;
    using Patch._PathManager;

    public static class Patcher {
        private const string HARMONY_ID = "de.viathinksoft.tmpe";
        private const string HARMONY_ID_PF = "de.viathinksoft.tmpe.pathfinding";

        private const string ERROR_MESSAGE =
            "****** ERRRROOORRRRRR!!!!!!!!!! **************\n" +
            "**********************************************\n" +
            "    HARMONY MOD DEPENDENCY IS NOT INSTALLED!\n\n" +
            SOLUTION + "\n" +
            "**********************************************\n" +
            "**********************************************\n";
        private const string SOLUTION =
            "Solution:\n" +
            " - exit to desktop.\n" +
            " - unsubscribe harmony mod.\n" +
            " - make sure harmony mod is deleted from the content folder\n" +
            " - resubscribe to harmony mod.\n" +
            " - run the game again.";

        internal static void AssertCitiesHarmonyInstalled() {
            if (!HarmonyHelper.IsHarmonyInstalled) {
                Shortcuts.ShowErrorDialog("Error: Missing Harmony", SOLUTION);
                throw new Exception(ERROR_MESSAGE);
            }
        }

        public static void Install() {
            bool fail = false;
#if DEBUG
            Harmony.DEBUG = false; // set to true to get harmony debug info.
#endif
            AssertCitiesHarmonyInstalled();
            fail = !PatchAll();

            if (fail) {
                Log.Info("patcher failed");
                Shortcuts.ShowErrorDialog(
                    "TM:PE failed to load",
                    "Traffic Manager: President Edition failed to load. You can " +
                    "continue playing but it's NOT recommended. Traffic Manager will " +
                    "not work as expected.");
            } else {
                Log.Info("TMPE patches installed successfully");
            }
        }

        public static void InstallPathFinding() {
            bool fail = false;
#if DEBUG
            Harmony.DEBUG = false; // set to true to get harmony debug info.
#endif
            AssertCitiesHarmonyInstalled();
            fail = !PatchPathfinding();

            if (fail) {
                Log.Info("TMPE Path-finding patcher failed");
                Shortcuts.ShowErrorDialog(
                    "TM:PE failed to patch Path-finding",
                    "Traffic Manager: President Edition failed to load necessary patches. You can " +
                    "continue playing but it's NOT recommended. Traffic Manager will " +
                    "not work as expected.");
            } else {
                Log.Info("TMPE Path-finding patches installed successfully");
            }
        }

        public static void UninstallPathFinding() {
            bool fail = false;
#if DEBUG
            Harmony.DEBUG = false; // set to true to get harmony debug info.
#endif
            AssertCitiesHarmonyInstalled();
            fail = !UnPatchPathfinding();

            if (fail) {
                Log.Info("TMPE Path-finding unpatcher failed");
                Shortcuts.ShowErrorDialog(
                    "TM:PE failed to unpatch Path-finding",
                    "Traffic Manager: President Edition failed to unload patches.\nTraffic Manager will " +
                    "not work as expected.");
            } else {
                Log.Info("TMPE Path-finding patches uninstalled successfully");
            }
        }

        /// <summary>
        /// Applies all PathFinding harmony patches.
        /// continues on error.
        /// </summary>
        /// <returns>false if exception happens, true otherwise</returns>
        private static bool PatchPathfinding() {
            try {
                var harmony = new Harmony(HARMONY_ID_PF);
                harmony.Patch(CalculatePathPatch.TargetMethod(), prefix: new HarmonyMethod(AccessTools.Method(typeof(CalculatePathPatch), nameof(CalculatePathPatch.Prefix))));
                harmony.Patch(CreatePathPatch.TargetMethod(), prefix: new HarmonyMethod(AccessTools.Method(typeof(CreatePathPatch), nameof(CreatePathPatch.Prefix))));
                harmony.Patch(ReleasePathPatch.TargetMethod(), prefix: new HarmonyMethod(AccessTools.Method(typeof(ReleasePathPatch), nameof(ReleasePathPatch.Prefix))));
                harmony.Patch(WaitForAllPathsPatch.TargetMethod(), prefix: new HarmonyMethod(AccessTools.Method(typeof(WaitForAllPathsPatch), nameof(WaitForAllPathsPatch.Prefix))));
                return true;
            } catch (Exception ex) {
                ex.LogException();
                return false;
            }
        }

        /// <summary>
        /// Removes all PathFinding harmony patches.
        /// continues on error.
        /// </summary>
        /// <returns>false if exception happens, true otherwise</returns>
        private static bool UnPatchPathfinding() {
            try {
                var harmony = new Harmony(HARMONY_ID_PF);
                harmony.Unpatch(CalculatePathPatch.TargetMethod(), HarmonyPatchType.Prefix, HARMONY_ID_PF);
                harmony.Unpatch(CreatePathPatch.TargetMethod(), HarmonyPatchType.Prefix, HARMONY_ID_PF);
                harmony.Unpatch(ReleasePathPatch.TargetMethod(), HarmonyPatchType.Prefix, HARMONY_ID_PF);
                harmony.Unpatch(WaitForAllPathsPatch.TargetMethod(), HarmonyPatchType.Prefix, HARMONY_ID_PF);
                return true;
            } catch (Exception ex) {
                ex.LogException();
                return false;
            }
        }

        /// <summary>
        /// applies all attribute driven harmony patches.
        /// continues on error.
        /// </summary>
        /// <returns>false if exception happens, true otherwise</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool PatchAll() {
            try {
                bool success = true;
                var harmony = new Harmony(HARMONY_ID);
                var assembly = Assembly.GetExecutingAssembly();
                foreach (var type in AccessTools.GetTypesFromAssembly(assembly)) {
                    try {
                        var methods = harmony.CreateClassProcessor(type).Patch();
                        if (methods != null && methods.Any()) {
                            var strMethods = methods.Select(_method => _method.Name).ToArray();
                        }
                    } catch (Exception ex) {
                        ex.LogException();
                        success = false;
                    }
                }
                return success;
            } catch (Exception ex) {
                ex.LogException();
                return false;
            }
        }

        public static void Uninstall() {
            try {
                new Harmony(HARMONY_ID).UnpatchAll(HARMONY_ID);
                Log.Info("TMPE patches uninstalled.");
            } catch(Exception ex) {
                ex.LogException(true);
            }
        }
    }
}
