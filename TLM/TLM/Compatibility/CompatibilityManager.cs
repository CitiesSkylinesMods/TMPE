namespace TrafficManager.Compatibility {
    using ColossalFramework;
    using ColossalFramework.Plugins;
    using static ColossalFramework.Plugins.PluginManager;
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using TrafficManager.Compatibility.Struct;
    using UnityEngine;
    using UnityEngine.SceneManagement;

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
        /// Is the compatibility checker paused? We pause it when active scene is not "MainMenu".
        /// </summary>
        private static bool paused_ = true;

        /// <summary>
        /// Tracks if a game restart is required (eg. after disabling/unsubscribing mods).
        /// </summary>
        private static bool restartRequired_ = false;

        /// <summary>
        /// Stores the original value of <see cref="LauncherLoginData.instance.m_continue"/>.
        /// </summary>
        private static bool autoContinue_ = false;

        /// <summary>
        /// Initializes static members of the <see cref="CompatibilityManager"/> class.
        /// </summary>
        static CompatibilityManager() {
            // Prevent vanilla autoloading last savegame
            PreventAutoContinue();

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
            // Abort if this is an in-game hotload
            // todo

            if (UIView.GetAView() != null) {
                // TM:PE enabled via Main Menu > Content Manager so run now
                Log.Info("CompatibilityManager.Activate()");
                paused_ = false;
                PerformChecks();
            } else {
                // TM:PE was already enabled on game load, or via mod autoenabler;
                // we must wait for main menu before doing anything else
                Log.Info("CompatibilityManager.Activate(): Waiting for main menu...");
                paused_ = true;
                LoadingManager.instance.m_introLoaded += OnIntroLoaded;
            }

            // Pause the compatibility checker if scene changes to something other than "MainMenu"
            SceneManager.activeSceneChanged += OnSceneChanged;
        }

        /// <summary>
        /// Removes any event listeners rendering the compatibility manager inactive.
        /// </summary>
        public static void Deactivate() {
            Log.Info("CompatibilityManager.Deactivate()");
            LoadingManager.instance.m_introLoaded -= OnIntroLoaded;
            Singleton<PluginManager>.instance.eventPluginsChanged -= OnPluginsChanged;
            SceneManager.activeSceneChanged -= OnSceneChanged;
        }

        /// <summary>
        /// Triggered when app intro screens have finished.
        /// </summary>
        private static void OnIntroLoaded() {
            LoadingManager.instance.m_introLoaded -= OnIntroLoaded;
            paused_ = false;
            PerformChecks();
        }

        /// <summary>
        /// Triggered when plugins change.
        /// </summary>
        private static void OnPluginsChanged() {
            if (!paused_) {
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
        /// <param name="current">The current <see cref="Scene"/> (seems to always be empty).</param>
        /// <param name="next">The <see cref="Scene"/> being transitioned to.</param>
        private static void OnSceneChanged(Scene current, Scene next) {
            Log.InfoFormat(
                "CompatibilityManager.OnSceneChange('{1}')",
                next.name);
            paused_ = next.name != "MainMenu";
        }

        /// <summary>
        /// Adds listener for plugin manager subscription change event.
        /// </summary>
        private static void ListenForSubscriptionChange() {
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
        private static bool CanWeDoStuff() {
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

            return !paused_;
        }

        /// <summary>
        /// Exits the game to desktop.
        /// </summary>
        private static void ExitToDesktop() {
            // Don't exit to desktop if we're in-game!
            if (paused_) {
                return;
            }
            paused_ = true;

            // Check we're not already quitting
            if (Singleton<LoadingManager>.instance.m_applicationQuitting) {
                return;
            }

            Singleton<LoadingManager>.instance.QuitApplication();
        }

        /// <summary>
        /// Attempt to halt the Paradox Launcher autoload sequence.
        /// Otherwise the user will not see any compatibility warnings.
        /// </summary>
        private static void PreventAutoContinue() {
            Log.Info("CompatibilityManager.PreventAutoContinue()");
            try {
                autoContinue_ = LauncherLoginData.instance.m_continue;
                LauncherLoginData.instance.m_continue = false;
            }
            catch {
                Log.Info(" - Failed!");
            }
        }

        /// <summary>
        /// If tests pass with no issues, we can resume launcher auto-continue
        /// if applicable.
        /// </summary>
        private static void ResumeAutoContinue() {
            if (autoContinue_) {
                Log.Info("CompatibilityManager.ResumeAutoContinue()");
                autoContinue_ = false;

                try {
                    MainMenu menu = GameObject.FindObjectOfType<MainMenu>();
                    if (menu != null) {
                        menu.m_BackgroundImage.zOrder = int.MaxValue;
                        menu.Invoke("AutoContinue", 2.5f);
                    }
                }
                catch {
                    Log.Info(" - Failed!");
                }
            }
        }

        /// <summary>
        /// Runs through entire compatibility checker sequence
        /// </summary>
        private static void PerformChecks() {
            Log.InfoFormat(
                "CompatibilityManager.PerformChecks() GUID = {0}",
                SelfGuid);

            // Check game version is what we expect it to be.
            if (!Check.Versions.Verify(TrafficManagerMod.ExpectedGameVersion, CurrentGameVersion)) {
                //todo: show warning about game version
            }

            // Verify that there are no mod incompatibilites which could
            // break the game, conflict with TM:PE, or cause minor problems.
            // Also check if there are multiple TM:PE.
            if (!Check.Mods.Verify(
                out Dictionary<PluginInfo, ModDescriptor> results,
                out int minor,
                out int major,
                out int critical,
                out int candidate)) {

                restartRequired_ = major > 0 || critical > 0 || candidate > 1;

                // todo: deal with incompatibilities
            }

            // If a restart is not yet required, check for zombie assemblies
            // which are the main cause of save/load issues.
            if (!restartRequired_ && !Check.Assemblies.Verify()) {

                restartRequired_ = true;

                // todo: show warning about settings loss
            }

            Check.DLCs.Verify();

            if (restartRequired_) {
                autoContinue_ = false;
            } else {
                ListenForSubscriptionChange();
                ResumeAutoContinue();
            }
        }
    }
}
