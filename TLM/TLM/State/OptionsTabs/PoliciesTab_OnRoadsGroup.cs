namespace TrafficManager.State {
    using System;
    using ColossalFramework.UI;
    using ICities;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.Manager.Impl;
    using TrafficManager.UI;
    using TrafficManager.UI.Helpers;
    using TrafficManager.Util;

    public static class PoliciesTab_OnRoadsGroup {

        public static CheckboxOption BanRegularTrafficOnBusLanes =
            new (nameof(SavedGameOptions.banRegularTrafficOnBusLanes), Scope.Savegame) {
                Label = "VR.Checkbox:Ban private cars and trucks on bus lanes",
                Handler = OnBanRegularTrafficOnBusLanesChanged,
            };

        // at a segment to segment transition, only the smaller segment gets crossings
        public static CheckboxOption NoDoubleCrossings =
            new (nameof(SavedGameOptions.NoDoubleCrossings), Scope.Savegame) {
                Label = "VR.Option:No double crossings",
                Handler = JunctionRestrictionsUpdateHandler,
            };

        public static DropDownOption<VehicleRestrictionsAggression> VehicleRestrictionsAggression =
            new(nameof(SavedGameOptions.vehicleRestrictionsAggression), Scope.Savegame) {
                Label = "VR.Dropdown:Vehicle restrictions aggression",
            };

        static PoliciesTab_OnRoadsGroup() {
            try {
                BanRegularTrafficOnBusLanes
                    .PropagateTrueTo(MaintenanceTab_FeaturesGroup.VehicleRestrictionsEnabled);
            }
            catch (Exception ex) {
                ex.LogException();
            }
        }

        internal static void AddUI(UIHelperBase tab) {

            var group = tab.AddGroup(T("VR.Group:On roads"));

            VehicleRestrictionsAggression.AddUI(group);

            BanRegularTrafficOnBusLanes.AddUI(group);

            NoDoubleCrossings.AddUI(group);
        }

        private static string T(string key) => Translation.Options.Get(key);


        private static void OnBanRegularTrafficOnBusLanesChanged(bool newValue) {
            VehicleRestrictionsManager.Instance.ClearCache();

            OptionsManager.ReinitialiseSubTools();

            if (SavedGameOptions.Instance.DedicatedTurningLanes)
                LaneArrowManager.Instance.UpdateDedicatedTurningLanePolicy(false);
        }

        private static void JunctionRestrictionsUpdateHandler(bool _) =>
            OptionsManager.UpdateJunctionRestrictionsManager();

    }
}