namespace TrafficManager {
    using ColossalFramework.Globalization;
    using CSUtil.Commons;
    using ICities;
    using JetBrains.Annotations;
    using System.Reflection;
    using System;
    using TrafficManager.Compatibility;
    using TrafficManager.State;
    using TrafficManager.UI;
    using UnityEngine.SceneManagement;

    /// <summary>
    /// The main class of the mod, which gets instantiated by the game engine.
    /// </summary>
    public class TrafficManagerMod : IUserMod {
#if LABS
        /// <summary>
        /// Build configuration <c>RELEASE LABS</c>.
        /// </summary>
        public const string BRANCH = "LABS";
#elif DEBUG
        /// <summary>
        /// Build configuration <c>DEBUG</c>, <c>TRACE</c>, etc.
        /// </summary>
        public const string BRANCH = "DEBUG";
#else
        /// <summary>
        /// Build configuration <c>RELEASE</c>.
        /// </summary>
        public const string BRANCH = "STABLE";
#endif

        /// <summary>
        /// Defines the game version that this version of TM:PE is expecting.
        /// See <see cref="CompatibilityManager.PerformChecks()"/> for more info.
        /// Update when necessary.
        /// </summary>
        public static readonly Version ExpectedGameVersion = new Version(1, 12, 3);

        /// <summary>
        /// The full mod name including version number and branch. This is also shown on the TM:PE toolbar in-game.
        /// </summary>
        public static readonly string ModName = "TM:PE " + VersionString + " " + BRANCH;

        /// <summary>
        /// Gets the mod version as defined by <c>SharedAssemblyInfo.cs</c>.
        /// Update with each release to workshop.
        /// </summary>
        public static Version ModVersion => typeof(TrafficManagerMod).Assembly.GetName().Version;

        /// <summary>
        /// Gets the string represetnation of <see cref="ModVersion"/>.
        /// </summary>
        public static string VersionString => ModVersion.ToString(3);

        /// <summary>
        /// Gets the mod name, which is shown in Content Manager > Mods, and also Options > Mod Settings.
        /// </summary>
        public string Name => ModName;

        /// <summary>
        /// Gets the description of the mod shown in Content Manager > Mods.
        /// </summary>
        public string Description => "Manage your city's traffic";

        /// <summary>
        /// This method is called by the game when the mod is enabled.
        /// </summary>
        [UsedImplicitly]
        public void OnEnabled() {
            Log.InfoFormat(
                "{0} designed for Cities: Skylines {1}",
                ModName,
                ExpectedGameVersion.ToString(3));

            Log.InfoFormat(
                "TrafficManagerMod.OnEnabled() Scene = {0}",
                SceneManager.GetActiveScene().name);

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

            // Run pre-flight compatibility checks
            CompatibilityManager.Activate();
        }

        /// <summary>
        /// This method is called by the game when the mod is disabled.
        /// </summary>
        [UsedImplicitly]
        public void OnDisabled() {
            Log.InfoFormat(
                "TrafficManagerMod.OnDisabled() Scene = {0}",
                SceneManager.GetActiveScene().name);

            CompatibilityManager.Deactivate();

            LocaleManager.eventLocaleChanged -= Translation.HandleGameLocaleChange;
            Translation.IsListeningToGameLocaleChanged = false; // is this necessary?

            if (LoadingExtension.InGame() && LoadingExtension.Instance != null) {
                //Hot reload Unloading
                LoadingExtension.Instance.OnLevelUnloading();
                LoadingExtension.Instance.OnReleased();
            }
        }

        /// <summary>
        /// This method is called by the game to initialise the mod settings screen.
        /// </summary>
        /// <param name="helper">A helper for creating UI components.</param>
        [UsedImplicitly]
        public void OnSettingsUI(UIHelperBase helper) {
            Log.InfoFormat(
                "TrafficManagerMod.OnSettingsUI() Scene = {0}",
                SceneManager.GetActiveScene().name);

            // Note: This bugs out if done in OnEnabled(), hence doing it here instead.
            if (!Translation.IsListeningToGameLocaleChanged) {
                Translation.IsListeningToGameLocaleChanged = true;
                LocaleManager.eventLocaleChanged += new LocaleManager.LocaleChangedHandler(Translation.HandleGameLocaleChange);
            }
            Options.MakeSettings(helper);
        }
    }
}
