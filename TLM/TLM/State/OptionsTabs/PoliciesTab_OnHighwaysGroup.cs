namespace TrafficManager.State {
    using ICities;
    using TrafficManager.Lifecycle;
    using TrafficManager.Manager.Impl;
    using TrafficManager.UI;
    using TrafficManager.UI.Helpers;

    public static class PoliciesTab_OnHighwaysGroup {

        public static CheckboxOption HighwayRules =
            new (nameof(Options.highwayRules), Options.PersistTo.Savegame) {
                Label = "VR.Checkbox:Enable highway merging/splitting rules", //legacy
                Handler = OnHighwayRulesChanged,
            };

        public static CheckboxOption HighwayMergingRules =
            new(nameof(Options.highwayMergingRules), Options.PersistTo.Savegame) {
                Label = "VR.Checkbox:Enable highway merging rules",
                Tooltip = "VR.Tooltip: Lightweight",
                Handler = OnHighwayMergingRulesChanged,
            };

        public static CheckboxOption PreferOuterLane =
            new (nameof(Options.preferOuterLane), Options.PersistTo.Savegame) {
                Label = "VR.Checkbox:Heavy trucks prefer outer lanes on highways",
            };

        internal static void AddUI(UIHelperBase tab) {

            var group = tab.AddGroup(T("VR.Group:Highways"));

            HighwayRules.AddUI(group);

            HighwayMergingRules.AddUI(group);

            PreferOuterLane.AddUI(group);
        }

        private static string T(string key) => Translation.Options.Get(key);

        private static void OnHighwayRulesChanged(bool _) {
            if (TMPELifecycle.Instance.Deserializing || !TMPELifecycle.InGameOrEditor())
                return;
            HighwayMergingRules.Value = false;

            Flags.ClearHighwayLaneArrows();
            Flags.ApplyAllFlags();
            OptionsManager.UpdateRoutingManager();
        }

        private static void OnHighwayMergingRulesChanged(bool _) {
            if (TMPELifecycle.Instance.Deserializing || !TMPELifecycle.InGameOrEditor())
                return;
            HighwayRules.Value = false;
            OptionsManager.UpdateRoutingManager();
        }

    }
}