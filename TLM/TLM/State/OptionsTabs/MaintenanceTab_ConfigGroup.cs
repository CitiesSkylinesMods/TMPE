namespace TrafficManager.State {
    using ICities;
    using TrafficManager.UI;
    using TrafficManager.UI.Helpers;
#if DEBUG
    using TrafficManager.UI.DebugSwitches;
#endif

    public static class MaintenanceTab_ConfigGroup {

        public static ActionButton ReloadGlobalConfig = new() {
            Label = "Maintenance.Button:Reload global configuration",
            Handler = OnReloadGlobalConfigClicked,
        };
        public static ActionButton ResetGlobalConfig = new() {
            Label = "Maintenance.Button:Reset all configuration",
            Handler = OnResetGlobalConfigClicked,
        };

        public static ActionButton SetAsDefaultForNewGames = new() {
            Label = "Maintenance.Button:Set as default for new games",
            Handler = OnSetAsDefaultForNewGamesClicked,
        };

#if DEBUG
        public static ActionButton DebugSwiches = new() {
            Translator = key => key,
            Label = "Debug Switches",
            Handler = DebugSwitchPanel.OpenModal,
        };
#endif

        internal static void AddUI(UIHelperBase tab) {
            var group = tab.AddGroup(T("Group:Configuration"));

            ReloadGlobalConfig.AddUI(group);
            ResetGlobalConfig.AddUI(group);
#if DEBUG
            DebugSwiches.AddUI(group);
#endif
            SetAsDefaultForNewGames.AddUI(group);
        }

        private static string T(string key) => Translation.Options.Get(key);

        private static void OnReloadGlobalConfigClicked()
            => GlobalConfig.Reload();

        private static void OnResetGlobalConfigClicked()
            => GlobalConfig.Reset(oldConfig: null);
        private static void OnSetAsDefaultForNewGamesClicked() {
            GlobalConfig.Instance.SavedGameOptions = SavedGameOptions.Instance;
            GlobalConfig.WriteConfig();
        }
    }
}