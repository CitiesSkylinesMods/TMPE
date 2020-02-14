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
    using TrafficManager.Compatibility.Checks;
    using UnityEngine.SceneManagement;
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
        /// Is the compatibility checker paused? We pause it when active scene is not "MainMenu".
        /// </summary>
        internal static bool Paused = true;

        /// <summary>
        /// Initializes static members of the <see cref="CompatibilityManager"/> class.
        /// </summary>
        static CompatibilityManager() {
            // Store current launcher auto-continue flag
            try {
                AutoLoadLastSave = LauncherLoginData.instance.m_continue;
                LauncherLoginData.instance.m_continue = false;
                Log.Info($"CompatibilityManager.ctor(): AutoLoadLastSave = {AutoLoadLastSave}");
            } catch {
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
            // Pause the compatibility checker if scene changes to something other than "MainMenu"
            SceneManager.activeSceneChanged += OnSceneChange;

            // todo: avoid checks when hotloading in-game
            if (UIView.GetAView() != null) {
                // TM:PE enabled via Main Menu > Content Manager so run now
                Log.Info("CompatibilityManager.Activate()");
                Paused = false;
                PerformChecks();
            } else {
                // TM:PE was already enabled on game load, or via mod autoenabler;
                // we must wait for main menu before doing anything else
                Log.Info("CompatibilityManager.Activate(): Waiting for main menu...");
                Paused = true;
                LoadingManager.instance.m_introLoaded += OnIntroLoaded;
            }
        }

        /// <summary>
        /// Removes any event listeners rendering the compatibility manager inactive.
        /// </summary>
        public static void Deactivate() {
            Log.Info("CompatibilityManager.Deactivate()");
            LoadingManager.instance.m_introLoaded -= PerformChecks;
            Singleton<PluginManager>.instance.eventPluginsChanged -= OnPluginsChanged;
            SceneManager.activeSceneChanged -= OnSceneChange;
        }

        /// <summary>
        /// Triggered when app intro screens have finished.
        /// </summary>
        private static void OnIntroLoaded() {
            LoadingManager.instance.m_introLoaded -= OnIntroLoaded;
            Paused = false;
            PerformChecks();
        }

        /// <summary>
        /// Triggered when plugins change.
        /// </summary>
        private static void OnPluginsChanged() {
            if (!Paused) {
                PerformChecks();
            }
        }

        /// <summary>
        /// Triggered when scene changes.
        ///
        /// Pause the compatibility checker if scene is not "MainMenu".
        ///
        /// Note: Game does not trigger the event between intro screen
        ///       and first display of main menu.
        /// </summary>
        /// 
        /// <param name="current">The current <see cref="Scene"/>.</param>
        /// <param name="next">The <see cref="Scene"/> being transitioned to.</param>
        private static void OnSceneChange(Scene current, Scene next) {
            Log.InfoFormat(
                "CompatibilityManager.OnSceneChange('{1}')",
                next.name);
            Paused = next.name != "MainMenu";
        }

        /// <summary>
        /// Checks to see current game version is the one TM:PE is expecting.
        /// </summary>
        /// 
        /// <returns>Returns <c>true</c> if wrong game version, otherwise <c>false</c>.</returns>
        internal static bool CheckGameVersion() {
            Log.InfoFormat(
                "CompatibilityManager.CheckGameVersion(): Expect: {0}, Actual: {1}",
                TrafficManagerMod.ExpectedGameVersion.ToString(3),
                CurrentGameVersion.ToString(3));

            return CurrentGameVersion == TrafficManagerMod.ExpectedGameVersion;
        }

        /// <summary>
        /// Displays a panel allowing user to choose which assembly they want to use.
        /// A game restart is required to make changes take effect.
        /// </summary>
        internal static void ShowAssemblyChooser() {
            // todo
            Log.Info("CompatibilityManager.ShowAssemblyChooser()");
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
            Log.Info("CompatibilityManager.ShowModRemoverDialog()");
        }

        /// <summary>
        /// Adds listener for plugin manager subscription change event.
        /// </summary>
        internal static void ListenForSubscriptionChange() {
            Log.Info("CompatibilityManager.ListenForSubscriptionChange()");
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
            if (SceneManager.GetActiveScene().name == "MainMenu") {
                Log.Info("CompatibilityManager.CanWeDoStuff()? Yes, 'MainMenu' scene");
                return true;
            }

            // make sure we're not loading a game/asset/etc
            if (Singleton<LoadingManager>.instance.m_currentlyLoading) {
                Log.Info("CompatibilityManager.CanWeDoStuff()? No; currently loading");
                return false;
            }

            // make sure we're not exiting to desktop
            if (Singleton<LoadingManager>.instance.m_applicationQuitting) {
                Log.Info("CompatibilityManager.CanWeDoStuff()? No; currently quitting");
                return false;
            }

            return !Paused;
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
        /// If no compatibility issues are found, check to see if the auto-load last game
        /// setting was active and if so load the last savegame.
        /// </summary>
        internal static void LoadLastSaveGame() {
            Log.Info("CompatibilityManager.LoadLastSaveGame()");

            // Make sure we don't auto-load again
            AutoLoadLastSave = false;

            // Don't auto-load if we're somehow in-game!
            if (!CanWeDoStuff()) {
                return;
            }
            //MainMenu.Invoke("AutoContinue", 2.5f);
        }

        /// <summary>
        /// Hides main menu by moving background image to top layer (same as vanilla game does).
        /// </summary>
        internal static void HideMainMenu() {
            
            //MainMenu.m_BackgroundImage.zOrder = int.MaxValue;
        }

        /// <summary>
        /// Runs through entire compatibility checker sequence
        ///
        /// Note: This method is either invoked directly, or via an event, hence being static.
        /// </summary>
        private static void PerformChecks() {
            Log.InfoFormat(
                "CompatibilityManager.PerformChecks() GUID = {0}",
                SelfGuid);

            CheckDLCs.Scan();

            if (!CheckGameVersion()) {
                //todo
            } else if (CheckAssemblies.Scan()) {
                ShowAssemblyChooser();
            } else if (CheckMods.Scan(
                out Dictionary<PluginInfo, string> critical,
                out Dictionary<PluginInfo, string> major,
                out Dictionary<PluginInfo, string> minor)) {
                ShowModRemoverDialog(critical, major, minor);
            } else if (AutoLoadLastSave) {
                LoadLastSaveGame();
            } else {
                ListenForSubscriptionChange();
            }
        }
    }
}
