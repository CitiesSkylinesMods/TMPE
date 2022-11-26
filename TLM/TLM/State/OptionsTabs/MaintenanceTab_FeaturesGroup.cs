namespace TrafficManager.State {
    using ICities;
    using System;
    using TrafficManager.Lifecycle;
    using TrafficManager.Manager.Impl;
    using TrafficManager.UI;
    using TrafficManager.UI.Helpers;
    using TrafficManager.Util;

    public static class MaintenanceTab_FeaturesGroup {

        public static CheckboxOption PrioritySignsEnabled =
            new (nameof(Options.prioritySignsEnabled), Options.Scope.Savegame) {
                DefaultValue = true,
                Label = "Checkbox:Priority signs",
                Handler = OnFeatureAvailabilityChanged,
            };
        public static CheckboxOption TimedLightsEnabled =
            new (nameof(Options.timedLightsEnabled), Options.Scope.Savegame) {
                DefaultValue = true,
                Label = "Checkbox:Timed traffic lights",
                Validator = TrafficLightsValidator,
                Handler = OnFeatureAvailabilityChanged,
            };
        public static CheckboxOption CustomSpeedLimitsEnabled =
            new (nameof(Options.customSpeedLimitsEnabled), Options.Scope.Savegame) {
                DefaultValue = true,
                Label = "Checkbox:Speed limits",
                Handler = OnFeatureAvailabilityChanged,
            };
        public static CheckboxOption VehicleRestrictionsEnabled =
            new (nameof(Options.vehicleRestrictionsEnabled), Options.Scope.Savegame) {
                DefaultValue = true,
                Label = "Checkbox:Vehicle restrictions",
                Handler = OnFeatureAvailabilityChanged,
            };
        public static CheckboxOption ParkingRestrictionsEnabled =
            new (nameof(Options.parkingRestrictionsEnabled), Options.Scope.Savegame) {
                DefaultValue = true,
                Label = "Checkbox:Parking restrictions",
                Handler = OnFeatureAvailabilityChanged,
            };
        public static CheckboxOption JunctionRestrictionsEnabled =
            new (nameof(Options.junctionRestrictionsEnabled), Options.Scope.Savegame) {
                DefaultValue = true,
                Label = "Checkbox:Junction restrictions",
                Handler = OnFeatureAvailabilityChanged,
            };
        public static CheckboxOption TurnOnRedEnabled =
            new (nameof(Options.turnOnRedEnabled), Options.Scope.Savegame) {
                DefaultValue = true,
                Label = "Maintenance.Checkbox:Turn on red",
                Indent = true,
            };
        public static CheckboxOption LaneConnectorEnabled =
            new (nameof(Options.laneConnectorEnabled), Options.Scope.Savegame) {
                DefaultValue = true,
                Label = "Maintenance.Checkbox:Lane connector",
                Handler = OnLaneConnectorEnabledChanged,
            };

        static MaintenanceTab_FeaturesGroup() {
            try {
                TurnOnRedEnabled
                    .PropagateTrueTo(JunctionRestrictionsEnabled);
            }
            catch (Exception ex) {
                ex.LogException();
            }
        }

        internal static void AddUI(UIHelperBase tab) {
            var group = tab.AddGroup(T("Maintenance.Group:Activated features"));

            PrioritySignsEnabled.AddUI(group);

            // TODO [issue #959] remove `if` when TTL is implemented in asset editor.
            if (CanTrafficLightsBeUsed)
                TimedLightsEnabled.AddUI(group);

            CustomSpeedLimitsEnabled.AddUI(group);
            VehicleRestrictionsEnabled.AddUI(group);
            ParkingRestrictionsEnabled.AddUI(group);
            JunctionRestrictionsEnabled.AddUI(group);
            TurnOnRedEnabled.AddUI(group);
            LaneConnectorEnabled.AddUI(group);
        }

        private static string T(string key) => Translation.Options.Get(key);

        private static bool CanTrafficLightsBeUsed
            => TMPELifecycle.PlayMode || TMPELifecycle.InMapOrScenarioEditor;

        // TODO [issue #959] remove when TTL is implemented in asset editor.
        private static bool TrafficLightsValidator(bool desired, out bool result) {
            result = CanTrafficLightsBeUsed && desired;
            return true;
        }

        private static void OnFeatureAvailabilityChanged(bool _)
            => OptionsManager.RebuildMenu();

        private static void OnLaneConnectorEnabledChanged(bool _) {
            OptionsManager.RebuildMenu();
            OptionsManager.UpdateRoutingManager();
        }
    }
}