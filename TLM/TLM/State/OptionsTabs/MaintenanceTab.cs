namespace TrafficManager.State {
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using ICities;
    using JetBrains.Annotations;
    using TrafficManager.Manager.Impl;
    using TrafficManager.UI.Helpers;
    using TrafficManager.UI;
    using TrafficManager.Lifecycle;
    using ColossalFramework;
    using System.Collections.Generic;

    public static class MaintenanceTab {
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

        private static string T(string text) {
            return Translation.Options.Get(text);
        }

        internal static void MakeSettings_Maintenance(ExtUITabstrip tabStrip) {
            UIHelper panelHelper = tabStrip.AddTabPage(Translation.Options.Get("Tab:Maintenance"));
            UIHelperBase maintenanceGroup = panelHelper.AddGroup(T("Tab:Maintenance"));

            _resetStuckEntitiesBtn = maintenanceGroup.AddButton(
                                         T("Maintenance.Button:Reset stuck cims and vehicles"),
                                         onClickResetStuckEntities) as UIButton;

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

            MaintenanceTab_FeaturesGroup.AddUI(panelHelper);

            MaintenanceTab_DespawnGroup.AddUI(panelHelper);
        }

        private static void onClickResetStuckEntities() {
            if (!Options.IsGameLoaded()) {
                return;
            }

            Singleton<SimulationManager>.instance.AddAction(
                () => { UtilityManager.Instance.ResetStuckEntities(); });
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
                Singleton<SimulationManager>.instance.AddAction(() =>
                    TrafficLightManager.Instance.RemoveAllExistingTrafficLights());
            });
        }

        private static void OnClickResetSpeedLimits() {
            if (!Options.IsGameLoaded()) {
                return;
            }

            SpeedLimitManager.Instance.ResetSpeedLimits();
        }

        private static void OnClickReloadGlobalConf() {
            GlobalConfig.Reload();
        }

        private static void OnClickResetGlobalConf() {
            GlobalConfig.Reset(oldConfig: null, resetAll: true);
        }

#if QUEUEDSTATS
        private static void OnShowPathFindStatsChanged(bool newVal) {
            if (!Options.IsGameLoaded())
                return;

            Log._Debug($"Show path-find stats changed to {newVal}");
            Options.showPathFindStats = newVal;
            OptionsManager.RebuildMenu();
        }
#endif

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
