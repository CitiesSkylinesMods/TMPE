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
    using Patch._PathFind;
    using Patch._PathManager;
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
            fail = !PatchAll(API.Harmony.HARMONY_ID, forbidden: typeof(CustomPathFindPatchAttribute));
            fail |= !PatchManual(API.Harmony.HARMONY_ID);

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
            fail = !PatchAll(API.Harmony.HARMONY_ID_PATHFINDING , required: typeof(CustomPathFindPatchAttribute));;

            if (fail) {
                Log.Info("TMPE Path-finding patcher failed");
                Prompt.Error(
                    "TM:PE failed to patch Path-finding",
                    "Traffic Manager: President Edition failed to load necessary patches. You can " +
                    "continue playing but it's NOT recommended. Traffic Manager will " +
                    "not work as expected.");
            } else {
                Log.Info("TMPE Path-finding patches installed successfully");
            }
        }

        /// <summary>
        /// applies all attribute driven harmony patches.
        /// continues on error.
        /// </summary>
        /// <returns>false if exception happens, true otherwise</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool PatchAll(string harmonyId, Type required = null, Type forbidden = null) {
            try {
                bool success = true;
                var harmony = new Harmony(harmonyId);
                var assembly = Assembly.GetExecutingAssembly();
                foreach (var type in AccessTools.GetTypesFromAssembly(assembly)) {
                    try {
                        if (required is not null && !type.IsDefined(required, true))
                            continue;
                        if (forbidden is not null && type.IsDefined(forbidden, true))
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

        /// <summary>
        /// manually applying harmony patches if target mods are enabled
        /// </summary>
        /// <returns>false if patching failed, true otherwise</returns>
        private static bool PatchManual(string harmonyId) {
            try {
                var harmony = new Harmony(harmonyId);

                // Patching SimulationStepPatch1 in Reversible Tram AI mod
                if (IsAssemblyEnabled("ReversibleTramAI")) {
                    Type simulationStepPatch1Type = Type.GetType("ReversibleTramAI.SimulationStepPatch1, ReversibleTramAI", false);
                    if (simulationStepPatch1Type != null) {
                        if (!TrafficManager.Patch._External._RTramAIModPatch.RTramAIModPatch.ApplyPatch(harmony, simulationStepPatch1Type)) return false;
                    }
                    else {
                        return false;
                    }
                }
                return true;
            }
            catch (Exception ex) {
                ex.LogException();
                return false;
            }
        }

        /// <summary>
        /// Check if a mod is enabled
        /// </summary>
        private static bool IsAssemblyEnabled(string assemblyName) {
            foreach (PluginManager.PluginInfo plugin in PluginManager.instance.GetPluginsInfo()) {
                foreach (Assembly assembly in plugin.GetAssemblies()) {
                    if (assembly.GetName().Name == assemblyName) {
                        return plugin.isEnabled;
                    }
                }
            }
            return false;
        }

        public static void Uninstall(string harmonyId) {
            try {
                new Harmony(harmonyId).UnpatchAll(harmonyId);
                Log.Info($"TMPE patches in [{harmonyId}] uninstalled.");
            } catch(Exception ex) {
                ex.LogException(true);
            }
        }
    }
}
