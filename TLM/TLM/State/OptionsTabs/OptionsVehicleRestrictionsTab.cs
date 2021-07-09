namespace TrafficManager.State {
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using ICities;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.Manager.Impl;
    using TrafficManager.UI;
    using TrafficManager.UI.Helpers;

    public static class OptionsVehicleRestrictionsTab {
        private static UICheckBox relaxedBussesToggle_;
        private static UICheckBox allRelaxedToggle_;
        private static UICheckBox allowEnterBlockedJunctionsToggle_;
        private static UICheckBox allowUTurnsToggle_;
        private static UICheckBox allowNearTurnOnRedToggle_;
        private static UICheckBox allowFarTurnOnRedToggle_;
        private static UICheckBox allowLaneChangesWhileGoingStraightToggle_;
        private static UICheckBox trafficLightPriorityRulesToggle_;
        private static UICheckBox automaticallyAddTrafficLightsIfApplicableToggle_;
        private static UIDropDown vehicleRestrictionsAggressionDropdown_;
        private static UICheckBox banRegularTrafficOnBusLanesToggle_;
        private static UICheckBox highwayRulesToggle_;
        private static UICheckBox preferOuterLaneToggle_;
        private static UICheckBox evacBussesMayIgnoreRulesToggle_;

        public static readonly CheckboxOption noDoubleCrossings =
            new("NoDoubleCrossings") {
                Label =
                    "VR.Option:No double crossings", // at a segment to segment transition, only the smaller segment gets crossings
                Handler = JunctionRestrictionsUpdateHandler,
            };

        public static readonly CheckboxOption dedicatedTurningLanes =
            new("DedicatedTurningLanes") {
                Label = "VR.Option:Dedicated turning lanes",
                Handler = LaneArrowsUpdateHandler,
            };

        private static void JunctionRestrictionsUpdateHandler(bool value) =>
            JunctionRestrictionsManager.Instance.UpdateAllDefaults();

        private static void LaneArrowsUpdateHandler(bool value) =>
            LaneArrowManager.Instance.UpdateAllDefaults(true);

        internal static void MakeSettings_VehicleRestrictions(ExtUITabstrip tabStrip) {
            UIHelper panelHelper =
                tabStrip.AddTabPage(Translation.Options.Get("Tab:Policies & Restrictions"));
            UIHelperBase atJunctionsGroup = panelHelper.AddGroup(
                Translation.Options.Get("VR.Group:At junctions"));
#if DEBUG
            allRelaxedToggle_
                = atJunctionsGroup.AddCheckbox(
                      Translation.Options.Get("VR.Checkbox:All vehicles may ignore lane arrows"),
                      Options.allRelaxed,
                      OnAllRelaxedChanged) as UICheckBox;
#endif
            relaxedBussesToggle_
                = atJunctionsGroup.AddCheckbox(
                      Translation.Options.Get("VR.Checkbox:Buses may ignore lane arrows"),
                      Options.relaxedBusses,
                      OnRelaxedBussesChanged) as UICheckBox;
            allowEnterBlockedJunctionsToggle_
                = atJunctionsGroup.AddCheckbox(
                      Translation.Options.Get("VR.Checkbox:Vehicles may enter blocked junctions"),
                      Options.allowEnterBlockedJunctions,
                      OnAllowEnterBlockedJunctionsChanged) as UICheckBox;
            allowUTurnsToggle_
                = atJunctionsGroup.AddCheckbox(
                      Translation.Options.Get("VR.Checkbox:Vehicles may do u-turns at junctions"),
                      Options.allowUTurns,
                      OnAllowUTurnsChanged) as UICheckBox;
            allowNearTurnOnRedToggle_
                = atJunctionsGroup.AddCheckbox(
                      Translation.Options.Get("VR.Checkbox:Vehicles may turn on red"),
                      Options.allowNearTurnOnRed,
                      OnAllowNearTurnOnRedChanged) as UICheckBox;
            allowFarTurnOnRedToggle_
                = atJunctionsGroup.AddCheckbox(
                      Translation.Options.Get(
                          "VR.Checkbox:Also apply to left/right turns between one-way streets"),
                      Options.allowFarTurnOnRed,
                      OnAllowFarTurnOnRedChanged) as UICheckBox;
            Options.Indent(allowFarTurnOnRedToggle_);
            allowLaneChangesWhileGoingStraightToggle_
                = atJunctionsGroup.AddCheckbox(
                      Translation.Options.Get(
                          "VR.Checkbox:Vehicles going straight may change lanes at junctions"),
                      Options.allowLaneChangesWhileGoingStraight,
                      OnAllowLaneChangesWhileGoingStraightChanged) as UICheckBox;
            trafficLightPriorityRulesToggle_
                = atJunctionsGroup.AddCheckbox(
                      Translation.Options.Get(
                          "VR.Checkbox:Vehicles follow priority rules at junctions with timedTL"),
                      Options.trafficLightPriorityRules,
                      OnTrafficLightPriorityRulesChanged) as UICheckBox;
            automaticallyAddTrafficLightsIfApplicableToggle_
                = atJunctionsGroup.AddCheckbox(
                      Translation.Options.Get(
                          "VR.Checkbox:Automatically add traffic lights if applicable"),
                      Options.automaticallyAddTrafficLightsIfApplicable,
                      OnAutomaticallyAddTrafficLightsIfApplicableChanged) as UICheckBox;
            dedicatedTurningLanes.AddUI(atJunctionsGroup);

            UIHelperBase onRoadsGroup =
                panelHelper.AddGroup(Translation.Options.Get("VR.Group:On roads"));

            vehicleRestrictionsAggressionDropdown_
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
            banRegularTrafficOnBusLanesToggle_
                = onRoadsGroup.AddCheckbox(
                      Translation.Options.Get(
                          "VR.Checkbox:Ban private cars and trucks on bus lanes"),
                      Options.banRegularTrafficOnBusLanes,
                      OnBanRegularTrafficOnBusLanesChanged) as UICheckBox;
            highwayRulesToggle_
                = onRoadsGroup.AddCheckbox(
                      Translation.Options.Get("VR.Checkbox:Enable highway merging/splitting rules"),
                      Options.highwayRules,
                      OnHighwayRulesChanged) as UICheckBox;
            preferOuterLaneToggle_
                = onRoadsGroup.AddCheckbox(
                      Translation.Options.Get(
                          "VR.Checkbox:Heavy trucks prefer outer lanes on highways"),
                      Options.preferOuterLane,
                      OnPreferOuterLaneChanged) as UICheckBox;

            if (SteamHelper.IsDLCOwned(SteamHelper.DLC.NaturalDisastersDLC)) {
                UIHelperBase inCaseOfEmergencyGroup =
                    panelHelper.AddGroup(
                        Translation.Options.Get("VR.Group:In case of emergency/disaster"));

                evacBussesMayIgnoreRulesToggle_
                    = inCaseOfEmergencyGroup.AddCheckbox(
                          Translation.Options.Get(
                              "VR.Checkbox:Evacuation buses may ignore traffic rules"),
                          Options.evacBussesMayIgnoreRules,
                          OnEvacBussesMayIgnoreRulesChanged) as UICheckBox;
            }

            noDoubleCrossings.AddUI(onRoadsGroup);

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

            if (changed && vehicleRestrictionsAggressionDropdown_ != null) {
                vehicleRestrictionsAggressionDropdown_.selectedIndex = (int)val;
            }
        }

        public static void SetTrafficLightPriorityRules(bool value) {
            Options.trafficLightPriorityRules = value;

            if (trafficLightPriorityRulesToggle_ != null) {
                trafficLightPriorityRulesToggle_.isChecked = value;
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

            if (allowEnterBlockedJunctionsToggle_ != null) {
                allowEnterBlockedJunctionsToggle_.isChecked = value;
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

            if (allowUTurnsToggle_ != null) {
                allowUTurnsToggle_.isChecked = value;
            }

            Constants.ManagerFactory.JunctionRestrictionsManager.UpdateAllDefaults();
            ModUI.GetTrafficManagerTool(false)?.InitializeSubTools();
        }

        public static void SetAllowNearTurnOnRed(bool newValue) {
            Options.allowNearTurnOnRed = newValue;

            if (allowNearTurnOnRedToggle_ != null) {
                allowNearTurnOnRedToggle_.isChecked = newValue;
            }

            Constants.ManagerFactory.JunctionRestrictionsManager.UpdateAllDefaults();
            ModUI.GetTrafficManagerTool(false)?.InitializeSubTools();
        }

        public static void SetAllowFarTurnOnRed(bool newValue) {
            Options.allowFarTurnOnRed = newValue;

            if (allowFarTurnOnRedToggle_ != null) {
                allowFarTurnOnRedToggle_.isChecked = newValue;
            }

            Constants.ManagerFactory.JunctionRestrictionsManager.UpdateAllDefaults();
            ModUI.GetTrafficManagerTool(false)?.InitializeSubTools();
        }

        public static void SetAllowLaneChangesWhileGoingStraight(bool value) {
            Options.allowLaneChangesWhileGoingStraight = value;

            if (allowLaneChangesWhileGoingStraightToggle_ != null) {
                allowLaneChangesWhileGoingStraightToggle_.isChecked = value;
            }

            Constants.ManagerFactory.JunctionRestrictionsManager.UpdateAllDefaults();
            ModUI.GetTrafficManagerTool(false)?.InitializeSubTools();
        }

        public static void SetRelaxedBusses(bool newRelaxedBusses) {
            Options.relaxedBusses = newRelaxedBusses;

            if (relaxedBussesToggle_ != null) {
                relaxedBussesToggle_.isChecked = newRelaxedBusses;
            }
        }

        public static void SetAllRelaxed(bool newAllRelaxed) {
            Options.allRelaxed = newAllRelaxed;

            if (allRelaxedToggle_ != null) {
                allRelaxedToggle_.isChecked = newAllRelaxed;
            }
        }

        public static void SetHighwayRules(bool newHighwayRules) {
            Options.highwayRules = newHighwayRules;

            if (highwayRulesToggle_ != null) {
                highwayRulesToggle_.isChecked = Options.highwayRules;
            }
        }

        public static void SetPreferOuterLane(bool val) {
            Options.preferOuterLane = val;

            if (preferOuterLaneToggle_ != null) {
                preferOuterLaneToggle_.isChecked = Options.preferOuterLane;
            }
        }

        public static void SetEvacBussesMayIgnoreRules(bool value) {
            if (!SteamHelper.IsDLCOwned(SteamHelper.DLC.NaturalDisastersDLC)) {
                value = false;
            }

            Options.evacBussesMayIgnoreRules = value;

            if (evacBussesMayIgnoreRulesToggle_ != null) {
                evacBussesMayIgnoreRulesToggle_.isChecked = value;
            }
        }

        public static void SetMayEnterBlockedJunctions(bool newMayEnterBlockedJunctions) {
            Options.allowEnterBlockedJunctions = newMayEnterBlockedJunctions;

            if (allowEnterBlockedJunctionsToggle_ != null) {
                allowEnterBlockedJunctionsToggle_.isChecked = newMayEnterBlockedJunctions;
            }
        }

        public static void SetBanRegularTrafficOnBusLanes(bool value) {
            Options.banRegularTrafficOnBusLanes = value;

            if (banRegularTrafficOnBusLanesToggle_ != null) {
                banRegularTrafficOnBusLanesToggle_.isChecked = value;
            }

            VehicleRestrictionsManager.Instance.ClearCache();
            ModUI.GetTrafficManagerTool(false)?.InitializeSubTools();
        }

        public static void SetAddTrafficLightsIfApplicable(bool value) {
            Options.automaticallyAddTrafficLightsIfApplicable = value;

            if (automaticallyAddTrafficLightsIfApplicableToggle_ != null) {
                automaticallyAddTrafficLightsIfApplicableToggle_.isChecked = value;
            }
        }
    } // end class
}