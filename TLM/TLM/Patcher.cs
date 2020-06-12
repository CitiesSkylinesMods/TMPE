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

    public class Patcher {
        public static Patcher Instance { get; private set; }

        public static Patcher Create() => Instance = new Patcher();

        private const string HARMONY_ID = "de.viathinksoft.tmpe";

        private bool initialized_ = false;

        public void Install() {
            if (initialized_) {
                return;
            }

            Log.Info("Init detours");
            bool fail = false;

            try {
#if DEBUG
                Harmony.DEBUG = true;
#endif
                // Harmony attribute-driven patching
                Log.Info($"Performing Harmony attribute-driven patching");
                var harmony = new Harmony(HARMONY_ID);
                Shortcuts.Assert(harmony != null, "HarmonyInst!=null");
                harmony.PatchAll();
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
                Log.Info("Detours failed");
                Singleton<SimulationManager>.instance.m_ThreadingWrapper.QueueMainThread(
                    () => {
                        UIView.library
                              .ShowModal<ExceptionPanel>("ExceptionPanel")
                              .SetMessage(
                                "TM:PE failed to load",
                                "Traffic Manager: President Edition failed to load. You can " +
                                "continue playing but it's NOT recommended. Traffic Manager will " +
                                "not work as expected.",
                                true);
                    });
            } else {
                Log.Info("Detours successful");
            }

            initialized_ = true;
        }

        public void Uninstall() {
            if (!initialized_) {
                return;
            }

            var harmony = new Harmony(HARMONY_ID);
            Shortcuts.Assert(harmony != null, "HarmonyInst!=null");
            harmony.UnpatchAll(HARMONY_ID);

            initialized_ = false;
            Log.Info("Reverting detours finished.");
        }
    }
}
