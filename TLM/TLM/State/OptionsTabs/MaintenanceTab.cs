namespace TrafficManager.State {
    using TrafficManager.UI.Helpers;
    using TrafficManager.UI;

    public static class MaintenanceTab {

        internal static void MakeSettings_Maintenance(ExtUITabstrip tabStrip) {

          var tab = tabStrip.AddTabPage(T("Tab:Maintenance"));

            MaintenanceTab_ToolsGroup.AddUI(tab);

            MaintenanceTab_FeaturesGroup.AddUI(tab);

            MaintenanceTab_DespawnGroup.AddUI(tab);

            MaintenanceTab_ConfigGroup.AddUI(tab);
        }

        private static string T(string key) => Translation.Options.Get(key);
    }
}
