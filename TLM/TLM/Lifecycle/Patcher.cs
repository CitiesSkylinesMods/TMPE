namespace TrafficManager.Lifecycle {
    using CSUtil.Commons;
    using HarmonyLib;
    using System;
    using CitiesHarmony.API;
    using System.Runtime.CompilerServices;
    using System.Reflection;
    using TrafficManager.Util;
    using System.Linq;

    public static class Patcher {
        private const string HARMONY_ID = "de.viathinksoft.tmpe";

        private const string ERROR_MESSAGE =
            "****** ERRRROOORRRRRR!!!!!!!!!! **************\n" +
            "**********************************************\n" +
            "    HARMONY MOD DEPENDENCY IS NOT INSTALLED!\n\n" +
            SOLUTION + "\n" +
            "**********************************************\n" +
            "**********************************************\n";
        private const string SOLUTION =
            "solution:\n" +
            " - exit to desktop.\n" +
            " - unsub harmony mod.\n" +
            " - make sure harmony mod is deleted from the content folder\n" +
            " - resub to harmony mod.\n" +
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

        /// <summary>
        /// applies all attribute diven harmony patches.
        /// continues on error.
        /// </summary>
        /// <returns>false if exception happens, true otherwise</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        static bool PatchAll() {
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
