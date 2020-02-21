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
        /// When <c>true</c>, don't perform checks or show UI.
        /// </summary>
        private static bool paused_ = true;

        /// <summary>
        /// When <c>true</c>, a game restart is required.
        /// </summary>
        private static bool restartRequired_ = false;

        /// <summary>
        /// When <c>true</c>, user wants to auto-load most recent save.
        /// </summary>
        private static bool autoContinue_ = false;

        /// <summary>
        /// Initializes static members of the <see cref="CompatibilityManager"/> class.
        /// </summary>
        static CompatibilityManager() {
            // Prevent vanilla autoloading last savegame, but remember if user wanted to
            StopLauncherAutoContinue();

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
            SceneManager.activeSceneChanged -= OnSceneChanged;
            LoadingManager.instance.m_introLoaded -= OnIntroLoaded;
            Singleton<PluginManager>.instance.eventPluginsChanged -= OnPluginsChanged;
            Singleton<PluginManager>.instance.eventPluginsStateChanged -= OnPluginsChanged;
        }

        /// <summary>
        /// Checks and logs:
        /// 
        /// * Game version
        /// * Incompatible mods
        /// * Multiple TM:PE versions
        /// * Zombie assemblies
        /// * Traffic-affecting DLCs
        /// </summary>
        private static void PerformChecks() {
            Log.InfoFormat(
                "CompatibilityManager.PerformChecks() GUID = {0}",
                SelfGuid);

            // Check game version is what we expect it to be.
            if (!Check.Versions.Verify(
                TrafficManagerMod.ExpectedGameVersion,
                Check.Versions.GetGameVersion())) {

                autoContinue_ = false;

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
                ListenForPluginChanges();
                ResumeLauncherAutoContinue();
            }
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
        /// </summary>
        /// 
        /// <param name="current">The current <see cref="Scene"/> (seems to always be empty).</param>
        /// <param name="next">The <see cref="Scene"/> being transitioned to.</param>
        private static void OnSceneChanged(Scene current, Scene next) {
            Log.InfoFormat(
                "CompatibilityManager.OnSceneChange('{0}','{1}')",
                current.name,
                next.name);

            paused_ = next.name != "MainMenu";
        }

        /// <summary>
        /// Adds listener for plugin manager subscription/state change events.
        /// </summary>
        private static void ListenForPluginChanges() {
            Log.Info("CompatibilityManager.ListenForSubscriptionChange()");

            // clear old listener if present (is this necessary? don't know enough about C# events)
            Singleton<PluginManager>.instance.eventPluginsChanged -= OnPluginsChanged;
            Singleton<PluginManager>.instance.eventPluginsStateChanged -= OnPluginsChanged;

            // add listener
            Singleton<PluginManager>.instance.eventPluginsChanged += OnPluginsChanged;
            Singleton<PluginManager>.instance.eventPluginsStateChanged += OnPluginsChanged;
        }

        /*
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
        */

        /// <summary>
        /// Halt the Paradox Launcher 
        /// Otherwise the user will not see any compatibility warnings.
        /// </summary>
        private static void StopLauncherAutoContinue() {
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
        /// Auto-load most recent save if the launcher was set to autocontinue.
        /// </summary>
        private static void ResumeLauncherAutoContinue() {
            Log.InfoFormat(
                "CompatibilityManager.ResumeAutoContinue() {0}",
                autoContinue_);

            if (autoContinue_) {
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
    }
}
