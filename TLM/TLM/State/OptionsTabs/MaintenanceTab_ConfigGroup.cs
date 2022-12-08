namespace TrafficManager.State {
    using ICities;
    using TrafficManager.UI;
    using TrafficManager.UI.Helpers;
#if DEBUG
    using TrafficManager.UI.DebugSwitches;
    using UnityEngine;
#endif

    public static class MaintenanceTab_ConfigGroup {

        public static ActionButton ReloadGlobalConfig = new() {
            Label = "Maintenance.Button:Reload global configuration",
            Handler = OnReloadGlobalConfigClicked,
        };
        public static ActionButton ResetGlobalConfig = new() {
            Label = "Maintenance.Button:Reset global configuration",
            Handler = OnResetGlobalConfigClicked,
        };

        public static ActionButton SetAsDefaultForNewGames = new() {
            Label = "Maintenance.Button:Set as default for new games",
            Handler = OnSetAsDefaultForNewGamesClicked,
        };
        public static ActionButton ResetDefaultsForNewGames = new() {
            Label = "Maintenance.Button:Reset defaults for new games",
            Handler = SavedGameOptions.ResetDefaults,
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
            SetAsDefaultForNewGames.AddUI(group);
            ResetDefaultsForNewGames.AddUI(group);
#if DEBUG
            DebugSwiches.AddUI(group);
#endif
        }

        private static string T(string key) => Translation.Options.Get(key);

        private static void OnReloadGlobalConfigClicked()
            => GlobalConfig.Reload();

        private static void OnResetGlobalConfigClicked()
            => GlobalConfig.Reset(oldConfig: null);
        private static void OnSetAsDefaultForNewGamesClicked() {
            SavedGameOptions.Instance.SerializeDefaults();
        }
    }
}