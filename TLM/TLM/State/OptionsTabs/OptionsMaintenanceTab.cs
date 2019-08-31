namespace TrafficManager.State {
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using ICities;
    using JetBrains.Annotations;
    using Manager.Impl;
    using UI;

    public static class OptionsMaintenanceTab {
        [UsedImplicitly]
        private static UIButton _resetStuckEntitiesBtn;

        [UsedImplicitly]
        private static UIButton _removeParkedVehiclesBtn;
#if DEBUG
        [UsedImplicitly]
        private static UIButton _resetSpeedLimitsBtn;
#endif
        [UsedImplicitly]
        private static UIButton _reloadGlobalConfBtn;

        [UsedImplicitly]
        private static UIButton _resetGlobalConfBtn;

#if QUEUEDSTATS
        private static UICheckBox _showPathFindStatsToggle;
#endif

        private static UICheckBox _enableCustomSpeedLimitsToggle;
        private static UICheckBox _enableVehicleRestrictionsToggle;
        private static UICheckBox _enableParkingRestrictionsToggle;
        private static UICheckBox _enableJunctionRestrictionsToggle;
        private static UICheckBox _turnOnRedEnabledToggle;
        private static UICheckBox _enableLaneConnectorToggle;

        internal static UICheckBox EnablePrioritySignsToggle;
        internal static UICheckBox EnableTimedLightsToggle;

        private static string T(string s) {
            return Translation.Get(s);
        }

        internal static void MakeSettings_Maintenance(UITabstrip tabStrip, int tabIndex) {
            Options.AddOptionTab(tabStrip, T("Maintenance"));
            tabStrip.selectedIndex = tabIndex;

            var currentPanel = tabStrip.tabContainer.components[tabIndex] as UIPanel;
            currentPanel.autoLayout = true;
            currentPanel.autoLayoutDirection = LayoutDirection.Vertical;
            currentPanel.autoLayoutPadding.top = 5;
            currentPanel.autoLayoutPadding.left = 10;
            currentPanel.autoLayoutPadding.right = 10;

            var panelHelper = new UIHelper(currentPanel);
            UIHelperBase maintenanceGroup = panelHelper.AddGroup(T("Maintenance"));

            _resetStuckEntitiesBtn = maintenanceGroup.AddButton(
                                        T("Reset_stuck_cims_and_vehicles"),
                                        onClickResetStuckEntities) as UIButton;

            _removeParkedVehiclesBtn = maintenanceGroup.AddButton(
                                          T("Remove_parked_vehicles"),
                                          onClickRemoveParkedVehicles) as UIButton;
#if DEBUG
            _resetSpeedLimitsBtn = maintenanceGroup.AddButton(
                                      T("Reset_custom_speed_limits"),
                                      onClickResetSpeedLimits) as UIButton;
#endif
            _reloadGlobalConfBtn = maintenanceGroup.AddButton(
                                      T("Reload_global_configuration"),
                                      onClickReloadGlobalConf) as UIButton;
            _resetGlobalConfBtn = maintenanceGroup.AddButton(
                                     T("Reset_global_configuration"),
                                     onClickResetGlobalConf) as UIButton;

#if QUEUEDSTATS
            _showPathFindStatsToggle = maintenanceGroup.AddCheckbox(
                                          T("Show_path-find_stats"),
                                          Options.showPathFindStats,
                                          onShowPathFindStatsChanged) as UICheckBox;
#endif

            var featureGroup = panelHelper.AddGroup(T("Activated_features")) as UIHelper;
            EnablePrioritySignsToggle = featureGroup.AddCheckbox(
                                            T("Priority_signs"),
                                            Options.prioritySignsEnabled,
                                            OnPrioritySignsEnabledChanged) as UICheckBox;
            EnableTimedLightsToggle = featureGroup.AddCheckbox(
                                          T("Timed_traffic_lights"),
                                          Options.timedLightsEnabled,
                                          OnTimedLightsEnabledChanged) as UICheckBox;
            _enableCustomSpeedLimitsToggle = featureGroup.AddCheckbox(
                                                T("Speed_limits"),
                                                Options.customSpeedLimitsEnabled,
                                                OnCustomSpeedLimitsEnabledChanged) as UICheckBox;
            _enableVehicleRestrictionsToggle
                = featureGroup.AddCheckbox(
                      T("Vehicle_restrictions"),
                      Options.vehicleRestrictionsEnabled,
                      OnVehicleRestrictionsEnabledChanged) as UICheckBox;
            _enableParkingRestrictionsToggle
                = featureGroup.AddCheckbox(
                      T("Parking_restrictions"),
                      Options.parkingRestrictionsEnabled,
                      OnParkingRestrictionsEnabledChanged) as UICheckBox;
            _enableJunctionRestrictionsToggle
                = featureGroup.AddCheckbox(
                      T("Junction_restrictions"),
                      Options.junctionRestrictionsEnabled,
                      OnJunctionRestrictionsEnabledChanged) as UICheckBox;
            _turnOnRedEnabledToggle = featureGroup.AddCheckbox(
                                         T("Turn_on_red"),
                                         Options.turnOnRedEnabled,
                                         OnTurnOnRedEnabledChanged) as UICheckBox;
            _enableLaneConnectorToggle = featureGroup.AddCheckbox(
                                            T("Lane_connector"),
                                            Options.laneConnectorEnabled,
                                            OnLaneConnectorEnabledChanged) as UICheckBox;

            Options.Indent(_turnOnRedEnabledToggle);
        }

        private static void onClickResetStuckEntities() {
            if (!Options.IsGameLoaded()) {
                return;
            }

            Constants.ServiceFactory.SimulationService.AddAction(
                () => { UtilityManager.Instance.ResetStuckEntities(); });
        }

        private static void onClickRemoveParkedVehicles() {
            if (!Options.IsGameLoaded()) {
                return;
            }

            Constants.ServiceFactory.SimulationService.AddAction(() => {
                UtilityManager.Instance.RemoveParkedVehicles();
            });
        }

        private static void onClickResetSpeedLimits() {
            if (!Options.IsGameLoaded()) {
                return;
            }

            Flags.ResetSpeedLimits();
        }

        private static void onClickReloadGlobalConf() {
            GlobalConfig.Reload();
        }

        private static void onClickResetGlobalConf() {
            GlobalConfig.Reset(null, true);
        }

#if QUEUEDSTATS
        private static void onShowPathFindStatsChanged(bool newVal) {
            if (!Options.IsGameLoaded())
                return;

            Log._Debug($"Show path-find stats changed to {newVal}");
            Options.showPathFindStats = newVal;
        }
#endif

        private static void OnPrioritySignsEnabledChanged(bool val) {
            if (!Options.IsGameLoaded()) {
                return;
            }

            Options.RebuildMenu();
            Options.prioritySignsEnabled = val;

            if (!val) {
                OptionsOverlaysTab.SetPrioritySignsOverlay(false);
                OptionsVehicleRestrictionsTab.SetTrafficLightPriorityRules(false);
            }
        }

        private static void OnTimedLightsEnabledChanged(bool val) {
            if (!Options.IsGameLoaded()) {
                return;
            }

            Options.RebuildMenu();
            Options.timedLightsEnabled = val;

            if (!val) {
                OptionsOverlaysTab.SetTimedLightsOverlay(false);
                OptionsVehicleRestrictionsTab.SetTrafficLightPriorityRules(false);
            }
        }

        private static void OnCustomSpeedLimitsEnabledChanged(bool val) {
            if (!Options.IsGameLoaded()) {
                return;
            }

            Options.RebuildMenu();
            Options.customSpeedLimitsEnabled = val;

            if (!val) {
                OptionsOverlaysTab.SetSpeedLimitsOverlay(false);
            }
        }

        private static void OnVehicleRestrictionsEnabledChanged(bool val) {
            if (!Options.IsGameLoaded()) {
                return;
            }

            Options.RebuildMenu();
            Options.vehicleRestrictionsEnabled = val;
            if (!val) {
                OptionsOverlaysTab.SetVehicleRestrictionsOverlay(false);
            }
        }

        private static void OnParkingRestrictionsEnabledChanged(bool val) {
            if (!Options.IsGameLoaded()) {
                return;
            }

            Options.RebuildMenu();
            Options.parkingRestrictionsEnabled = val;

            if (!val) {
                OptionsOverlaysTab.SetParkingRestrictionsOverlay(false);
            }
        }

        private static void OnJunctionRestrictionsEnabledChanged(bool val) {
            if (!Options.IsGameLoaded()) {
                return;
            }

            Options.RebuildMenu();
            Options.junctionRestrictionsEnabled = val;

            if (!val) {
                OptionsVehicleRestrictionsTab.SetAllowUTurns(false);
                OptionsVehicleRestrictionsTab.SetAllowEnterBlockedJunctions(false);
                OptionsVehicleRestrictionsTab.SetAllowLaneChangesWhileGoingStraight(false);
                SetTurnOnRedEnabled(false);
                OptionsOverlaysTab.SetJunctionRestrictionsOverlay(false);
            }
        }

        private static void OnTurnOnRedEnabledChanged(bool val) {
            if (!Options.IsGameLoaded()) {
                return;
            }

            SetTurnOnRedEnabled(val);
        }

        private static void OnLaneConnectorEnabledChanged(bool val) {
            if (!Options.IsGameLoaded()) {
                return;
            }

            bool changed = val != Options.laneConnectorEnabled;

            if (!changed) {
                return;
            }

            Options.RebuildMenu();
            Options.laneConnectorEnabled = val;
            RoutingManager.Instance.RequestFullRecalculation();

            if (!val) {
                OptionsOverlaysTab.SetConnectedLanesOverlay(false);
            }
        }

        public static void SetCustomSpeedLimitsEnabled(bool newValue) {
            Options.RebuildMenu();
            Options.customSpeedLimitsEnabled = newValue;

            if (_enableCustomSpeedLimitsToggle != null) {
                _enableCustomSpeedLimitsToggle.isChecked = newValue;
            }

            if (!newValue) {
                OptionsOverlaysTab.SetSpeedLimitsOverlay(false);
            }
        }

        public static void SetVehicleRestrictionsEnabled(bool newValue) {
            Options.RebuildMenu();
            Options.vehicleRestrictionsEnabled = newValue;

            if (_enableVehicleRestrictionsToggle != null) {
                _enableVehicleRestrictionsToggle.isChecked = newValue;
            }

            if (!newValue) {
                OptionsOverlaysTab.SetVehicleRestrictionsOverlay(false);
            }
        }

        public static void SetParkingRestrictionsEnabled(bool newValue) {
            Options.RebuildMenu();
            Options.parkingRestrictionsEnabled = newValue;

            if (_enableParkingRestrictionsToggle != null) {
                _enableParkingRestrictionsToggle.isChecked = newValue;
            }

            if (!newValue) {
                OptionsOverlaysTab.SetParkingRestrictionsOverlay(false);
            }
        }

        public static void SetJunctionRestrictionsEnabled(bool newValue) {
            Options.RebuildMenu();
            Options.junctionRestrictionsEnabled = newValue;

            if (_enableJunctionRestrictionsToggle != null) {
                _enableJunctionRestrictionsToggle.isChecked = newValue;
            }

            if (!newValue) {
                OptionsOverlaysTab.SetJunctionRestrictionsOverlay(false);
            }
        }

        public static void SetTurnOnRedEnabled(bool newValue) {
            Options.turnOnRedEnabled = newValue;

            if (_turnOnRedEnabledToggle != null) {
                _turnOnRedEnabledToggle.isChecked = newValue;
            }

            if (!newValue) {
                OptionsVehicleRestrictionsTab.SetAllowNearTurnOnRed(false);
                OptionsVehicleRestrictionsTab.SetAllowFarTurnOnRed(false);
            }
        }

        public static void SetLaneConnectorEnabled(bool newValue) {
            Options.RebuildMenu();
            Options.laneConnectorEnabled = newValue;

            if (_enableLaneConnectorToggle != null) {
                _enableLaneConnectorToggle.isChecked = newValue;
            }

            if (!newValue) {
                OptionsOverlaysTab.SetConnectedLanesOverlay(false);
            }
        }

#if QUEUEDSTATS
        public static void SetShowPathFindStats(bool value) {
            Options.showPathFindStats = value;
            if (_showPathFindStatsToggle != null) {
                _showPathFindStatsToggle.isChecked = value;
            }
        }
#endif
    }
}