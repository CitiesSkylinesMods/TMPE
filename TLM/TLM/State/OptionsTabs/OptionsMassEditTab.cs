namespace TrafficManager.State {
    using ICities;
    using UI;
    using UI.Helpers;

    public static class OptionsMassEditTab {
        public static CheckboxOption rabout_DedicatedExitLanes = new CheckboxOption(
            key: "rabout_DedicatedExitLanes",
            default_value: true,
            group_name: "MassEdit",
            tooltip: true);

        public static CheckboxOption rabout_StayInLaneMainR = new CheckboxOption(
            key: "rabout_StayInLaneMainR",
            default_value: true,
            group_name: "MassEdit",
            false);

        public static CheckboxOption rabout_StayInLaneNearRabout = new CheckboxOption(
            key: "rabout_StayInLaneNearRabout",
            default_value: true,
            group_name: "MassEdit",
            true);

        public static CheckboxOption rabout_NoCrossMainR = new CheckboxOption(
            key: "rabout_NoCrossMainR",
            default_value: true,
            group_name: "MassEdit");

        public static CheckboxOption rabout_NoCrossYeildR = new CheckboxOption(
            key: "rabout_NoCrossYeildR",
            default_value: false,
            group_name: "MassEdit");

        public static CheckboxOption rabout_PrioritySigns = new CheckboxOption(
            key: "rabout_PrioritySigns",
            default_value: true,
            group_name: "MassEdit");

        internal static void MakeSettings_MassEdit(ExtUITabstrip tabStrip, int tabIndex) {
            UIHelper panelHelper = tabStrip.AddTabPage(T("Tab:MassEdit"));
            MakePanel_MassEdit(panelHelper);
        }

        internal static void MakePanel_MassEdit(UIHelperBase panelHelper) {
            UIHelperBase raboutGroup = panelHelper.AddGroup(
                T("MassEdit.Group: Roundabouts"));
            rabout_NoCrossMainR.AddUI(raboutGroup);
            rabout_NoCrossYeildR.AddUI(raboutGroup);
            rabout_StayInLaneMainR.AddUI(raboutGroup);
            rabout_StayInLaneNearRabout.AddUI(raboutGroup);
            rabout_DedicatedExitLanes.AddUI(raboutGroup);
            rabout_PrioritySigns.AddUI(raboutGroup);
        }

        private static string T(string key) => Translation.Options.Get(key);
    } // end class
}
