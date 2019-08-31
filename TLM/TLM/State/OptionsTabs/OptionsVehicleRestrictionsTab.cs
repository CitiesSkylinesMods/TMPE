namespace TrafficManager.State {
    using API.Traffic.Enums;
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using ICities;
    using Manager.Impl;
    using UI;

    public static class OptionsVehicleRestrictionsTab {
        private static UICheckBox _relaxedBussesToggle;
        private static UICheckBox _allRelaxedToggle;
        private static UICheckBox _allowEnterBlockedJunctionsToggle;
        private static UICheckBox _allowUTurnsToggle;
        private static UICheckBox _allowNearTurnOnRedToggle;
        private static UICheckBox _allowFarTurnOnRedToggle;
        private static UICheckBox _allowLaneChangesWhileGoingStraightToggle;
        private static UICheckBox _trafficLightPriorityRulesToggle;
        private static UIDropDown _vehicleRestrictionsAggressionDropdown;
        private static UICheckBox _banRegularTrafficOnBusLanesToggle;
        private static UICheckBox _highwayRulesToggle;
        private static UICheckBox _preferOuterLaneToggle;
        private static UICheckBox _evacBussesMayIgnoreRulesToggle;

        private static string T(string s) {
            return Translation.Get(s);
        }

        internal static void MakeSettings_VehicleRestrictions(UITabstrip tabStrip, int tabIndex) {
            Options.AddOptionTab(tabStrip, T("Policies_&_Restrictions"));
            tabStrip.selectedIndex = tabIndex;

            var currentPanel = tabStrip.tabContainer.components[tabIndex] as UIPanel;
            currentPanel.autoLayout = true;
            currentPanel.autoLayoutDirection = LayoutDirection.Vertical;
            currentPanel.autoLayoutPadding.top = 5;
            currentPanel.autoLayoutPadding.left = 10;
            currentPanel.autoLayoutPadding.right = 10;

            var panelHelper = new UIHelper(currentPanel);
            UIHelperBase atJunctionsGroup = panelHelper.AddGroup(T("At_junctions"));
#if DEBUG
            _allRelaxedToggle = atJunctionsGroup.AddCheckbox(
                                   T("All_vehicles_may_ignore_lane_arrows"),
                                   Options.allRelaxed,
                                   OnAllRelaxedChanged) as UICheckBox;
#endif
            _relaxedBussesToggle = atJunctionsGroup.AddCheckbox(
                                      T("Busses_may_ignore_lane_arrows"),
                                      Options.relaxedBusses,
                                      OnRelaxedBussesChanged) as UICheckBox;
            _allowEnterBlockedJunctionsToggle
                = atJunctionsGroup.AddCheckbox(
                      T("Vehicles_may_enter_blocked_junctions"),
                      Options.allowEnterBlockedJunctions,
                      onAllowEnterBlockedJunctionsChanged) as UICheckBox;
            _allowUTurnsToggle = atJunctionsGroup.AddCheckbox(
                                    T("Vehicles_may_do_u-turns_at_junctions"),
                                    Options.allowUTurns,
                                    onAllowUTurnsChanged) as UICheckBox;
            _allowNearTurnOnRedToggle = atJunctionsGroup.AddCheckbox(
                                           T("Vehicles_may_turn_on_red"),
                                           Options.allowNearTurnOnRed,
                                           onAllowNearTurnOnRedChanged) as UICheckBox;
            _allowFarTurnOnRedToggle
                = atJunctionsGroup.AddCheckbox(
                      T("Also_apply_to_left/right_turns_between_one-way_streets"),
                      Options.allowFarTurnOnRed,
                      onAllowFarTurnOnRedChanged) as UICheckBox;
            _allowLaneChangesWhileGoingStraightToggle
                = atJunctionsGroup.AddCheckbox(
                      T("Vehicles_going_straight_may_change_lanes_at_junctions"),
                      Options.allowLaneChangesWhileGoingStraight,
                      onAllowLaneChangesWhileGoingStraightChanged) as UICheckBox;
            _trafficLightPriorityRulesToggle
                = atJunctionsGroup.AddCheckbox(
                      T("Vehicles_follow_priority_rules_at_junctions_with_timed_traffic_lights"),
                      Options.trafficLightPriorityRules,
                      OnTrafficLightPriorityRulesChanged) as UICheckBox;

            Options.Indent(_allowFarTurnOnRedToggle);

            UIHelperBase onRoadsGroup = panelHelper.AddGroup(T("On_roads"));
            _vehicleRestrictionsAggressionDropdown
                = onRoadsGroup.AddDropdown(
                      T("Vehicle_restrictions_aggression") + ":",
                      new[] { T("Low"), T("Medium"), T("High"), T("Strict") },
                      (int)Options.vehicleRestrictionsAggression,
                      OnVehicleRestrictionsAggressionChanged) as UIDropDown;
            _banRegularTrafficOnBusLanesToggle
                = onRoadsGroup.AddCheckbox(
                      T("Ban_private_cars_and_trucks_on_bus_lanes"),
                      Options.banRegularTrafficOnBusLanes,
                      OnBanRegularTrafficOnBusLanesChanged) as UICheckBox;
            _highwayRulesToggle = onRoadsGroup.AddCheckbox(
                                     T("Enable_highway_specific_lane_merging/splitting_rules"),
                                     Options.highwayRules,
                                     OnHighwayRulesChanged) as UICheckBox;
            _preferOuterLaneToggle = onRoadsGroup.AddCheckbox(
                                        T("Heavy_trucks_prefer_outer_lanes_on_highways"),
                                        Options.preferOuterLane,
                                        OnPreferOuterLaneChanged) as UICheckBox;

            if (SteamHelper.IsDLCOwned(SteamHelper.DLC.NaturalDisastersDLC)) {
                UIHelperBase inCaseOfEmergencyGroup =
                    panelHelper.AddGroup(T("In_case_of_emergency"));
                _evacBussesMayIgnoreRulesToggle
                    = inCaseOfEmergencyGroup.AddCheckbox(
                          T("Evacuation_busses_may_ignore_traffic_rules"),
                          Options.evacBussesMayIgnoreRules,
                          OnEvacBussesMayIgnoreRulesChanged) as UICheckBox;
            }
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

        private static void onAllowEnterBlockedJunctionsChanged(bool newValue) {
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

        private static void onAllowUTurnsChanged(bool newValue) {
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

        private static void onAllowNearTurnOnRedChanged(bool newValue) {
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

        private static void onAllowFarTurnOnRedChanged(bool newValue) {
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

        private static void onAllowLaneChangesWhileGoingStraightChanged(bool newValue) {
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
            UIBase.GetTrafficManagerTool(false)?.InitializeSubTools();
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
            UIBase.GetTrafficManagerTool(false)?.InitializeSubTools();
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
            UIBase.GetTrafficManagerTool(false)?.InitializeSubTools();
        }

        public static void SetAllowNearTurnOnRed(bool newValue) {
            Options.allowNearTurnOnRed = newValue;

            if (_allowNearTurnOnRedToggle != null) {
                _allowNearTurnOnRedToggle.isChecked = newValue;
            }

            Constants.ManagerFactory.JunctionRestrictionsManager.UpdateAllDefaults();
            UIBase.GetTrafficManagerTool(false)?.InitializeSubTools();
        }

        public static void SetAllowFarTurnOnRed(bool newValue) {
            Options.allowFarTurnOnRed = newValue;

            if (_allowFarTurnOnRedToggle != null) {
                _allowFarTurnOnRedToggle.isChecked = newValue;
            }

            Constants.ManagerFactory.JunctionRestrictionsManager.UpdateAllDefaults();
            UIBase.GetTrafficManagerTool(false)?.InitializeSubTools();
        }

        public static void SetAllowLaneChangesWhileGoingStraight(bool value) {
            Options.allowLaneChangesWhileGoingStraight = value;
            if (_allowLaneChangesWhileGoingStraightToggle != null)
                _allowLaneChangesWhileGoingStraightToggle.isChecked = value;
            Constants.ManagerFactory.JunctionRestrictionsManager.UpdateAllDefaults();
            UIBase.GetTrafficManagerTool(false)?.InitializeSubTools();
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
            UIBase.GetTrafficManagerTool(false)?.InitializeSubTools();
        }

    } // end class
}