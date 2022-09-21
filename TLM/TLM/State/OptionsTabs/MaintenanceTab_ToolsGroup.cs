namespace TrafficManager.State {
    using ColossalFramework;
    using ICities;
    using TrafficManager.Lifecycle;
    using TrafficManager.Manager.Impl;
    using TrafficManager.Manager.Impl.LaneConnection;
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
        public static ActionButton RemoveLaneConnections = new() {
            Label = "Maintenance.Button:Remove all lane connections",
            Handler = OnResetLaneConnectionsClicked,
        };

        internal static void AddUI(UIHelperBase tab) {
            if (!TMPELifecycle.InGameOrEditor())
                return;

            var group = tab.AddGroup(T("Group:Tools"));

            ResetStuckEntities.AddUI(group);
            RemoveTrafficLights.AddUI(group);
            RemoveLaneConnections.AddUI(group);
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

        private static void OnResetLaneConnectionsClicked() {
            ConfirmPanel.ShowModal(
                T("Maintenance.Dialog.Title:Remove all lane connections"),
                T("Maintenance.Dialog.Text:Remove all lane connections, Confirmation"),
                (_, result) => {
                    if (result == 1) {
                        SimulationManager.instance
                                         .AddAction(() => LaneConnectionManager.Instance.RemoveAllLaneConnections());
                    }
                });
        }
    }
}