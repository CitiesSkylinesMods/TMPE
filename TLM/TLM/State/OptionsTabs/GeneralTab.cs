namespace TrafficManager.State {
    using TrafficManager.API.Traffic.Enums;
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using ICities;
    using JetBrains.Annotations;
    using TrafficManager.UI;
    using TrafficManager.UI.Helpers;
    using TrafficManager.UI.WhatsNew;

    public static class GeneralTab {
        public static ActionButton WhatsNewButton = new() {
            Label = "What's New?",
            Handler = WhatsNew.OpenModal,
        };

        private static UICheckBox _instantEffectsToggle;

        [UsedImplicitly]
        private static UIDropDown _simulationAccuracyDropdown;

        private static string T(string key) => Translation.Options.Get(key);

        internal static void MakeSettings_General(ExtUITabstrip tabStrip) {
            UIHelper tab = tabStrip.AddTabPage(T("Tab:General"));
            UIHelperBase group;

#if DEBUG
            GeneralTab_DebugGroup.AddUI(tab);
#endif

            tab.AddSpace(5);
            WhatsNewButton.AddUI(tab);
            tab.AddSpace(5);

            GeneralTab_LocalisationGroup.AddUI(tab);

            group = tab.AddGroup(T("General.Group:Simulation"));

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

            _instantEffectsToggle = group.AddCheckbox(
                                       text: T("General.Checkbox:Apply AI changes right away"),
                                       defaultValue: Options.instantEffects,
                                       eventCallback: OnInstantEffectsChanged) as UICheckBox;

            GeneralTab_InterfaceGroup.AddUI(tab);

            GeneralTab_CompatibilityGroup.AddUI(tab);
        }

        private static void OnSimulationAccuracyChanged(int newAccuracy) {
            if (!Options.IsGameLoaded()) {
                return;
            }

            Log._Debug($"Simulation accuracy changed to {newAccuracy}");
            Options.simulationAccuracy = (SimulationAccuracy)newAccuracy;
        }

        private static void OnInstantEffectsChanged(bool newValue) {
            if (!Options.IsGameLoaded()) {
                return;
            }

            Log._Debug($"Instant effects changed to {newValue}");
            Options.instantEffects = newValue;
        }

        public static void SetSimulationAccuracy(SimulationAccuracy newAccuracy) {
            Options.simulationAccuracy = newAccuracy;
            if (_simulationAccuracyDropdown != null) {
                _simulationAccuracyDropdown.selectedIndex = (int)newAccuracy;
            }
        }

        public static void SetInstantEffects(bool value) {
            Options.instantEffects = value;

            if (_instantEffectsToggle != null) {
                _instantEffectsToggle.isChecked = value;
            }
        }
    } // end class
}
