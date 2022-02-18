namespace TrafficManager.State {
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using ICities;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.Manager.Impl;
    using TrafficManager.UI.Helpers;
    using TrafficManager.UI;
    using static TrafficManager.Util.Shortcuts;

    public static class PoliciesTab {
        private static UIDropDown _vehicleRestrictionsAggressionDropdown;
        private static UICheckBox _banRegularTrafficOnBusLanesToggle;
        private static UICheckBox _highwayRulesToggle;
        private static UICheckBox _preferOuterLaneToggle;
        private static UICheckBox _evacBussesMayIgnoreRulesToggle;

        public static CheckboxOption NoDoubleCrossings =
            new CheckboxOption(nameof(NoDoubleCrossings)) {
                Label = "VR.Option:No double crossings", // at a segment to segment transition, only the smaller segment gets crossings
                Handler = JunctionRestrictionsUpdateHandler,
            };

        static void JunctionRestrictionsUpdateHandler(bool value ) =>
            JunctionRestrictionsManager.Instance.UpdateAllDefaults();

        internal static void MakeSettings_VehicleRestrictions(ExtUITabstrip tabStrip) {
            UIHelper panelHelper = tabStrip.AddTabPage(Translation.Options.Get("Tab:Policies & Restrictions"));

            PoliciesTab_AtJunctionsGroup.AddUI(panelHelper);

            UIHelperBase onRoadsGroup =
                panelHelper.AddGroup(Translation.Options.Get("VR.Group:On roads"));

            _vehicleRestrictionsAggressionDropdown
                = onRoadsGroup.AddDropdown(
                      Translation.Options.Get("VR.Dropdown:Vehicle restrictions aggression") + ":",
                      new[] {
                                Translation.Options.Get("VR.Dropdown.Option:Low Aggression"),
                                Translation.Options.Get("VR.Dropdown.Option:Medium Aggression"),
                                Translation.Options.Get("VR.Dropdown.Option:High Aggression"),
                                Translation.Options.Get("VR.Dropdown.Option:Strict"),
                            },
                      (int)Options.vehicleRestrictionsAggression,
                      OnVehicleRestrictionsAggressionChanged) as UIDropDown;
            _banRegularTrafficOnBusLanesToggle
                = onRoadsGroup.AddCheckbox(
                      Translation.Options.Get("VR.Checkbox:Ban private cars and trucks on bus lanes"),
                      Options.banRegularTrafficOnBusLanes,
                      OnBanRegularTrafficOnBusLanesChanged) as UICheckBox;
            _highwayRulesToggle
                = onRoadsGroup.AddCheckbox(
                      Translation.Options.Get("VR.Checkbox:Enable highway merging/splitting rules"),
                      Options.highwayRules,
                      OnHighwayRulesChanged) as UICheckBox;
            _preferOuterLaneToggle
                = onRoadsGroup.AddCheckbox(
                      Translation.Options.Get("VR.Checkbox:Heavy trucks prefer outer lanes on highways"),
                      Options.preferOuterLane,
                      OnPreferOuterLaneChanged) as UICheckBox;

            if (SteamHelper.IsDLCOwned(SteamHelper.DLC.NaturalDisastersDLC)) {
                UIHelperBase inCaseOfEmergencyGroup =
                    panelHelper.AddGroup(
                        Translation.Options.Get("VR.Group:In case of emergency/disaster"));

                _evacBussesMayIgnoreRulesToggle
                    = inCaseOfEmergencyGroup.AddCheckbox(
                          Translation.Options.Get("VR.Checkbox:Evacuation buses may ignore traffic rules"),
                          Options.evacBussesMayIgnoreRules,
                          OnEvacBussesMayIgnoreRulesChanged) as UICheckBox;
            }

            NoDoubleCrossings.AddUI(onRoadsGroup);

            OptionsMassEditTab.MakePanel_MassEdit(panelHelper);
        }

        private static void OnVehicleRestrictionsAggressionChanged(int newValue) {
            if (!Options.IsGameLoaded()) {
                return;
            }

            Log._Debug($"vehicleRestrictionsAggression changed to {newValue}");
            SetVehicleRestrictionsAggression((VehicleRestrictionsAggression)newValue);
        }

        public static void SetVehicleRestrictionsAggression(VehicleRestrictionsAggression val) {
            bool changed = Options.vehicleRestrictionsAggression != val;
            Options.vehicleRestrictionsAggression = val;

            if (changed && _vehicleRestrictionsAggressionDropdown != null) {
                _vehicleRestrictionsAggressionDropdown.selectedIndex = (int)val;
            }
        }

        private static void OnBanRegularTrafficOnBusLanesChanged(bool newValue) {
            if (!Options.IsGameLoaded()) {
                return;
            }

            Log._Debug($"banRegularTrafficOnBusLanes changed to {newValue}");
            Options.banRegularTrafficOnBusLanes = newValue;
            VehicleRestrictionsManager.Instance.ClearCache();
            ModUI.GetTrafficManagerTool()?.InitializeSubTools();
            if (Options.DedicatedTurningLanes) {
                LaneArrowManager.Instance.UpdateDedicatedTurningLanePolicy(false);
            }
        }

        private static void OnHighwayRulesChanged(bool newHighwayRules) {
            if (!Options.IsGameLoaded()) {
                return;
            }

            bool changed = newHighwayRules != Options.highwayRules;
            if (!changed) {
                return;
            }

            Log._Debug($"Highway rules changed to {newHighwayRules}");
            Options.highwayRules = newHighwayRules;
            Flags.ClearHighwayLaneArrows();
            Flags.ApplyAllFlags();
            RoutingManager.Instance.RequestFullRecalculation();
        }

        private static void OnPreferOuterLaneChanged(bool val) {
            if (!Options.IsGameLoaded()) {
                return;
            }

            Options.preferOuterLane = val;
        }

        private static void OnEvacBussesMayIgnoreRulesChanged(bool value) {
            if (!Options.IsGameLoaded()) {
                return;
            }

            Log._Debug($"evacBussesMayIgnoreRules changed to {value}");
            Options.evacBussesMayIgnoreRules = value;
        }

        public static void SetHighwayRules(bool newHighwayRules) {
            Options.highwayRules = newHighwayRules;

            if (_highwayRulesToggle != null) {
                _highwayRulesToggle.isChecked = Options.highwayRules;
            }
        }

        public static void SetPreferOuterLane(bool val) {
            Options.preferOuterLane = val;

            if (_preferOuterLaneToggle != null) {
                _preferOuterLaneToggle.isChecked = Options.preferOuterLane;
            }
        }

        public static void SetEvacBussesMayIgnoreRules(bool value) {
            if (!SteamHelper.IsDLCOwned(SteamHelper.DLC.NaturalDisastersDLC)) {
                value = false;
            }

            Options.evacBussesMayIgnoreRules = value;

            if (_evacBussesMayIgnoreRulesToggle != null) {
                _evacBussesMayIgnoreRulesToggle.isChecked = value;
            }
        }

        public static void SetBanRegularTrafficOnBusLanes(bool value) {
            Options.banRegularTrafficOnBusLanes = value;

            if (_banRegularTrafficOnBusLanesToggle != null) {
                _banRegularTrafficOnBusLanesToggle.isChecked = value;
            }

            VehicleRestrictionsManager.Instance.ClearCache();
            ModUI.GetTrafficManagerTool()?.InitializeSubTools();
        }

    }
}
