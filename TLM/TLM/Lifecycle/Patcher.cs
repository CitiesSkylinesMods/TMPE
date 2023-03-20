namespace TrafficManager.Lifecycle {
    using CSUtil.Commons;
    using HarmonyLib;
    using System;
    using CitiesHarmony.API;
    using System.Runtime.CompilerServices;
    using System.Reflection;
    using TrafficManager.Util;
    using System.Linq;
    using Patch;
    using ColossalFramework.Plugins;
    using TrafficManager.UI.Helpers;

    public static class Patcher {
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
        private static bool pathfindingPatchesInstalled;

        internal static void AssertCitiesHarmonyInstalled() {
            if (!HarmonyHelper.IsHarmonyInstalled) {
                Prompt.Error("Error: Missing Harmony", SOLUTION);
                throw new Exception(ERROR_MESSAGE);
            }
        }

        public static void Install() {
            bool fail = false;
#if DEBUG
            Harmony.DEBUG = false; // set to true to get harmony debug info.
#endif
            AssertCitiesHarmonyInstalled();
            fail = !PatchAll(API.Harmony.HARMONY_ID, forbiddens: new [] {typeof(CustomPathFindPatchAttribute), typeof(PreloadPatchAttribute)});

            if (fail) {
                Log.Info("patcher failed");
                Prompt.Error(
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
            fail = !PatchAll(API.Harmony.HARMONY_ID_PATHFINDING, required: typeof(CustomPathFindPatchAttribute));

            if (fail) {
                Log.Info("TMPE Path-finding patcher failed");
                Prompt.Error(
                    "TM:PE failed to patch Path-finding",
                    "Traffic Manager: President Edition failed to load necessary patches. You can " +
                    "continue playing but it's NOT recommended. Traffic Manager will " +
                    "not work as expected.");
            } else {
                pathfindingPatchesInstalled = true;
                Log.Info("TMPE Path-finding patches installed successfully");
            }
        }

        public static void InstallPreload() {

            bool fail = false;
#if DEBUG
            Harmony.DEBUG = false; // set to true to get harmony debug info.
#endif
            AssertCitiesHarmonyInstalled();

            // reinstall:
            Uninstall(API.Harmony.HARMONY_ID_PRELOAD);
            fail = !PatchAll(API.Harmony.HARMONY_ID_PRELOAD, required: typeof(PreloadPatchAttribute));

            if (fail) {
                Log.Info("TMPE patcher failed at preload");
                Prompt.Error(
                    "TM:PE failed to patch at preload",
                    "Traffic Manager: President Edition failed to load necessary patches. You can " +
                    "continue playing but it's NOT recommended. Traffic Manager will " +
                    "not work as expected.");
            } else {
                Log.Info("preload patches installed successfully");
            }
        }

        /// <summary>
        /// applies all attribute driven harmony patches.
        /// continues on error.
        /// </summary>
        /// <returns>false if exception happens, true otherwise</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool PatchAll(string harmonyId, Type required = null, params Type[] forbiddens) {
            try {
                bool success = true;
                var harmony = new Harmony(harmonyId);
                var assembly = Assembly.GetExecutingAssembly();
                foreach (var type in AccessTools.GetTypesFromAssembly(assembly)) {
                    try {
                        if (required is not null && !type.IsDefined(required, true))
                            continue;
                        bool isForbidden = forbiddens?.Any(forbidden => type.IsDefined(forbidden, true)) ?? false;
                        if (isForbidden)
                            continue;

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

        public static void Uninstall(string harmonyId) {
            try {
                new Harmony(harmonyId).UnpatchAll(harmonyId);
                if (harmonyId.Equals(API.Harmony.HARMONY_ID_PATHFINDING)) {
                    pathfindingPatchesInstalled = false;
                }
                Log.Info($"TMPE patches in [{harmonyId}] uninstalled.");
            } catch(Exception ex) {
                ex.LogException(true);
            }
        }
    }
}
