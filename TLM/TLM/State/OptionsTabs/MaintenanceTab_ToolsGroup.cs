namespace TrafficManager.State {
    using ColossalFramework;
    using ICities;
    using TrafficManager.Lifecycle;
    using TrafficManager.Manager.Impl;
    using TrafficManager.UI;
    using TrafficManager.UI.Helpers;

    public static class MaintenanceTab_ToolsGroup {

        public static ActionButton ResetStuckEntities = new() {
            Label = "Maintenance.Button:Reset stuck cims and vehicles",
            Handler = OnResetStuckEntitiesClicked,
        };
        public static ActionButton RemoveTrafficLights = new() {
            Label = "Maintenance.Button:Remove all existing traffic lights",
            Handler = OnRemoveTrafficLightsClicked,
        };
        public static ActionButton ResetSpeedLimits = new() {
            Label = "Maintenance.Button:Reset custom speed limits",
            Handler = OnResetSpeedLimitsClicked,
        };

        internal static void AddUI(UIHelperBase tab) {
            if (!TMPELifecycle.InGameOrEditor())
                return;

            var group = tab.AddGroup(T("Group:Tools"));

            ResetStuckEntities.AddUI(group);
            RemoveTrafficLights.AddUI(group);
#if DEBUG
            ResetSpeedLimits.AddUI(group);
#endif
        }

        private static string T(string key) => Translation.Options.Get(key);

        private static void OnResetStuckEntitiesClicked()
            => Singleton<SimulationManager>.instance.AddAction(
                () => { UtilityManager.Instance.ResetStuckEntities(); });

        private static void OnRemoveTrafficLightsClicked() {
            ConfirmPanel.ShowModal(
                T("Maintenance.Dialog.Title:Remove all traffic lights"),
                T("Maintenance.Dialog.Text:Remove all traffic lights, Confirmation"),
                (_, result) => {
                    if (result == 1) DoRemoveTrafficLights();
                });
        }

        private static void DoRemoveTrafficLights()
            => Singleton<SimulationManager>.instance.AddAction(
                () => TrafficLightManager.Instance.RemoveAllExistingTrafficLights());

        private static void OnResetSpeedLimitsClicked()
            => SpeedLimitManager.Instance.ResetSpeedLimits();
    }
}