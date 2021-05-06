namespace TrafficManager.State {
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using ICities;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.Manager.Impl;
    using TrafficManager.UI.Helpers;
    using TrafficManager.UI;
    using static TrafficManager.Util.Shortcuts;

    public static class OptionsVehicleRestrictionsTab {
        private static UICheckBox _relaxedBussesToggle;
        private static UICheckBox _allRelaxedToggle;
        private static UICheckBox _allowEnterBlockedJunctionsToggle;
        private static UICheckBox _allowUTurnsToggle;
        private static UICheckBox _allowNearTurnOnRedToggle;
        private static UICheckBox _allowFarTurnOnRedToggle;
        private static UICheckBox _allowLaneChangesWhileGoingStraightToggle;
        private static UICheckBox _trafficLightPriorityRulesToggle;
        private static UICheckBox _automaticallyAddTrafficLightsIfApplicableToggle;
        private static UIDropDown _vehicleRestrictionsAggressionDropdown;
        private static UICheckBox _banRegularTrafficOnBusLanesToggle;
        private static UICheckBox _highwayRulesToggle;
        private static UICheckBox _preferOuterLaneToggle;
        private static UICheckBox _evacBussesMayIgnoreRulesToggle;

        public static CheckboxOption NoDoubleCrossings =
            new CheckboxOption("NoDoubleCrossings") {
                Label = "VR.Option:No double crossings", // at a segment to segment transition, only the smaller segment gets crossings
                Handler = JunctionRestrictionsUpdateHandler,
            };

        public static CheckboxOption DedicatedTurningLanes =
            new CheckboxOption("DedicatedTurningLanes") {
                Label = "VR.Option:Dedicated turning lanes",
                Handler = LaneArrowsUpdateHandler,
        };

        static void JunctionRestrictionsUpdateHandler(bool value ) =>
            JunctionRestrictionsManager.Instance.UpdateAllDefaults();

        static void LaneArrowsUpdateHandler(bool value) =>
            LaneArrowManager.Instance.UpdateAllDefaults(true);

        internal static void MakeSettings_VehicleRestrictions(ExtUITabstrip tabStrip) {
            UIHelper panelHelper = tabStrip.AddTabPage(Translation.Options.Get("Tab:Policies & Restrictions"));
            UIHelperBase atJunctionsGroup = panelHelper.AddGroup(
                Translation.Options.Get("VR.Group:At junctions"));
#if DEBUG
            _allRelaxedToggle
                = atJunctionsGroup.AddCheckbox(
                      Translation.Options.Get("VR.Checkbox:All vehicles may ignore lane arrows"),
                      Options.allRelaxed,
                      OnAllRelaxedChanged) as UICheckBox;
#endif
            _relaxedBussesToggle
                = atJunctionsGroup.AddCheckbox(
                      Translation.Options.Get("VR.Checkbox:Buses may ignore lane arrows"),
                      Options.relaxedBusses,
                      OnRelaxedBussesChanged) as UICheckBox;
            _allowEnterBlockedJunctionsToggle
                = atJunctionsGroup.AddCheckbox(
                      Translation.Options.Get("VR.Checkbox:Vehicles may enter blocked junctions"),
                      Options.allowEnterBlockedJunctions,
                      OnAllowEnterBlockedJunctionsChanged) as UICheckBox;
            _allowUTurnsToggle
                = atJunctionsGroup.AddCheckbox(
                      Translation.Options.Get("VR.Checkbox:Vehicles may do u-turns at junctions"),
                      Options.allowUTurns,
                      OnAllowUTurnsChanged) as UICheckBox;
            _allowNearTurnOnRedToggle
                = atJunctionsGroup.AddCheckbox(
                      Translation.Options.Get("VR.Checkbox:Vehicles may turn on red"),
                      Options.allowNearTurnOnRed,
                      OnAllowNearTurnOnRedChanged) as UICheckBox;
            _allowFarTurnOnRedToggle
                = atJunctionsGroup.AddCheckbox(
                      Translation.Options.Get("VR.Checkbox:Also apply to left/right turns between one-way streets"),
                      Options.allowFarTurnOnRed,
                      OnAllowFarTurnOnRedChanged) as UICheckBox;
            Options.Indent(_allowFarTurnOnRedToggle);
            _allowLaneChangesWhileGoingStraightToggle
                = atJunctionsGroup.AddCheckbox(
                      Translation.Options.Get("VR.Checkbox:Vehicles going straight may change lanes at junctions"),
                      Options.allowLaneChangesWhileGoingStraight,
                      OnAllowLaneChangesWhileGoingStraightChanged) as UICheckBox;
            _trafficLightPriorityRulesToggle
                = atJunctionsGroup.AddCheckbox(
                      Translation.Options.Get("VR.Checkbox:Vehicles follow priority rules at junctions with timedTL"),
                      Options.trafficLightPriorityRules,
                      OnTrafficLightPriorityRulesChanged) as UICheckBox;
            _automaticallyAddTrafficLightsIfApplicableToggle
                = atJunctionsGroup.AddCheckbox(
                      Translation.Options.Get("VR.Checkbox:Automatically add traffic lights if applicable"),
                      Options.automaticallyAddTrafficLightsIfApplicable,
                      OnAutomaticallyAddTrafficLightsIfApplicableChanged) as UICheckBox;
            DedicatedTurningLanes.AddUI(atJunctionsGroup);


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

        private static void OnAllRelaxedChanged(bool newAllRelaxed) {
            if (!Options.IsGameLoaded()) {
                return;
            }

            Log._Debug($"All relaxed changed to {newAllRelaxed}");
            Options.allRelaxed = newAllRelaxed;
        }

        private static void OnRelaxedBussesChanged(bool newRelaxedBusses) {
            if (!Options.IsGameLoaded()) {
                return;
            }

            Log._Debug($"Relaxed busses changed to {newRelaxedBusses}");
            Options.relaxedBusses = newRelaxedBusses;
        }

        private static void OnAllowEnterBlockedJunctionsChanged(bool newValue) {
            if (!Options.IsGameLoaded()) {
                return;
            }

            if (newValue && !Options.junctionRestrictionsEnabled) {
                SetAllowEnterBlockedJunctions(false);
                return;
            }

            Log._Debug($"allowEnterBlockedJunctions changed to {newValue}");
            SetAllowEnterBlockedJunctions(newValue);
        }

        private static void OnAllowUTurnsChanged(bool newValue) {
            if (!Options.IsGameLoaded()) {
                return;
            }

            if (newValue && !Options.junctionRestrictionsEnabled) {
                SetAllowUTurns(false);
                return;
            }

            Log._Debug($"allowUTurns changed to {newValue}");
            SetAllowUTurns(newValue);
        }

        private static void OnAllowNearTurnOnRedChanged(bool newValue) {
            if (!Options.IsGameLoaded()) {
                return;
            }

            if (newValue && !Options.turnOnRedEnabled) {
                SetAllowNearTurnOnRed(false);
                SetAllowFarTurnOnRed(false);
                return;
            }

            Log._Debug($"allowNearTurnOnRed changed to {newValue}");
            SetAllowNearTurnOnRed(newValue);

            if (!newValue) {
                SetAllowFarTurnOnRed(false);
            }
        }

        private static void OnAllowFarTurnOnRedChanged(bool newValue) {
            if (!Options.IsGameLoaded()) {
                return;
            }

            if (newValue && (!Options.turnOnRedEnabled || !Options.allowNearTurnOnRed)) {
                SetAllowFarTurnOnRed(false);
                return;
            }

            Log._Debug($"allowFarTurnOnRed changed to {newValue}");
            SetAllowFarTurnOnRed(newValue);
        }

        private static void OnAllowLaneChangesWhileGoingStraightChanged(bool newValue) {
            if (!Options.IsGameLoaded()) {
                return;
            }

            if (newValue && !Options.junctionRestrictionsEnabled) {
                SetAllowLaneChangesWhileGoingStraight(false);
                return;
            }

            Log._Debug($"allowLaneChangesWhileGoingStraight changed to {newValue}");
            SetAllowLaneChangesWhileGoingStraight(newValue);
        }

        private static void OnTrafficLightPriorityRulesChanged(bool newValue) {
            if (!Options.IsGameLoaded()) {
                return;
            }

            if (newValue && !Options.prioritySignsEnabled) {
                SetTrafficLightPriorityRules(false);
                return;
            }

            Log._Debug($"trafficLightPriorityRules changed to {newValue}");
            Options.trafficLightPriorityRules = newValue;

            if (newValue) {
                SetPrioritySignsEnabled(true);
                SetTimedLightsEnabled(true);
            }
        }

        private static void OnAutomaticallyAddTrafficLightsIfApplicableChanged(bool newValue) {
            if (!Options.IsGameLoaded()) {
                return;
            }
            Log._Debug($"AutomaticallyAddTrafficLightsIfApplicableChanged changed to {newValue}");
            Options.automaticallyAddTrafficLightsIfApplicable = newValue;
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

        public static void SetTrafficLightPriorityRules(bool value) {
            Options.trafficLightPriorityRules = value;

            if (_trafficLightPriorityRulesToggle != null) {
                _trafficLightPriorityRulesToggle.isChecked = value;
            }
        }

        private static void OnBanRegularTrafficOnBusLanesChanged(bool newValue) {
            if (!Options.IsGameLoaded()) {
                return;
            }

            Log._Debug($"banRegularTrafficOnBusLanes changed to {newValue}");
            Options.banRegularTrafficOnBusLanes = newValue;
            VehicleRestrictionsManager.Instance.ClearCache();
            ModUI.GetTrafficManagerTool(false)?.InitializeSubTools();
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

        public static void SetAllowEnterBlockedJunctions(bool value) {
            Options.allowEnterBlockedJunctions = value;

            if (_allowEnterBlockedJunctionsToggle != null) {
                _allowEnterBlockedJunctionsToggle.isChecked = value;
            }

            Constants.ManagerFactory.JunctionRestrictionsManager.UpdateAllDefaults();
            ModUI.GetTrafficManagerTool(false)?.InitializeSubTools();
        }

        public static void SetPrioritySignsEnabled(bool newValue) {
            Options.RebuildMenu();
            Options.prioritySignsEnabled = newValue;

            if (OptionsMaintenanceTab.EnablePrioritySignsToggle != null) {
                OptionsMaintenanceTab.EnablePrioritySignsToggle.isChecked = newValue;
            }

            if (!newValue) {
                OptionsOverlaysTab.SetPrioritySignsOverlay(false);
            }
        }

        public static void SetTimedLightsEnabled(bool newValue) {
            Options.RebuildMenu();
            Options.timedLightsEnabled = newValue;

            if (OptionsMaintenanceTab.EnableTimedLightsToggle != null) {
                OptionsMaintenanceTab.EnableTimedLightsToggle.isChecked = newValue;
            }

            if (!newValue) {
                OptionsOverlaysTab.SetTimedLightsOverlay(false);
            }
        }

        public static void SetAllowUTurns(bool value) {
            Options.allowUTurns = value;

            if (_allowUTurnsToggle != null) {
                _allowUTurnsToggle.isChecked = value;
            }

            Constants.ManagerFactory.JunctionRestrictionsManager.UpdateAllDefaults();
            ModUI.GetTrafficManagerTool(false)?.InitializeSubTools();
        }

        public static void SetAllowNearTurnOnRed(bool newValue) {
            Options.allowNearTurnOnRed = newValue;

            if (_allowNearTurnOnRedToggle != null) {
                _allowNearTurnOnRedToggle.isChecked = newValue;
            }

            Constants.ManagerFactory.JunctionRestrictionsManager.UpdateAllDefaults();
            ModUI.GetTrafficManagerTool(false)?.InitializeSubTools();
        }

        public static void SetAllowFarTurnOnRed(bool newValue) {
            Options.allowFarTurnOnRed = newValue;

            if (_allowFarTurnOnRedToggle != null) {
                _allowFarTurnOnRedToggle.isChecked = newValue;
            }

            Constants.ManagerFactory.JunctionRestrictionsManager.UpdateAllDefaults();
            ModUI.GetTrafficManagerTool(false)?.InitializeSubTools();
        }

        public static void SetAllowLaneChangesWhileGoingStraight(bool value) {
            Options.allowLaneChangesWhileGoingStraight = value;
            if (_allowLaneChangesWhileGoingStraightToggle != null)
                _allowLaneChangesWhileGoingStraightToggle.isChecked = value;
            Constants.ManagerFactory.JunctionRestrictionsManager.UpdateAllDefaults();
            ModUI.GetTrafficManagerTool(false)?.InitializeSubTools();
        }

        public static void SetRelaxedBusses(bool newRelaxedBusses) {
            Options.relaxedBusses = newRelaxedBusses;

            if (_relaxedBussesToggle != null) {
                _relaxedBussesToggle.isChecked = newRelaxedBusses;
            }
        }

        public static void SetAllRelaxed(bool newAllRelaxed) {
            Options.allRelaxed = newAllRelaxed;

            if (_allRelaxedToggle != null) {
                _allRelaxedToggle.isChecked = newAllRelaxed;
            }
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

        public static void SetMayEnterBlockedJunctions(bool newMayEnterBlockedJunctions) {
            Options.allowEnterBlockedJunctions = newMayEnterBlockedJunctions;

            if (_allowEnterBlockedJunctionsToggle != null) {
                _allowEnterBlockedJunctionsToggle.isChecked = newMayEnterBlockedJunctions;
            }
        }

        public static void SetBanRegularTrafficOnBusLanes(bool value) {
            Options.banRegularTrafficOnBusLanes = value;

            if (_banRegularTrafficOnBusLanesToggle != null) {
                _banRegularTrafficOnBusLanesToggle.isChecked = value;
            }

            VehicleRestrictionsManager.Instance.ClearCache();
            ModUI.GetTrafficManagerTool(false)?.InitializeSubTools();
        }

        public static void SetAddTrafficLightsIfApplicable(bool value) {
            Options.automaticallyAddTrafficLightsIfApplicable = value;

            if (_automaticallyAddTrafficLightsIfApplicableToggle != null) {
                _automaticallyAddTrafficLightsIfApplicableToggle.isChecked = value;
            }
        }
    } // end class
}
