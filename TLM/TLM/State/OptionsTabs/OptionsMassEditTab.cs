namespace TrafficManager.State {
    using ICities;
    using UI;
    using UI.Helpers;

    public static class OptionsMassEditTab {
        internal static CheckboxOption rabout_DedicatedExitLanes =
            new CheckboxOption("rabout_DedicatedExitLanes") {
            Label = "Roundabout.Option:Allocate dedicated exit lanes",
            Tooltip = "Roundabout.Tooltip:Allocate dedicated exit lanes",
        };

        internal static CheckboxOption rabout_StayInLaneMainR =
            new CheckboxOption("rabout_StayInLaneMainR") {
            Label = "Roundabout.Option:Stay in lane inside roundabout",
        };

        internal static CheckboxOption rabout_StayInLaneNearRabout =
            new CheckboxOption("rabout_StayInLaneNearRabout") {
            Label = "Roundabout.Option:Stay in lane outside roundabout",
            Tooltip = "Roundabout.Tooltip:Stay in lane outside roundabout",
        };

        internal static CheckboxOption rabout_NoCrossMainR =
            new CheckboxOption("rabout_NoCrossMainR") {
            Label = "Roundabout.Option:No crossing inside",
        };

        internal static CheckboxOption rabout_NoCrossYeildR =
            new CheckboxOption("rabout_NoCrossYeildR") {
            Label = "Roundabout.Option:No crossing on incoming roads",
        };

        internal static CheckboxOption rabout_PrioritySigns =
            new CheckboxOption("rabout_PrioritySigns") {
            Label = "Roundabout.Option:Set priority signs",
        };

        internal static void MakeSettings_MassEdit(ExtUITabstrip tabStrip, int tabIndex)
        {
            UIHelper panelHelper = tabStrip.AddTabPage(T("Tab:MassEdit"));
            MakePanel_MassEdit(panelHelper);
        }

        internal static void MakePanel_MassEdit(UIHelperBase panelHelper) {
            UIHelperBase raboutGroup = panelHelper.AddGroup(T("MassEdit.Group:Roundabouts"));
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
