namespace TrafficManager
{
    using System;
    using System.Reflection;
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using ICities;
    using JetBrains.Annotations;
    using State;
    using Util;

    public class TrafficManagerMod : IUserMod {
#if LABS
        public const string BRANCH = "LABS";
#elif DEBUG
        public const string BRANCH = "DEBUG";
#else
        public const string BRANCH = "STABLE";
#endif

        public const uint GAME_VERSION = 184803856u;
        public const uint GAME_VERSION_A = 1u;
        public const uint GAME_VERSION_B = 12u;
        public const uint GAME_VERSION_C = 1u;
        public const uint GAME_VERSION_BUILD = 2u;

        public const string VERSION = "11.0-alpha4";

        public static readonly string ModName = "TM:PE " + BRANCH + " " + VERSION;

        public string Name => ModName;

        public string Description => "Manage your city's traffic";

        [UsedImplicitly]
        public void OnEnabled() {
            Log.InfoFormat(
                "TM:PE enabled. Version {0}, Build {1} {2} for game version {3}.{4}.{5}-f{6}",
                VERSION,
                Assembly.GetExecutingAssembly().GetName().Version,
                BRANCH,
                GAME_VERSION_A,
                GAME_VERSION_B,
                GAME_VERSION_C,
                GAME_VERSION_BUILD);
            Log.InfoFormat(
                "Enabled TM:PE has GUID {0}",
                Assembly.GetExecutingAssembly().ManifestModule.ModuleVersionId);

            // check for incompatible mods
            if (UIView.GetAView() != null) {
                // when TM:PE is enabled in content manager
                CheckForIncompatibleMods();
            } else {
                // or when game first loads if TM:PE was already enabled
                LoadingManager.instance.m_introLoaded += CheckForIncompatibleMods;
            }

            // Log Mono version
            Type monoRt = Type.GetType("Mono.Runtime");
            if (monoRt != null) {
                MethodInfo displayName = monoRt.GetMethod(
                    "GetDisplayName",
                    BindingFlags.NonPublic | BindingFlags.Static);
                if (displayName != null) {
                    Log.InfoFormat("Mono version: {0}", displayName.Invoke(null, null));
                }
            }
        }

        [UsedImplicitly]
        public void OnDisabled() {
            Log.Info("TM:PE disabled.");
            LoadingManager.instance.m_introLoaded -= CheckForIncompatibleMods;
        }

        [UsedImplicitly]
        public void OnSettingsUI(UIHelperBase helper) {
            Options.MakeSettings(helper);
        }

        private static void CheckForIncompatibleMods() {
            ModsCompatibilityChecker mcc = new ModsCompatibilityChecker();
            mcc.PerformModCheck();
        }
    }
}
