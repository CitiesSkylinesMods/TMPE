namespace TrafficManager.State {
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using ICities;
    using JetBrains.Annotations;
    using TrafficManager.Manager.Impl;
    using TrafficManager.UI.Helpers;
    using TrafficManager.UI;
    using TrafficManager.Lifecycle;
    public static class OptionsMaintenanceTab {
        [UsedImplicitly]
        private static UIButton _resetStuckEntitiesBtn;

        [UsedImplicitly]
        private static UIButton _removeParkedVehiclesBtn;

        [UsedImplicitly]
        private static UIButton _removeAllExistingTrafficLightsBtn;
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

        private static string T(string text) {
            return Translation.Options.Get(text);
        }

        internal static void MakeSettings_Maintenance(ExtUITabstrip tabStrip) {
            UIHelper panelHelper = tabStrip.AddTabPage(Translation.Options.Get("Tab:Maintenance"));
            UIHelperBase maintenanceGroup = panelHelper.AddGroup(T("Tab:Maintenance"));

            _resetStuckEntitiesBtn = maintenanceGroup.AddButton(
                                         T("Maintenance.Button:Reset stuck cims and vehicles"),
                                         onClickResetStuckEntities) as UIButton;

            _removeParkedVehiclesBtn = maintenanceGroup.AddButton(
                                           T("Maintenance.Button:Remove parked vehicles"),
                                           OnClickRemoveParkedVehicles) as UIButton;

            _removeAllExistingTrafficLightsBtn = maintenanceGroup.AddButton(
                                           T("Maintenance.Button:Remove all existing traffic lights"),
                                           OnClickRemoveAllExistingTrafficLights) as UIButton;
#if DEBUG
            _resetSpeedLimitsBtn = maintenanceGroup.AddButton(
                                       T("Maintenance.Button:Reset custom speed limits"),
                                       OnClickResetSpeedLimits) as UIButton;
#endif
            _reloadGlobalConfBtn = maintenanceGroup.AddButton(
                                       T("Maintenance.Button:Reload global configuration"),
                                       OnClickReloadGlobalConf) as UIButton;
            _resetGlobalConfBtn = maintenanceGroup.AddButton(
                                      T("Maintenance.Button:Reset global configuration"),
                                      OnClickResetGlobalConf) as UIButton;

#if QUEUEDSTATS
            _showPathFindStatsToggle = maintenanceGroup.AddCheckbox(
                                           T("Maintenance.Checkbox:Show path-find stats"),
                                           Options.showPathFindStats,
                                           OnShowPathFindStatsChanged) as UICheckBox;
#endif

            var featureGroup =
                panelHelper.AddGroup(T("Maintenance.Group:Activated features")) as UIHelper;
            EnablePrioritySignsToggle = featureGroup.AddCheckbox(
                                            T("Checkbox:Priority signs"),
                                            Options.prioritySignsEnabled,
                                            OnPrioritySignsEnabledChanged) as UICheckBox;
            EnableTimedLightsToggle = featureGroup.AddCheckbox(
                                          T("Checkbox:Timed traffic lights"),
                                          Options.timedLightsEnabled,
                                          OnTimedLightsEnabledChanged) as UICheckBox;
            _enableCustomSpeedLimitsToggle = featureGroup.AddCheckbox(
                                                 T("Checkbox:Speed limits"),
                                                 Options.customSpeedLimitsEnabled,
                                                 OnCustomSpeedLimitsEnabledChanged) as UICheckBox;
            _enableVehicleRestrictionsToggle = featureGroup.AddCheckbox(
                                                       T("Checkbox:Vehicle restrictions"),
                                                       Options.vehicleRestrictionsEnabled,
                                                       OnVehicleRestrictionsEnabledChanged) as
                                                   UICheckBox;
            _enableParkingRestrictionsToggle = featureGroup.AddCheckbox(
                                                       T("Checkbox:Parking restrictions"),
                                                       Options.parkingRestrictionsEnabled,
                                                       OnParkingRestrictionsEnabledChanged) as
                                                   UICheckBox;
            _enableJunctionRestrictionsToggle = featureGroup.AddCheckbox(
                                                        T("Checkbox:Junction restrictions"),
                                                        Options.junctionRestrictionsEnabled,
                                                        OnJunctionRestrictionsEnabledChanged) as
                                                    UICheckBox;
            _turnOnRedEnabledToggle = featureGroup.AddCheckbox(
                                          T("Maintenance.Checkbox:Turn on red"),
                                          Options.turnOnRedEnabled,
                                          OnTurnOnRedEnabledChanged) as UICheckBox;
            _enableLaneConnectorToggle = featureGroup.AddCheckbox(
                                             T("Maintenance.Checkbox:Lane connector"),
                                             Options.laneConnectorEnabled,
                                             OnLaneConnectorEnabledChanged) as UICheckBox;

            Options.Indent(_turnOnRedEnabledToggle);

            // TODO [issue ##959] remove when TTL is implemented in asset editor.
            bool inEditor = TMPELifecycle.InGameOrEditor()
                            && TMPELifecycle.AppMode != AppMode.Game;
            if (inEditor) {
                EnableTimedLightsToggle.isChecked = false;
                EnableTimedLightsToggle.isEnabled = false;
                // since this is temprory I don't want to go through the trouble of creating translation key.
                EnableTimedLightsToggle.tooltip = "TTL is not yet supported in asset editor";
            }
        }

        private static void onClickResetStuckEntities() {
            if (!Options.IsGameLoaded()) {
                return;
            }

            Constants.ServiceFactory.SimulationService.AddAction(
                () => { UtilityManager.Instance.ResetStuckEntities(); });
        }

        private static void OnClickRemoveParkedVehicles() {
            if (!Options.IsGameLoaded()) {
                return;
            }

            Constants.ServiceFactory.SimulationService.AddAction(() => {
                UtilityManager.Instance.RemoveParkedVehicles();
            });
        }

        private static void OnClickRemoveAllExistingTrafficLights() {
            if (!Options.IsGameLoaded()) {
                return;
            }

            ConfirmPanel.ShowModal(T("Maintenance.Dialog.Title:Remove all traffic lights"),
                                   T("Maintenance.Dialog.Text:Remove all traffic lights, Confirmation"),
                                   (_, result) => {
                if(result != 1)
                {
                    return;
                }

                Log._Debug("Removing all existing Traffic Lights");
                Constants.ServiceFactory.SimulationService.AddAction(() => TrafficLightManager.Instance.RemoveAllExistingTrafficLights());
            });
        }

        private static void OnClickResetSpeedLimits() {
            if (!Options.IsGameLoaded()) {
                return;
            }

            Flags.ResetSpeedLimits();
        }

        private static void OnClickReloadGlobalConf() {
            GlobalConfig.Reload();
        }

        private static void OnClickResetGlobalConf() {
            GlobalConfig.Reset(null, true);
        }

#if QUEUEDSTATS
        private static void OnShowPathFindStatsChanged(bool newVal) {
            if (!Options.IsGameLoaded())
                return;

            Log._Debug($"Show path-find stats changed to {newVal}");
            Options.showPathFindStats = newVal;
            Options.RebuildMenu();
        }
#endif

        private static void OnPrioritySignsEnabledChanged(bool val) {
            if (!Options.IsGameLoaded()) {
                return;
            }

            Options.prioritySignsEnabled = val;
            Options.RebuildMenu();

            if (!val) {
                OptionsOverlaysTab.SetPrioritySignsOverlay(false);
                OptionsVehicleRestrictionsTab.SetTrafficLightPriorityRules(false);
            }
        }

        private static void OnTimedLightsEnabledChanged(bool val) {
            if (!Options.IsGameLoaded()) {
                return;
            }

            Options.timedLightsEnabled = val;
            Options.RebuildMenu();

            if (!val) {
                OptionsOverlaysTab.SetTimedLightsOverlay(false);
                OptionsVehicleRestrictionsTab.SetTrafficLightPriorityRules(false);
            }
        }

        private static void OnCustomSpeedLimitsEnabledChanged(bool val) {
            if (!Options.IsGameLoaded()) {
                return;
            }

            Options.customSpeedLimitsEnabled = val;
            Options.RebuildMenu();

            if (!val) {
                OptionsOverlaysTab.SetSpeedLimitsOverlay(false);
            }
        }

        private static void OnVehicleRestrictionsEnabledChanged(bool val) {
            if (!Options.IsGameLoaded()) {
                return;
            }

            Options.vehicleRestrictionsEnabled = val;
            Options.RebuildMenu();

            if (!val) {
                OptionsOverlaysTab.SetVehicleRestrictionsOverlay(false);
            }
        }

        private static void OnParkingRestrictionsEnabledChanged(bool val) {
            if (!Options.IsGameLoaded()) {
                return;
            }

            Options.parkingRestrictionsEnabled = val;
            Options.RebuildMenu();

            if (!val) {
                OptionsOverlaysTab.SetParkingRestrictionsOverlay(false);
            }
        }

        private static void OnJunctionRestrictionsEnabledChanged(bool val) {
            if (!Options.IsGameLoaded()) {
                return;
            }

            Options.junctionRestrictionsEnabled = val;
            Options.RebuildMenu();

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

            Options.laneConnectorEnabled = val;
            Options.RebuildMenu();

            RoutingManager.Instance.RequestFullRecalculation();

            if (!val) {
                OptionsOverlaysTab.SetConnectedLanesOverlay(false);
            }
        }

        public static void SetCustomSpeedLimitsEnabled(bool newValue) {
            Options.customSpeedLimitsEnabled = newValue;
            Options.RebuildMenu();

            if (_enableCustomSpeedLimitsToggle != null) {
                _enableCustomSpeedLimitsToggle.isChecked = newValue;
            }

            if (!newValue) {
                OptionsOverlaysTab.SetSpeedLimitsOverlay(false);
            }
        }

        public static void SetVehicleRestrictionsEnabled(bool newValue) {
            Options.vehicleRestrictionsEnabled = newValue;
            Options.RebuildMenu();

            if (_enableVehicleRestrictionsToggle != null) {
                _enableVehicleRestrictionsToggle.isChecked = newValue;
            }

            if (!newValue) {
                OptionsOverlaysTab.SetVehicleRestrictionsOverlay(false);
            }
        }

        public static void SetParkingRestrictionsEnabled(bool newValue) {
            Options.parkingRestrictionsEnabled = newValue;
            Options.RebuildMenu();

            if (_enableParkingRestrictionsToggle != null) {
                _enableParkingRestrictionsToggle.isChecked = newValue;
            }

            if (!newValue) {
                OptionsOverlaysTab.SetParkingRestrictionsOverlay(false);
            }
        }

        public static void SetJunctionRestrictionsEnabled(bool newValue) {
            Options.junctionRestrictionsEnabled = newValue;
            Options.RebuildMenu();

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
            Options.laneConnectorEnabled = newValue;
            Options.RebuildMenu();

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
