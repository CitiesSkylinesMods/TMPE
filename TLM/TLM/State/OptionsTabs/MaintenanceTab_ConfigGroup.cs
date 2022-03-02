namespace TrafficManager.State {
    using ICities;
    using TrafficManager.UI;
    using TrafficManager.UI.Helpers;

    public static class MaintenanceTab_ConfigGroup {

        public static ActionButton ReloadGlobalConfig = new() {
            Label = "Maintenance.Button:Reload global configuration",
            Handler = OnReloadGlobalConfigClicked,
        };
        public static ActionButton ResetGlobalConfig = new() {
            Label = "Maintenance.Button:Reset global configuration",
            Handler = OnResetGlobalConfigClicked,
        };

        internal static void AddUI(UIHelperBase tab) {

            var group = tab.AddGroup(T("Group:Configuration"));

            ReloadGlobalConfig.AddUI(group);
            ResetGlobalConfig.AddUI(group);
        }

        private static string T(string key) => Translation.Options.Get(key);

        private static void OnReloadGlobalConfigClicked()
            => GlobalConfig.Reload();

        private static void OnResetGlobalConfigClicked()
            => GlobalConfig.Reset(oldConfig: null, resetAll: true);
    }
}