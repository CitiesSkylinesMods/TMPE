namespace TrafficManager.LifeCycle {
    using ColossalFramework.UI;
    using ColossalFramework;
    using CSUtil.Commons;
    using HarmonyLib;
    using System.Collections.Generic;
    using System.Reflection;
    using System;

    using TrafficManager.RedirectionFramework;

    using TrafficManager.Util;

    public class Patcher {
        public static Patcher Instance { get; private set; }
        public static Patcher Create() => Instance = new Patcher();

        private const string HARMONY_ID = "de.viathinksoft.tmpe";

        public class Detour {
            public MethodInfo OriginalMethod;
            public MethodInfo CustomMethod;
            public RedirectCallsState Redirect;

            public Detour(MethodInfo originalMethod, MethodInfo customMethod) {
                OriginalMethod = originalMethod;
                CustomMethod = customMethod;
                Redirect = RedirectionHelper.RedirectCalls(originalMethod, customMethod);
            }
        }

        public bool initialized_ { get; private set; } = false;

        /// <summary>
        /// Method redirection states for attribute-driven detours
        /// </summary>
        public IDictionary<MethodInfo, RedirectCallsState> DetouredMethodStates { get; private set; } =
            new Dictionary<MethodInfo, RedirectCallsState>();

        public void Install() {
            // TODO realize detouring with annotations
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
                Log.Error("Could not deploy Harmony patches");
                Log.Info(e.Message);
                Log.Info(e.StackTrace);
                fail = true;
                throw e;
            }

            try {
                Log.Info("Deploying attribute-driven detours");
                DetouredMethodStates = AssemblyRedirector.Deploy();
            }
            catch (Exception e) {
                Log.Error("Could not deploy attribute-driven detours");
                Log.Info(e.ToString());
                Log.Info(e.StackTrace);
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
            harmony.UnpatchAll();

            initialized_ = false;
            Log.Info("Reverting detours finished.");
        }

    }
}
