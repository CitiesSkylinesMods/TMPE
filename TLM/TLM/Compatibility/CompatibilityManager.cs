namespace TrafficManager.Compatibility {
    using ColossalFramework;
    using ColossalFramework.Plugins;
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using ICities;
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using System.Text;
    using static ColossalFramework.Plugins.PluginManager;

    /// <summary>
    /// Manages pre-flight checks for known incompatible mods.
    /// </summary>
    public static class CompatibilityManager {

        /// <summary>
        /// The Guid of the executing assembly (used to filter self from incompatibility checks).
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "RAS0002:Readonly field for a non-readonly struct", Justification = "Rarely used.")]
        public static readonly Guid SelfGuid;

        /// <summary>
        /// The current game version as a <see cref="Version"/> instance.
        /// </summary>
        internal static readonly Version CurrentGameVersion;

        /// <summary>
        /// Keeps track of whether we need to auto-load the last save if no compatibility issues found.
        /// </summary>
        internal static bool AutoLoadLastSave;

        /// <summary>
        /// Initializes static members of the <see cref="CompatibilityManager"/> class.
        /// </summary>
        static CompatibilityManager() {
            // Store current launcher auto-continue flag
            try {
                AutoLoadLastSave = LauncherLoginData.instance.m_continue;
                LauncherLoginData.instance.m_continue = false;
            }
            catch {
                Log.Info("");
                AutoLoadLastSave = false;
            }

            // Translate current game verison in to Version instance
            CurrentGameVersion = new Version(
                Convert.ToInt32(BuildConfig.APPLICATION_VERSION_A),
                Convert.ToInt32(BuildConfig.APPLICATION_VERSION_B),
                Convert.ToInt32(BuildConfig.APPLICATION_VERSION_C));

            // GUID for currently executing assembly
            SelfGuid = Assembly.GetExecutingAssembly().ManifestModule.ModuleVersionId;
        }

        /// <summary>
        /// Activates the Compatibility Manager which will trigger compatibility
        /// checks when applicable.
        /// </summary>
        public static void Activate() {
            if (UIView.GetAView() != null) {
                // TM:PE enabled via Main Menu > Content Manager so run now
                PerformChecks();
            } else {
                // TM:PE was already enabled on game load, or via mod autoenabler;
                // we must wait for main menu before doing anything else
                LoadingManager.instance.m_introLoaded += PerformChecks;
            }
        }

        /// <summary>
        /// Removes any event listeners rendering the compatibility manager inactive.
        /// </summary>
        public static void Deactivate() {
            LoadingManager.instance.m_introLoaded -= PerformChecks;
            Singleton<PluginManager>.instance.eventPluginsChanged -= PerformChecks;
        }

        /// <summary>
        /// Checks to see current game version is the one TM:PE is expecting.
        /// </summary>
        /// <returns>Returns <c>true</c> if wrong game version, otherwise <c>false</c>.</returns>
        internal static bool IsUnexpectedGameVersion() {
            return CurrentGameVersion != TrafficManagerMod.ExpectedGameVersion;
        }

        /// <summary>
        /// Checks to see if there are multiple TM:PE assemblies loaded in RAM, which can
        /// cause severe save/load issues.
        /// See: <c>krzychu124/Cities-Skylines-Traffic-Manager-President-Edition #608 and #211</c>.
        /// </summary>
        /// 
        /// <returns>Returns <c>true</c> if multiple assemblies, otherwise <c>false</c>.</returns>
        internal static bool HasMultipleAssemblies() {
            // todo
            return false;
        }

        /// <summary>
        /// Displays a panel allowing user to choose which assembly they want to use.
        /// A game restart is required to make changes take effect.
        /// </summary>
        internal static void ShowAssemblyChooser() {
            // todo
        }

        /// <summary>
        /// Displays a panel allowing users to remove/disable incompatible mods.
        /// </summary>
        /// 
        /// <param name="critical">A dictionary of critical incompatibilities.</param>
        /// <param name="major">A dictionary of major incompatibilities.</param>
        /// <param name="minor">A dictionary of minor incompatibilities.</param>
        internal static void ShowModRemoverDialog(
                Dictionary<PluginInfo, string> critical,
                Dictionary<PluginInfo, string> major,
                Dictionary<PluginInfo, string> minor) {
            // todo
        }

        /// <summary>
        /// Adds listener for plugin manager subscription change event.
        /// </summary>
        internal static void ListenForSubscriptionChange() {
            // clear old listener if present (is this necessary? don't know enough about C# events)
            Singleton<PluginManager>.instance.eventPluginsChanged -= PerformChecks;
            // add listener
            Singleton<PluginManager>.instance.eventPluginsChanged += PerformChecks;
        }

        /// <summary>
        /// Does some checks to ensure we're not in-game or in-map editor or loading or quitting.
        /// </summary>
        /// 
        /// <returns>Returns <c>true</c> if safe to run tests, otherwise <c>false</c>.</returns>
        internal static bool CanWeDoStuff() {
            // make sure we're not loading a game/asset/etc
            if (Singleton<LoadingManager>.instance.m_currentlyLoading) {
                return false;
            }

            // make sure we're not exiting to desktop
            if (Singleton<LoadingManager>.instance.m_applicationQuitting) {
                return false;
            }

            return LoadingExtension.NotInGameOrEditor;
        }

        /// <summary>
        /// Exits the game to desktop after a very short delay.
        /// </summary>
        internal static void ExitToDesktop() {
            // Don't exit to desktop if we're in-game!
            if (!CanWeDoStuff()) {
                return;
            }
            Singleton<LoadingManager>.instance.QuitApplication();
        }

        /// <summary>
        /// Log status of relevant DLCs (content and music packs are ignored).
        /// </summary>
        internal static void LogRelevantDLC() {
            StringBuilder sb = new StringBuilder("DLC Activation:\n\n", 50);
        }

        /// <summary>
        /// If no compatibility issues are found, check to see if the auto-load last game
        /// setting was active and if so load the last savegame.
        /// </summary>
        internal static void LoadLastSaveGame() {
            // Make sure we don't auto-load again
            AutoLoadLastSave = false;
            // Don't auto-load if we're in-game!
            if (!CanWeDoStuff()) {
                return;
            }
            //MainMenu.Invoke("AutoContinue", 2.5f);
        }

        /// <summary>
        /// Runs through entire compatibility checker sequence
        ///
        /// Note: This method is either invoked directly, or via an event, hence being static.
        /// </summary>
        private static void PerformChecks() {
            // Skip checks if we're in-game
            if (!CanWeDoStuff()) {
                return;
            }

            Log.InfoFormat(
                "CompatibilityManager.PerformChecks() GUID = {0}",
                SelfGuid);

            LogRelevantDLC();

            if (HasMultipleAssemblies()) {
                ShowAssemblyChooser();
            } else if (ModScanner.DetectIncompatibleMods(
                out Dictionary<PluginInfo, string> critical,
                out Dictionary<PluginInfo, string> major,
                out Dictionary<PluginInfo, string> minor)) {
                ShowModRemoverDialog(critical, major, minor);
            } else {
                ListenForSubscriptionChange();
            }
        }
    }
}
