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
    public class CompatibilityManager {

        /// <summary>
        /// The Guid of the executing assembly (used to filter self from incompatibility checks).
        /// </summary>
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
            StopLauncherAutoContinue();

            SelfGuid = Assembly.GetExecutingAssembly().ManifestModule.ModuleVersionId;
        }

        /// <summary>
        /// Run checks when possible to do so.
        /// </summary>
        public static void Activate() {
            Log._DebugFormat(
                "CompatibilityManager.Activate() Scene = {0}",
                SceneManager.GetActiveScene().name);

            // Abort if this is an in-game hot reload
            if (SceneManager.GetActiveScene().name == "Game") {
                Log._Debug("- Skipping due to in-game hot reload");
                paused_ = true;
                return;
            }

            paused_ = UIView.GetAView() == null;

            if (paused_) {
                Log._Debug("- Waiting for main menu...");
                LoadingManager.instance.m_introLoaded += OnIntroLoaded;
            } else {
                PerformChecks();
                SetEvents(true);
            }
        }

        /// <summary>
        /// Deactivates the compatibility checker.
        /// </summary>
        public static void Deactivate() {
            Log._Debug("CompatibilityManager.Deactivate()");

            paused_ = true;

            // todo: destroy IncompatibleMods.Instance.List

            SetEvents(false);
        }

        /// <summary>
        /// Removes all event listeners and, optionally, add all event listeners.
        /// </summary>
        /// 
        /// <param name="active">If <c>true</c> then event listeners are added.</param>
        private static void SetEvents(bool active) {

            SceneManager.activeSceneChanged -= OnSceneChanged;
            LoadingManager.instance.m_introLoaded -= OnIntroLoaded;
            Singleton<PluginManager>.instance.eventPluginsChanged -= OnPluginsChanged;
            Singleton<PluginManager>.instance.eventPluginsStateChanged -= OnPluginsChanged;

            if (active) {
                SceneManager.activeSceneChanged += OnSceneChanged;
                Singleton<PluginManager>.instance.eventPluginsChanged += OnPluginsChanged;
                Singleton<PluginManager>.instance.eventPluginsStateChanged += OnPluginsChanged;
            }
        }

        /// <summary>
        /// Checks and logs:
        /// 
        /// * Game version
        /// * Incompatible mods
        /// * Multiple TM:PE versions
        /// * Zombie assemblies
        /// * Traffic-affecting DLCs.
        /// </summary>
        private static void PerformChecks() {
            Log.InfoFormat(
                "CompatibilityManager.PerformChecks() GUID = {0}",
                SelfGuid);

            if (!Check.Versions.Verify(
                TrafficManagerMod.ExpectedGameVersion,
                Check.Versions.GetGameVersion())) {

                autoContinue_ = false;

                //todo: show warning about game version
            }

            if (!Check.Mods.Verify(
                out Dictionary<PluginInfo, ModDescriptor> results,
                out int minor,
                out int major,
                out int critical,
                out int candidate)) {

                restartRequired_ = true;

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
                ResumeLauncherAutoContinue();
            }
        }

        /// <summary>
        /// Triggered when app intro screens have finished.
        /// </summary>
        private static void OnIntroLoaded() {
            Log._Debug("CompatibilityManager.OnIntroLoaded()");

            paused_ = false;
            PerformChecks();
            SetEvents(true);
        }

        /// <summary>
        /// Triggered by plugin subscription/state change.
        /// </summary>
        private static void OnPluginsChanged() {
            Log._Debug("CompatibilityManager.OnPluginsChanged()");

            if (!paused_) {
                PerformChecks();
            }
        }

        /// <summary>
        /// Triggered by scene changes.
        /// </summary>
        /// 
        /// <param name="current">The current <see cref="Scene"/> (usually empty).</param>
        /// <param name="next">The <see cref="Scene"/> being transitioned to.</param>
        private static void OnSceneChanged(Scene current, Scene next) {
            Log._DebugFormat(
                "CompatibilityManager.OnSceneChanged('{0}','{1}')",
                current.name,
                next.name);

            paused_ = next.name != "MainMenu";
        }

        /*
        private static bool CanWeDoStuff() {
            if (SceneManager.GetActiveScene().name == "MainMenu") {
                return true;
            }

            // make sure we're not loading a game/asset/etc
            if (Singleton<LoadingManager>.instance.m_currentlyLoading) {
                return false;
            }

            // make sure we're not exiting to desktop
            if (Singleton<LoadingManager>.instance.m_applicationQuitting) {
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
            Log._Debug("CompatibilityManager.StopLauncherAutoContinue()");

            try {
                autoContinue_ = LauncherLoginData.instance.m_continue;
                LauncherLoginData.instance.m_continue = false;
            }
            catch (Exception e) {
                Log.ErrorFormat(" - Stop AutoContinue Failed:\n{0}", e.ToString());
            }
        }

        /// <summary>
        /// Auto-load most recent save if the launcher was set to autocontinue.
        /// </summary>
        private static void ResumeLauncherAutoContinue() {
            Log._DebugFormat(
                "CompatibilityManager.ResumeLauncherAutoContinue() {0}",
                autoContinue_);

            if (paused_ || Singleton<LoadingManager>.instance.m_applicationQuitting) {
                return;
            }

            if (autoContinue_) {
                paused_ = true;
                autoContinue_ = false;

                try {
                    MainMenu menu = GameObject.FindObjectOfType<MainMenu>();
                    if (menu != null) {
                        menu.m_BackgroundImage.zOrder = int.MaxValue;
                        menu.Invoke("AutoContinue", 2.5f);
                    }
                }
                catch (Exception e) {
                    Log.ErrorFormat(" - Resume AutoContinue Failed:\n{0}", e.ToString());
                }
            }
        }

        /// <summary>
        /// Exits the game to desktop.
        /// </summary>
        private static void ExitToDesktop() {
            Log._Debug("CompatibilityManager.ExitToDesktop()");

            if (paused_ || Singleton<LoadingManager>.instance.m_applicationQuitting) {
                return;
            }

            paused_ = true;
            autoContinue_ = false;

            SetEvents(false);

            Singleton<LoadingManager>.instance.QuitApplication();
        }
    }
}
