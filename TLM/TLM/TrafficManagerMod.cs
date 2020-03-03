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

    public class TrafficManagerMod : IUserMod {
#if LABS
        public const string BRANCH = "LABS";
#elif DEBUG
        public const string BRANCH = "DEBUG";
#else
        public const string BRANCH = "STABLE";
#endif

        // These values from `BuildConfig` class (`APPLICATION_VERSION` constants) in game file `Managed/Assembly-CSharp.dll` (use ILSpy to inspect them)
        public const uint GAME_VERSION = 185066000u;
        public const uint GAME_VERSION_A = 1u;
        public const uint GAME_VERSION_B = 12u;
        public const uint GAME_VERSION_C = 3u;
        public const uint GAME_VERSION_BUILD = 2u;

        // Use SharedAssemblyInfo.cs to modify TM:PE version
        // External mods (eg. CSUR Toolbox) reference the versioning for compatibility purposes
        public static Version ModVersion => typeof(TrafficManagerMod).Assembly.GetName().Version;

        // used for in-game display
        public static string VersionString => ModVersion.ToString(3);

        public static readonly string ModName = "TM:PE " + VersionString + " " + BRANCH;

        public string Name => ModName;

        public string Description => "Manage your city's traffic";

        internal static TrafficManagerMod Instance = null;

        internal bool InGameHotReload { get; set; } = false;

        internal static bool InGame() => SceneManager.GetActiveScene().name == "Game";

        internal static bool listeningToLocaleChange_ = false;

        [UsedImplicitly]
        public void OnEnabled() {
            Instance = this;
            InGameHotReload = InGame();

            Lifecycle.Instance.OnEnabled(InGameHotReload);

            if (!InGameHotReload) {
                // check for incompatible mods
                if (UIView.GetAView() != null) {
                    // when TM:PE is enabled in content manager
                    Lifecycle.Instance.OnCompatibilityCheck();
                } else {
                    // or when game first loads if TM:PE was already enabled
                    LoadingManager.instance.m_introLoaded += CheckForIncompatibleMods;
                }
            }
        }

        [UsedImplicitly]
        public void OnDisabled() {
            LoadingManager.instance.m_introLoaded -= CheckForIncompatibleMods;
            LocaleManager.eventLocaleChanged -= GameLocaleChanged;

            bool hotUnload = InGame() && LoadingExtension.Instance != null;

            if (hotUnload) {
                //Hot reload Unloading
                LoadingExtension.Instance.OnLevelUnloading();
                LoadingExtension.Instance.OnReleased();
            }

            Lifecycle.Instance.OnDisabled(hotUnload);

            Instance = null;
        }

        [UsedImplicitly]
        public void OnSettingsUI(UIHelperBase helper) {
            if (!listeningToLocaleChange_) {
                listeningToLocaleChange_ = true;
                LocaleManager.eventLocaleChanged += new LocaleManager.LocaleChangedHandler(GameLocaleChanged);
                GameLocaleChanged(); // call on first use
            }

            Lifecycle.Instance.OnSettings(helper, InGame());
        }

        private static void GameLocaleChanged() {
            Lifecycle.Instance.OnLocaleChange(LocaleManager.instance.language);
        }

        private static void CheckForIncompatibleMods() {
            LoadingManager.instance.m_introLoaded -= CheckForIncompatibleMods;
            Lifecycle.Instance.OnCompatibilityCheck();
        }
    }
}
