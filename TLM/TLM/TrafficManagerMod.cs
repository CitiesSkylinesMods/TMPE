namespace TrafficManager {
    using ColossalFramework.Globalization;
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using ICities;
    using JetBrains.Annotations;
    using System.Reflection;
    using System;
    using TrafficManager.State;
    using TrafficManager.UI;
    using TrafficManager.Util;
    using static TrafficManager.Util.Shortcuts;
    using ColossalFramework;
    using UnityEngine.SceneManagement;
    using CitiesHarmony.API;

    public class TrafficManagerMod : IUserMod {
#if BENCHMARK
        public TrafficManagerMod() {
            Benchmark.BenchmarkManager.Setup();
        }
#endif

#if LABS
        public const string BRANCH = "LABS";
#elif DEBUG
        public const string BRANCH = "DEBUG";
#else
        public const string BRANCH = "STABLE";
#endif

        // These values from `BuildConfig` class (`APPLICATION_VERSION` constants) in game file `Managed/Assembly-CSharp.dll` (use ILSpy to inspect them)
        public const uint GAME_VERSION = 188868624U;
        public const uint GAME_VERSION_A = 1u;
        public const uint GAME_VERSION_B = 13u;
        public const uint GAME_VERSION_C = 0u;
        public const uint GAME_VERSION_BUILD = 8u;

        // Use SharedAssemblyInfo.cs to modify TM:PE version
        // External mods (eg. CSUR Toolbox) reference the versioning for compatibility purposes
        public static Version ModVersion => typeof(TrafficManagerMod).Assembly.GetName().Version;

        // used for in-game display
        public static string VersionString => ModVersion.ToString(3);

        public static readonly string ModName = "TM:PE " + VersionString + " " + BRANCH;

        public string Name => ModName;

        public string Description => "Manage your city's traffic";

        internal static bool InGameHotReload { get; set; } = false;

        /// <summary>
        /// determines if simulation is inside game/editor. useful to detect hot-reload.
        /// </summary>
        internal static bool InGameOrEditor() =>
            SceneManager.GetActiveScene().name != "IntroScreen" &&
            SceneManager.GetActiveScene().name != "Startup";

        [UsedImplicitly]
        public void OnEnabled() {
            Log.InfoFormat(
                "TM:PE enabled. Version {0}, Build {1} {2} for game version {3}.{4}.{5}-f{6}",
                VersionString,
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

            Log._Debug("Scene is " + SceneManager.GetActiveScene().name);

            InGameHotReload = InGameOrEditor();

            HarmonyHelper.EnsureHarmonyInstalled();

            if(InGameHotReload) {
                SerializableDataExtension.Load();
                LoadingExtension.Load();
            }

#if DEBUG
            const bool installHarmonyASAP = false; // set true for fast testing
            if (installHarmonyASAP)
                HarmonyHelper.DoOnHarmonyReady(delegate () { Patcher.Create().Install(); });
#endif
        }

        [UsedImplicitly]
        public void OnDisabled() {
            Log.Info("TM:PE disabled.");
            LoadingManager.instance.m_introLoaded -= CheckForIncompatibleMods;
            LocaleManager.eventLocaleChanged -= Translation.HandleGameLocaleChange;
            Translation.IsListeningToGameLocaleChanged = false; // is this necessary?

            if (InGameOrEditor() && LoadingExtension.IsGameLoaded) {
                //Hot Unload
                LoadingExtension.Unload();
            }
        }

        [UsedImplicitly]
        public void OnSettingsUI(UIHelperBase helper) {
            // Note: This bugs out if done in OnEnabled(), hence doing it here instead.
            if (!Translation.IsListeningToGameLocaleChanged) {
                Translation.IsListeningToGameLocaleChanged = true;
                LocaleManager.eventLocaleChanged += new LocaleManager.LocaleChangedHandler(Translation.HandleGameLocaleChange);
            }
            Options.MakeSettings(helper);
        }

        private static void CheckForIncompatibleMods() {
            ModsCompatibilityChecker mcc = new ModsCompatibilityChecker();
            mcc.PerformModCheck();
        }
    }
}
