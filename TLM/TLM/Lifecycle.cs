namespace TrafficManager {
    using CSUtil.Commons;
    using ICities;
    using TrafficManager.State;
    using TrafficManager.UI;

    /// <summary>
    /// This class manages the lifecycle of TMPE mod.
    ///
    /// It also serves as an initial design draft for HarmonyLifecycle mod.
    /// Code commenting is verbose; that would get moved to docs somwhere.
    /// </summary>
    public class Lifecycle { // : ILifecycle

        /// <summary>
        /// The Lifecycle instance; may be <c>null</c>.
        /// </summary>
        private static Lifecycle instance_;

        /// <summary>
        /// Gets the <see cref="Lifecycle"/> instance (creates one if necessary).
        /// </summary>
        public static Lifecycle Instance => instance_ ?? (instance_ = new Lifecycle());

        /// <summary>
        /// Called when the mod is enabled in one of the following ways:
        ///
        /// * Manually in Content Manager > Mods
        /// * Already enabled when Cities.exe starts
        /// * Auto-enabled upon subscription, by the Mod Autoenabler mod
        /// * Hot reload of a dev build.
        /// </summary>
        /// 
        /// <param name="hotLoad">If <c>true</c>, the mod was enabled due to hot reload.</param>
        public void OnEnabled(bool hotLoad) {
            if (hotLoad) {
                Log.Info("HOT RELOAD");
            } else {
                Temp.LogEnvironmentDetails();
            }
        }

        /// <summary>
        /// Called when locale needs updating:
        ///
        /// * Before OnSettings(), if not already called
        /// * When user changes game language.
        ///
        /// Note that lanauge mods often use non-standard language codes such as:
        ///
        /// * jaex --> ja
        /// * zh-cn --> zh
        /// * kr --> ko.
        /// </summary>
        /// 
        /// <param name="locale">A string representing the language code.</param>
        public void OnLocaleChange(string locale) {
            Translation.HandleGameLocaleChange();
        }

        /// <summary>
        /// Called when the game wants the mod to create its settings UI:
        ///
        /// * When mod is first enabled
        /// * Each time a city is loaded.
        ///
        /// The <paramref name="inGame"/> parameter can be used to adapt your settings
        /// screen depending on whether it's in-game or not.
        /// </summary>
        /// 
        /// <param name="helper">The <see cref="UIHelperBase"/> instance used to create the UI.</param>
        /// <param name="inGame">If <c>true</c>, the in-game settings screen should be created.</param>
        public void OnSettings(UIHelperBase helper, bool inGame) {
            // todo: instead of `inGame` bool, should we use an enum (or whatever) to
            // differentiate between in-game, in scneario, in editor, etc?
            // or have separate methods, eg. OnMainSettings(), OnGameSettings(), OnEditorSettings()?
            Options.MakeSettings(helper);
        }

        /// <summary>
        /// Called at an appropriate time to perform compatibility checks:
        ///
        /// * PluginManager has processed all mods
        /// * Intro screens have completed; UIView is available
        /// * App localisation services are available
        /// * NOT loading, unloading or exiting
        /// * NOT in game or editor.
        /// </summary>
        /// 
        /// <returns>Return <c>true</c> if environment is compatible, otherwise <c>false</c>.</returns>
        public bool OnCompatibilityCheck() {
            // todo: should we pass in game version as a `Version`?
            return Temp.CheckCompatibility();
        }


        /// <summary>
        /// Called when the mod is disabled in one of the following ways:
        ///
        /// * Manually in Content Manager > Mods
        /// * Unsubscribed while enabled
        /// * Hot reload of dev build causes hot unload of current build
        /// * Game exit to desktop.
        /// </summary>
        /// 
        /// <param name="hotUnload">If <c>true</c>, the mod was enabled due to hot reload.</param>
        public void OnDisabled(bool hotUnload) {
            Log.Info("TM:PE disabled.");
        }

    }
}
