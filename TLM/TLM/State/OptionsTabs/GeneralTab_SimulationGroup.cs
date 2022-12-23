namespace TrafficManager.State {
    using ICities;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.UI;
    using TrafficManager.UI.Helpers;

    public static class GeneralTab_SimulationGroup {

        public static DropDownOption<SimulationAccuracy> SimulationAccuracy =
            new(nameof(SavedGameOptions.simulationAccuracy), Scope.Savegame) {
                Label = "General.Dropdown:Simulation accuracy",
            };

        internal static void AddUI(UIHelperBase tab) {
            var group = tab.AddGroup(T("General.Group:Simulation"));
            SimulationAccuracy.AddUI(group);
        }

        private static string T(string key) => Translation.Options.Get(key);
    }
}