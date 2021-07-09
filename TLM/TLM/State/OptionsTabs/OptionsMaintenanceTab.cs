namespace TrafficManager.State {
    using System.Diagnostics;
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using ICities;
    using JetBrains.Annotations;
    using TrafficManager.Lifecycle;
    using TrafficManager.Manager.Impl;
    using TrafficManager.UI;
    using TrafficManager.UI.Helpers;

    public static class OptionsMaintenanceTab {
        [UsedImplicitly]
        private static UIButton resetStuckEntitiesBtn_;

        [UsedImplicitly]
        private static UIButton removeParkedVehiclesBtn_;

        [UsedImplicitly]
        private static UIButton removeAllExistingTrafficLightsBtn_;
#if DEBUG
        [UsedImplicitly]
        private static UIButton resetSpeedLimitsBtn_;
#endif
        [UsedImplicitly]
        private static UIButton reloadGlobalConfBtn_;

        [UsedImplicitly]
        private static UIButton resetGlobalConfBtn_;

#if QUEUEDSTATS
        private static UICheckBox showPathFindStatsToggle_;
#endif

        private static UICheckBox enableCustomSpeedLimitsToggle_;
        private static UICheckBox enableVehicleRestrictionsToggle_;
        private static UICheckBox enableParkingRestrictionsToggle_;
        private static UICheckBox enableJunctionRestrictionsToggle_;
        private static UICheckBox turnOnRedEnabledToggle_;
        private static UICheckBox enableLaneConnectorToggle_;

        internal static UICheckBox EnablePrioritySignsToggle;
        internal static UICheckBox EnableTimedLightsToggle;

        private static string T(string text) {
            return Translation.Options.Get(text);
        }

        internal static void MakeSettings_Maintenance(ExtUITabstrip tabStrip) {
            UIHelper panelHelper = tabStrip.AddTabPage(Translation.Options.Get("Tab:Maintenance"));
            UIHelperBase maintenanceGroup = panelHelper.AddGroup(T("Tab:Maintenance"));

            resetStuckEntitiesBtn_ = maintenanceGroup.AddButton(
                                         T("Maintenance.Button:Reset stuck cims and vehicles"),
                                         OnClickResetStuckEntities) as UIButton;

            removeParkedVehiclesBtn_ = maintenanceGroup.AddButton(
                                           T("Maintenance.Button:Remove parked vehicles"),
                                           OnClickRemoveParkedVehicles) as UIButton;

            removeAllExistingTrafficLightsBtn_ = maintenanceGroup.AddButton(
                                                         T(
                                                             "Maintenance.Button:Remove all existing traffic lights"),
                                                         OnClickRemoveAllExistingTrafficLights) as
                                                     UIButton;
#if DEBUG
            resetSpeedLimitsBtn_ = maintenanceGroup.AddButton(
                                       T("Maintenance.Button:Reset custom speed limits"),
                                       OnClickResetSpeedLimits) as UIButton;
#endif
            reloadGlobalConfBtn_ = maintenanceGroup.AddButton(
                                       T("Maintenance.Button:Reload global configuration"),
                                       OnClickReloadGlobalConf) as UIButton;
            resetGlobalConfBtn_ = maintenanceGroup.AddButton(
                                      T("Maintenance.Button:Reset global configuration"),
                                      OnClickResetGlobalConf) as UIButton;

#if QUEUEDSTATS
            showPathFindStatsToggle_ = maintenanceGroup.AddCheckbox(
                                           T("Maintenance.Checkbox:Show path-find stats"),
                                           Options.showPathFindStats,
                                           OnShowPathFindStatsChanged) as UICheckBox;
#endif

            var featureGroup =
                panelHelper.AddGroup(T("Maintenance.Group:Activated features")) as UIHelper;

            Debug.Assert(featureGroup != null, nameof(featureGroup) + " != null");
            EnablePrioritySignsToggle = featureGroup.AddCheckbox(
                                            T("Checkbox:Priority signs"),
                                            Options.prioritySignsEnabled,
                                            OnPrioritySignsEnabledChanged) as UICheckBox;
            EnableTimedLightsToggle = featureGroup.AddCheckbox(
                                          T("Checkbox:Timed traffic lights"),
                                          Options.timedLightsEnabled,
                                          OnTimedLightsEnabledChanged) as UICheckBox;
            enableCustomSpeedLimitsToggle_ = featureGroup.AddCheckbox(
                                                 T("Checkbox:Speed limits"),
                                                 Options.customSpeedLimitsEnabled,
                                                 OnCustomSpeedLimitsEnabledChanged) as UICheckBox;
            enableVehicleRestrictionsToggle_ = featureGroup.AddCheckbox(
                                                       T("Checkbox:Vehicle restrictions"),
                                                       Options.vehicleRestrictionsEnabled,
                                                       OnVehicleRestrictionsEnabledChanged) as
                                                   UICheckBox;
            enableParkingRestrictionsToggle_ = featureGroup.AddCheckbox(
                                                       T("Checkbox:Parking restrictions"),
                                                       Options.parkingRestrictionsEnabled,
                                                       OnParkingRestrictionsEnabledChanged) as
                                                   UICheckBox;
            enableJunctionRestrictionsToggle_ = featureGroup.AddCheckbox(
                                                        T("Checkbox:Junction restrictions"),
                                                        Options.junctionRestrictionsEnabled,
                                                        OnJunctionRestrictionsEnabledChanged) as
                                                    UICheckBox;
            turnOnRedEnabledToggle_ = featureGroup.AddCheckbox(
                                          T("Maintenance.Checkbox:Turn on red"),
                                          Options.turnOnRedEnabled,
                                          OnTurnOnRedEnabledChanged) as UICheckBox;
            enableLaneConnectorToggle_ = featureGroup.AddCheckbox(
                                             T("Maintenance.Checkbox:Lane connector"),
                                             Options.laneConnectorEnabled,
                                             OnLaneConnectorEnabledChanged) as UICheckBox;

            Options.Indent(turnOnRedEnabledToggle_);

            // TODO [issue ##959] remove when TTL is implemented in asset editor.
            bool inEditor = TMPELifecycle.InGameOrEditor()
                            && TMPELifecycle.AppMode != AppMode.Game;
            if (inEditor) {
                EnableTimedLightsToggle.isChecked = false;
                EnableTimedLightsToggle.isEnabled = false;

                // since this is temp I don't want to go through the trouble of creating translation key.
                EnableTimedLightsToggle.tooltip = "TTL is not yet supported in asset editor";
            }
        }

        private static void OnClickResetStuckEntities() {
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

            Constants.ServiceFactory.SimulationService.AddAction(
                () => { UtilityManager.Instance.RemoveParkedVehicles(); });
        }

        private static void OnClickRemoveAllExistingTrafficLights() {
            if (!Options.IsGameLoaded()) {
                return;
            }

            ConfirmPanel.ShowModal(
                T("Maintenance.Dialog.Title:Remove all traffic lights"),
                T("Maintenance.Dialog.Text:Remove all traffic lights, Confirmation"),
                (_, result) => {
                    if (result != 1) {
                        return;
                    }

                    Log._Debug("Removing all existing Traffic Lights");
                    Constants.ServiceFactory.SimulationService.AddAction(
                        () => TrafficLightManager.Instance.RemoveAllExistingTrafficLights());
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
            if (!Options.IsGameLoaded()) {
                return;
            }

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

            if (enableCustomSpeedLimitsToggle_ != null) {
                enableCustomSpeedLimitsToggle_.isChecked = newValue;
            }

            if (!newValue) {
                OptionsOverlaysTab.SetSpeedLimitsOverlay(false);
            }
        }

        public static void SetVehicleRestrictionsEnabled(bool newValue) {
            Options.vehicleRestrictionsEnabled = newValue;
            Options.RebuildMenu();

            if (enableVehicleRestrictionsToggle_ != null) {
                enableVehicleRestrictionsToggle_.isChecked = newValue;
            }

            if (!newValue) {
                OptionsOverlaysTab.SetVehicleRestrictionsOverlay(false);
            }
        }

        public static void SetParkingRestrictionsEnabled(bool newValue) {
            Options.parkingRestrictionsEnabled = newValue;
            Options.RebuildMenu();

            if (enableParkingRestrictionsToggle_ != null) {
                enableParkingRestrictionsToggle_.isChecked = newValue;
            }

            if (!newValue) {
                OptionsOverlaysTab.SetParkingRestrictionsOverlay(false);
            }
        }

        public static void SetJunctionRestrictionsEnabled(bool newValue) {
            Options.junctionRestrictionsEnabled = newValue;
            Options.RebuildMenu();

            if (enableJunctionRestrictionsToggle_ != null) {
                enableJunctionRestrictionsToggle_.isChecked = newValue;
            }

            if (!newValue) {
                OptionsOverlaysTab.SetJunctionRestrictionsOverlay(false);
            }
        }

        public static void SetTurnOnRedEnabled(bool newValue) {
            Options.turnOnRedEnabled = newValue;

            if (turnOnRedEnabledToggle_ != null) {
                turnOnRedEnabledToggle_.isChecked = newValue;
            }

            if (!newValue) {
                OptionsVehicleRestrictionsTab.SetAllowNearTurnOnRed(false);
                OptionsVehicleRestrictionsTab.SetAllowFarTurnOnRed(false);
            }
        }

        public static void SetLaneConnectorEnabled(bool newValue) {
            Options.laneConnectorEnabled = newValue;
            Options.RebuildMenu();

            if (enableLaneConnectorToggle_ != null) {
                enableLaneConnectorToggle_.isChecked = newValue;
            }

            if (!newValue) {
                OptionsOverlaysTab.SetConnectedLanesOverlay(false);
            }
        }

#if QUEUEDSTATS
        public static void SetShowPathFindStats(bool value) {
            Options.showPathFindStats = value;
            if (showPathFindStatsToggle_ != null) {
                showPathFindStatsToggle_.isChecked = value;
            }
        }
#endif
    }
}