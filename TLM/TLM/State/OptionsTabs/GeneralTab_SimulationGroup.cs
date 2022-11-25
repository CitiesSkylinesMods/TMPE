namespace TrafficManager.State {
    using ICities;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.UI;
    using TrafficManager.UI.Helpers;

    public static class GeneralTab_SimulationGroup {

        public static DropDownOption<SimulationAccuracy> SimulationAccuracyOption =
            new(nameof(Options.simulationAccuracy), Options.PersistTo.Savegame) {
                Label = "General.Dropdown:Simulation accuracy",
                DefaultValue = SimulationAccuracy.VeryHigh,
            };

        internal static void AddUI(UIHelperBase tab) {
            var group = tab.AddGroup(T("General.Group:Simulation"));
            SimulationAccuracyOption.AddUI(group);
        }

        private static string T(string key) => Translation.Options.Get(key);
    }
}