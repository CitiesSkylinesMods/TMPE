namespace TrafficManager.State {
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using ICities;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.UI;
    using TrafficManager.UI.Helpers;

    public static class GeneralTab_SimulationGroup {

        private static UIDropDown _simulationAccuracyDropdown;

        public static CheckboxOption InstantEffects =
            new (nameof(Options.instantEffects), Options.PersistTo.Savegame) {
                Label = "General.Checkbox:Apply AI changes right away",
            };

        public static void SetSimulationAccuracy(SimulationAccuracy value) {
            Options.simulationAccuracy = value;
            if (_simulationAccuracyDropdown != null) {
                _simulationAccuracyDropdown.selectedIndex = (int)value;
            }
        }

        internal static void AddUI(UIHelperBase tab) {

            var group = tab.AddGroup(T("General.Group:Simulation"));

            AddSimulationAccuracyDropDown(group);
            InstantEffects.AddUI(group);
        }

        private static string T(string key) => Translation.Options.Get(key);

        private static void AddSimulationAccuracyDropDown(UIHelperBase group) {
            string[] simPrecisionOptions = new[] {
                T("General.Dropdown.Option:Very low"),
                T("General.Dropdown.Option:Low"),
                T("General.Dropdown.Option:Medium"),
                T("General.Dropdown.Option:High"),
                T("General.Dropdown.Option:Very high"),
            };
            _simulationAccuracyDropdown = group.AddDropdown(
                text: T("General.Dropdown:Simulation accuracy") + ":",
                options: simPrecisionOptions,
                defaultSelection: (int)Options.simulationAccuracy,
                eventCallback: OnSimulationAccuracyChanged) as UIDropDown;
        }

        private static void OnSimulationAccuracyChanged(int value) {
            if (!Options.IsGameLoaded()) return;

            Log.Info($"Simulation accuracy changed to {value}");
            Options.simulationAccuracy = (SimulationAccuracy)value;
        }
    }
}