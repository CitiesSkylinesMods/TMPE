namespace TrafficManager.State {
    using ICities;
    using TrafficManager.Lifecycle;
    using TrafficManager.Manager.Impl;
    using TrafficManager.UI;
    using TrafficManager.UI.Helpers;

    public static class PoliciesTab_OnHighwaysGroup {

        public static CheckboxOption HighwayRules =
            new (nameof(SavedGameOptions.highwayRules), Scope.Savegame) {
                Label = "VR.Checkbox:Enable highway merging/splitting rules", //legacy
                Handler = OnHighwayRulesChanged,
            };

        public static CheckboxOption HighwayMergingRules =
            new(nameof(SavedGameOptions.highwayMergingRules), Scope.Savegame) {
                Label = "VR.Checkbox:Enable highway merging rules",
                Tooltip = "VR.Tooltip: Lightweight merging rules",
                Handler = OnHighwayMergingRulesChanged,
            };

        public static CheckboxOption PreferOuterLane =
            new (nameof(SavedGameOptions.preferOuterLane), Scope.Savegame) {
                Label = "VR.Checkbox:Heavy trucks prefer outer lanes on highways",
            };

        internal static void AddUI(UIHelperBase tab) {

            var group = tab.AddGroup(T("VR.Group:Highways"));

            HighwayMergingRules.AddUI(group);

            HighwayRules.AddUI(group);

            PreferOuterLane.AddUI(group);
        }

        private static string T(string key) => Translation.Options.Get(key);

        static bool dontUpdateRoutingManager;
        private static void OnHighwayRulesChanged(bool enabled) {
            try {
                if (TMPELifecycle.Instance.Deserializing || !TMPELifecycle.InGameOrEditor())
                    return;
                if (enabled) {
                    dontUpdateRoutingManager = true;
                    HighwayMergingRules.Value = false; // don't call UpdateRoutingManager() 2 times.
                    dontUpdateRoutingManager = false;
                }
                Flags.ClearHighwayLaneArrows();
                Flags.ApplyAllFlags();
                if (!dontUpdateRoutingManager) {
                    OptionsManager.UpdateRoutingManager();
                }
            } finally {
                dontUpdateRoutingManager = false;
            }
        }

        private static void OnHighwayMergingRulesChanged(bool enabled) {
            try {
                if (TMPELifecycle.Instance.Deserializing || !TMPELifecycle.InGameOrEditor())
                    return;
                if (enabled) {
                    dontUpdateRoutingManager = true;
                    HighwayRules.Value = false; // don't call UpdateRoutingManager() 2 times.
                    dontUpdateRoutingManager = false;
                }
                if (!dontUpdateRoutingManager) {
                    OptionsManager.UpdateRoutingManager();
                }
            } finally {
                dontUpdateRoutingManager = false;
            }
        }

    }
}