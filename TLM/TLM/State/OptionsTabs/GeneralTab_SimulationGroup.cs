namespace TrafficManager.State {
    using ICities;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.UI.Helpers;

    public static class GeneralTab_SimulationGroup {

        public static DropDownOption<SimulationAccuracy> SimulationAccuracy =
            new(nameof(Options.simulationAccuracy), Options.PersistTo.Savegame) {
                Label = "General.Dropdown:Simulation accuracy",
            };

        internal static void AddUI(UIHelperBase tab) {
            var group = tab.AddGroup("General.Group:Simulation");
            SimulationAccuracy.AddUI(group);
        }
    }
}