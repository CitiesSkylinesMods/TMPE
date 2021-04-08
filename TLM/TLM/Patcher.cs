namespace TrafficManager {
    using ColossalFramework;
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using HarmonyLib;
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using TrafficManager.RedirectionFramework;
    using TrafficManager.Util;

    public static class Patcher {
        private const string HARMONY_ID = "de.viathinksoft.tmpe";

        public static void Install() {
            Log.Info("Init detours");
            bool fail = false;

            try {
#if DEBUG
                Harmony.DEBUG = true;
#endif
                // Harmony attribute-driven patching
                Log.Info($"Performing Harmony attribute-driven patches");
                new Harmony(HARMONY_ID).PatchAll();
                Log.Info($"Harmony attribute-driven patching successfull!");
            }
            catch (Exception e) {
                Log.Error("Could not apply Harmony patches because the following exception occured:\n " +
                    e +
                    "\n   -- End of inner exception stack trace -- ");
                fail = true;
            }

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
                UIView.library
                        .ShowModal<ExceptionPanel>("ExceptionPanel")
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

        public static void Uninstall() {
            new Harmony(HARMONY_ID).UnpatchAll(HARMONY_ID);
            AssemblyRedirector.Revert();
            Log.Info("TMPE patches uninstalled.");
        }
    }
}
