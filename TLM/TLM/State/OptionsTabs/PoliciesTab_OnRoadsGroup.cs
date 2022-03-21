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
            new (nameof(Options.banRegularTrafficOnBusLanes), Options.PersistTo.Savegame) {
                Label = "VR.Checkbox:Ban private cars and trucks on bus lanes",
                Handler = OnBanRegularTrafficOnBusLanesChanged,
            };

        // at a segment to segment transition, only the smaller segment gets crossings
        public static CheckboxOption NoDoubleCrossings =
            new (nameof(Options.NoDoubleCrossings), Options.PersistTo.Savegame) {
                Label = "VR.Option:No double crossings",
                Handler = JunctionRestrictionsUpdateHandler,
            };

        private static UIDropDown _vehicleRestrictionsAggressionDropDown;

        static PoliciesTab_OnRoadsGroup() {
            try {
                BanRegularTrafficOnBusLanes
                    .PropagateTrueTo(MaintenanceTab_FeaturesGroup.VehicleRestrictionsEnabled);
            }
            catch (Exception ex) {
                ex.LogException();
            }
        }

        public static void SetVehicleRestrictionsAggression(VehicleRestrictionsAggression val) {
            _vehicleRestrictionsAggressionDropDown.selectedIndex = (int)val;
        }

        internal static void AddUI(UIHelperBase tab) {

            var group = tab.AddGroup(T("VR.Group:On roads"));

            AddVehicleRestrictionsDropDown(group);

            BanRegularTrafficOnBusLanes.AddUI(group);

            NoDoubleCrossings.AddUI(group);
        }

        private static string T(string key) => Translation.Options.Get(key);

        private static void AddVehicleRestrictionsDropDown(UIHelperBase group) {
            var items = new[]
            {
                T("VR.Dropdown.Option:Low Aggression"),
                T("VR.Dropdown.Option:Medium Aggression"),
                T("VR.Dropdown.Option:High Aggression"),
                T("VR.Dropdown.Option:Strict"),
            };

            _vehicleRestrictionsAggressionDropDown = group.AddDropdown(
                T("VR.Dropdown:Vehicle restrictions aggression") + ":",
                items,
                (int)Options.vehicleRestrictionsAggression,
                OnVehicleRestrictionsAggressionChanged) as UIDropDown;
        }

        private static void OnVehicleRestrictionsAggressionChanged(int val)
            => Options.vehicleRestrictionsAggression = (VehicleRestrictionsAggression)val;

        private static void OnBanRegularTrafficOnBusLanesChanged(bool newValue) {
            VehicleRestrictionsManager.Instance.ClearCache();

            OptionsManager.ReinitialiseSubTools();

            if (Options.DedicatedTurningLanes)
                LaneArrowManager.Instance.UpdateDedicatedTurningLanePolicy(false);
        }

        private static void JunctionRestrictionsUpdateHandler(bool _) =>
            OptionsManager.UpdateJunctionRestrictionsManager();

    }
}