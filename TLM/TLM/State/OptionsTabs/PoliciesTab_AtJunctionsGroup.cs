namespace TrafficManager.State {
    using ICities;
    using System;
    using TrafficManager.Manager.Impl;
    using TrafficManager.UI;
    using TrafficManager.UI.Helpers;
    using TrafficManager.Util;

    public static class PoliciesTab_AtJunctionsGroup {

        public static CheckboxOption AllRelaxed =
            new (nameof(SavedGameOptions.allRelaxed), Scope.Savegame) {
                Label = "VR.Checkbox:All vehicles may ignore lane arrows",
                Validator = DebugOnlyValidator,
            };
        public static CheckboxOption RelaxedBusses =
            new (nameof(SavedGameOptions.relaxedBusses), Scope.Savegame) {
                Label = "VR.Checkbox:Buses may ignore lane arrows",
            };
        public static CheckboxOption AllowEnterBlockedJunctions =
            new (nameof(SavedGameOptions.allowEnterBlockedJunctions), Scope.Savegame) {
                Label = "VR.Checkbox:Vehicles may enter blocked junctions",
                Handler = OnJunctionRestrictionPolicyChanged,
            };
        public static CheckboxOption AllowUTurns =
            new (nameof(SavedGameOptions.allowUTurns), Scope.Savegame) {
                Label = "VR.Checkbox:Vehicles may do u-turns at junctions",
                Handler = OnJunctionRestrictionPolicyChanged,
            };
        public static CheckboxOption AllowNearTurnOnRed =
            new (nameof(SavedGameOptions.allowNearTurnOnRed), Scope.Savegame) {
                Label = "VR.Checkbox:Vehicles may turn on red",
                Handler = OnJunctionRestrictionPolicyChanged,
            };
        public static CheckboxOption AllowFarTurnOnRed =
            new (nameof(SavedGameOptions.allowFarTurnOnRed), Scope.Savegame) {
                Label = "VR.Checkbox:Also apply to left/right turns between one-way streets",
                Indent = true,
                Handler = OnJunctionRestrictionPolicyChanged,
            };
        public static CheckboxOption AllowLaneChangesWhileGoingStraight =
            new (nameof(SavedGameOptions.allowLaneChangesWhileGoingStraight), Scope.Savegame) {
                Label = "VR.Checkbox:Vehicles going straight may change lanes at junctions",
                Handler = OnJunctionRestrictionPolicyChanged,
            };
        public static CheckboxOption TrafficLightPriorityRules =
            new (nameof(SavedGameOptions.trafficLightPriorityRules), Scope.Savegame) {
                Label = "VR.Checkbox:Vehicles follow priority rules at junctions with timedTL",
            };
        public static CheckboxOption AutomaticallyAddTrafficLightsIfApplicable =
            new (nameof(SavedGameOptions.automaticallyAddTrafficLightsIfApplicable), Scope.Savegame) {
                Label = "VR.Checkbox:Automatically add traffic lights if applicable",
            };
        public static CheckboxOption DedicatedTurningLanes =
            new (nameof(DedicatedTurningLanes)) {
                Label = "VR.Option:Dedicated turning lanes",
                Handler = OnDedicatedTurningLanesChanged,
            };

        static PoliciesTab_AtJunctionsGroup() {
            try {
                AllowEnterBlockedJunctions
                    .PropagateTrueTo(MaintenanceTab_FeaturesGroup.JunctionRestrictionsEnabled);
                AllowUTurns
                    .PropagateTrueTo(MaintenanceTab_FeaturesGroup.JunctionRestrictionsEnabled);
                AllowNearTurnOnRed
                    .PropagateTrueTo(MaintenanceTab_FeaturesGroup.TurnOnRedEnabled);
                AllowFarTurnOnRed
                    .PropagateTrueTo(AllowNearTurnOnRed);
                AllowLaneChangesWhileGoingStraight
                    .PropagateTrueTo(MaintenanceTab_FeaturesGroup.JunctionRestrictionsEnabled);
                TrafficLightPriorityRules
                    .PropagateTrueTo(MaintenanceTab_FeaturesGroup.PrioritySignsEnabled)
                    .PropagateTrueTo(MaintenanceTab_FeaturesGroup.TimedLightsEnabled);
            }
            catch (Exception ex) {
                ex.LogException();
            }
        }

        internal static void AddUI(UIHelperBase tab) {

            var group = tab.AddGroup(T("VR.Group:At junctions"));

#if DEBUG
            AllRelaxed.AddUI(group);
#endif
            RelaxedBusses.AddUI(group);
            AllowEnterBlockedJunctions.AddUI(group);
            AllowUTurns.AddUI(group);
            AllowNearTurnOnRed.AddUI(group);
            AllowFarTurnOnRed.AddUI(group);
            AllowLaneChangesWhileGoingStraight.AddUI(group);
            TrafficLightPriorityRules.AddUI(group);
            AutomaticallyAddTrafficLightsIfApplicable.AddUI(group);
            DedicatedTurningLanes.AddUI(group);
        }

        private static string T(string key) => Translation.Options.Get(key);

        private static bool DebugOnlyValidator(bool desired, out bool result) {
            result = VersionUtil.IS_DEBUG && desired;
            return true;
        }

        private static void OnJunctionRestrictionPolicyChanged(bool _) {
            OptionsManager.UpdateJunctionRestrictionsManager();
            OptionsManager.ReinitialiseSubTools();
        }

        private static void OnDedicatedTurningLanesChanged(bool _)
            => OptionsManager.UpdateDedicatedTurningLanes();
    }
}