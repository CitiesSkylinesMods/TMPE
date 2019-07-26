namespace TrafficManager.State {
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using ICities;
    using JetBrains.Annotations;
    using Manager.Impl;
    using UI;

    public class OptionsMaintenanceTab {
        private static UIButton resetStuckEntitiesBtn;
        private static UIButton removeParkedVehiclesBtn;
#if DEBUG
        private static UIButton resetSpeedLimitsBtn;
#endif
        private static UIButton reloadGlobalConfBtn;
        private static UIButton resetGlobalConfBtn;
#if QUEUEDSTATS
        private static UICheckBox showPathFindStatsToggle;
#endif
        private static UICheckBox enableCustomSpeedLimitsToggle;
        private static UICheckBox enableVehicleRestrictionsToggle;
        private static UICheckBox enableParkingRestrictionsToggle;
        private static UICheckBox enableJunctionRestrictionsToggle;
        private static UICheckBox turnOnRedEnabledToggle;
        private static UICheckBox enableLaneConnectorToggle;

        internal static UICheckBox enablePrioritySignsToggle;
        internal static UICheckBox enableTimedLightsToggle;

        private static string T(string s) {
            return Translation.GetString(s);
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

            resetStuckEntitiesBtn = maintenanceGroup.AddButton(
                                        T("Reset_stuck_cims_and_vehicles"),
                                        onClickResetStuckEntities) as UIButton;

            removeParkedVehiclesBtn = maintenanceGroup.AddButton(
                                          T("Remove_parked_vehicles"),
                                          onClickRemoveParkedVehicles) as UIButton;
#if DEBUG
            resetSpeedLimitsBtn = maintenanceGroup.AddButton(
                                      T("Reset_custom_speed_limits"),
                                      onClickResetSpeedLimits) as UIButton;
#endif
            reloadGlobalConfBtn = maintenanceGroup.AddButton(
                                      T("Reload_global_configuration"),
                                      onClickReloadGlobalConf) as UIButton;
            resetGlobalConfBtn = maintenanceGroup.AddButton(
                                     T("Reset_global_configuration"),
                                     onClickResetGlobalConf) as UIButton;

#if QUEUEDSTATS
            showPathFindStatsToggle = maintenanceGroup.AddCheckbox(
                                          T("Show_path-find_stats"),
                                          Options.showPathFindStats,
                                          onShowPathFindStatsChanged) as UICheckBox;
#endif

            var featureGroup = panelHelper.AddGroup(T("Activated_features")) as UIHelper;
            enablePrioritySignsToggle = featureGroup.AddCheckbox(
                                            T("Priority_signs"),
                                            Options.prioritySignsEnabled,
                                            OnPrioritySignsEnabledChanged) as UICheckBox;
            enableTimedLightsToggle = featureGroup.AddCheckbox(
                                          T("Timed_traffic_lights"),
                                          Options.timedLightsEnabled,
                                          OnTimedLightsEnabledChanged) as UICheckBox;
            enableCustomSpeedLimitsToggle = featureGroup.AddCheckbox(
                                                T("Speed_limits"),
                                                Options.customSpeedLimitsEnabled,
                                                OnCustomSpeedLimitsEnabledChanged) as UICheckBox;
            enableVehicleRestrictionsToggle
                = featureGroup.AddCheckbox(
                      T("Vehicle_restrictions"),
                      Options.vehicleRestrictionsEnabled,
                      OnVehicleRestrictionsEnabledChanged) as UICheckBox;
            enableParkingRestrictionsToggle
                = featureGroup.AddCheckbox(
                      T("Parking_restrictions"),
                      Options.parkingRestrictionsEnabled,
                      OnParkingRestrictionsEnabledChanged) as UICheckBox;
            enableJunctionRestrictionsToggle
                = featureGroup.AddCheckbox(
                      T("Junction_restrictions"),
                      Options.junctionRestrictionsEnabled,
                      OnJunctionRestrictionsEnabledChanged) as UICheckBox;
            turnOnRedEnabledToggle = featureGroup.AddCheckbox(
                                         T("Turn_on_red"),
                                         Options.turnOnRedEnabled,
                                         OnTurnOnRedEnabledChanged) as UICheckBox;
            enableLaneConnectorToggle = featureGroup.AddCheckbox(
                                            T("Lane_connector"),
                                            Options.laneConnectorEnabled,
                                            OnLaneConnectorEnabledChanged) as UICheckBox;

            Options.Indent(turnOnRedEnabledToggle);
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

            Options.MenuRebuildRequired = true;
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

            Options.MenuRebuildRequired = true;
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

            Options.MenuRebuildRequired = true;
            Options.customSpeedLimitsEnabled = val;

            if (!val) {
                OptionsOverlaysTab.SetSpeedLimitsOverlay(false);
            }
        }

        private static void OnVehicleRestrictionsEnabledChanged(bool val) {
            if (!Options.IsGameLoaded()) {
                return;
            }

            Options.MenuRebuildRequired = true;
            Options.vehicleRestrictionsEnabled = val;
            if (!val) {
                OptionsOverlaysTab.SetVehicleRestrictionsOverlay(false);
            }
        }

        private static void OnParkingRestrictionsEnabledChanged(bool val) {
            if (!Options.IsGameLoaded()) {
                return;
            }

            Options.MenuRebuildRequired = true;
            Options.parkingRestrictionsEnabled = val;

            if (!val) {
                OptionsOverlaysTab.SetParkingRestrictionsOverlay(false);
            }
        }

        private static void OnJunctionRestrictionsEnabledChanged(bool val) {
            if (!Options.IsGameLoaded()) {
                return;
            }

            Options.MenuRebuildRequired = true;
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

            Options.MenuRebuildRequired = true;
            Options.laneConnectorEnabled = val;
            RoutingManager.Instance.RequestFullRecalculation();

            if (!val) {
                OptionsOverlaysTab.SetConnectedLanesOverlay(false);
            }
        }

        public static void SetCustomSpeedLimitsEnabled(bool newValue) {
            Options.MenuRebuildRequired = true;
            Options.customSpeedLimitsEnabled = newValue;

            if (enableCustomSpeedLimitsToggle != null) {
                enableCustomSpeedLimitsToggle.isChecked = newValue;
            }

            if (!newValue) {
                OptionsOverlaysTab.SetSpeedLimitsOverlay(false);
            }
        }

        public static void SetVehicleRestrictionsEnabled(bool newValue) {
            Options.MenuRebuildRequired = true;
            Options.vehicleRestrictionsEnabled = newValue;

            if (enableVehicleRestrictionsToggle != null) {
                enableVehicleRestrictionsToggle.isChecked = newValue;
            }

            if (!newValue) {
                OptionsOverlaysTab.SetVehicleRestrictionsOverlay(false);
            }
        }

        public static void SetParkingRestrictionsEnabled(bool newValue) {
            Options.MenuRebuildRequired = true;
            Options.parkingRestrictionsEnabled = newValue;

            if (enableParkingRestrictionsToggle != null) {
                enableParkingRestrictionsToggle.isChecked = newValue;
            }

            if (!newValue) {
                OptionsOverlaysTab.SetParkingRestrictionsOverlay(false);
            }
        }

        public static void SetJunctionRestrictionsEnabled(bool newValue) {
            Options.MenuRebuildRequired = true;
            Options.junctionRestrictionsEnabled = newValue;

            if (enableJunctionRestrictionsToggle != null) {
                enableJunctionRestrictionsToggle.isChecked = newValue;
            }

            if (!newValue) {
                OptionsOverlaysTab.SetJunctionRestrictionsOverlay(false);
            }
        }

        public static void SetTurnOnRedEnabled(bool newValue) {
            Options.turnOnRedEnabled = newValue;

            if (turnOnRedEnabledToggle != null) {
                turnOnRedEnabledToggle.isChecked = newValue;
            }

            if (!newValue) {
                OptionsVehicleRestrictionsTab.SetAllowNearTurnOnRed(false);
                OptionsVehicleRestrictionsTab.SetAllowFarTurnOnRed(false);
            }
        }

        public static void SetLaneConnectorEnabled(bool newValue) {
            Options.MenuRebuildRequired = true;
            Options.laneConnectorEnabled = newValue;

            if (enableLaneConnectorToggle != null) {
                enableLaneConnectorToggle.isChecked = newValue;
            }

            if (!newValue) {
                OptionsOverlaysTab.SetConnectedLanesOverlay(false);
            }
        }

#if QUEUEDSTATS
        public static void SetShowPathFindStats(bool value) {
            Options.showPathFindStats = value;
            if (showPathFindStatsToggle != null) {
                showPathFindStatsToggle.isChecked = value;
            }
        }
#endif
    }
}