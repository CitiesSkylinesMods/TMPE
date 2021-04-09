namespace TrafficManager {
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using HarmonyLib;
    using System;
    using TrafficManager.RedirectionFramework;
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
                    "    HARMONY MOD DEPENDANCY IS NOT INSTALLED!\n\n" +
                    "solution:\n" +
                    " - exit to desktop.\n" +
                    " - unsub harmony mod.\n" +
                    " - make sure harmony mod is deleted from the content folder\n" +
                    " - resub to harmony mod.\n" +
                    " - run the game again.\n" +
                    "**********************************************\n" +
                    "**********************************************\n";

        internal static void AssertCitiesHarmonyInstalled() {
            if(!HarmonyHelper.IsHarmonyInstalled) {
                throw new Exception(ERROR_MESSAGE);
            }
        }

        public static void Install() {
            Log.Info("Init detours");
            bool fail = false;
#if DEBUG
            Harmony.DEBUG = false; // set to true to get harmony debug info.
#endif
            AssertCitiesHarmonyInstalled();
            fail = !PatchAll();

            try {
                Log.Info("Deploying attribute-driven detours");
                AssemblyRedirector.Deploy();
            }
            catch (Exception e) {
                Log.Error("Could not deploy attribute-driven detours because the following exception occured:\n "
                    + e +
                    "\n    -- End of inner exception stack trace -- ");
                fail = true;
            }

            if (fail) {
                Log.Info("patcher failed");
                UIView.library.ShowModal<ExceptionPanel>("ExceptionPanel")
                    .SetMessage(
                    "TM:PE failed to load",
                    "Traffic Manager: President Edition failed to load. You can " +
                    "continue playing but it's NOT recommended. Traffic Manager will " +
                    "not work as expected.",
                    true);
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
            bool success = true;
            var harmony = new Harmony(HARMONY_ID);
            var assembly = Assembly.GetExecutingAssembly();
            foreach(var type in AccessTools.GetTypesFromAssembly(assembly)) {
                try {
                    var methods = harmony.CreateClassProcessor(type).Patch();
                    if (methods != null && methods.Any()) {
                        var strMethods = methods.Select(_method => _method.Name).ToArray();
                        Log.Info($"{type.FullName} patched: " + string.Join(", ", strMethods));
                    }
                } catch(Exception ex) {
                    ex.LogException();
                    success = false;
                }
            }
            {
                var methods = harmony.GetPatchedMethods()
                    .Select(_method => $"\t{_method.FullDescription()}");
                Log.Info($"The following methods were patched by TMPE:\n" +
                    string.Join("\n", methods.ToArray()));
            }
            return success;
        }

        public static void Uninstall() {
            new Harmony(HARMONY_ID).UnpatchAll(HARMONY_ID);
            AssemblyRedirector.Revert();
            Log.Info("TMPE patches uninstalled.");
        }
    }
}
