namespace TrafficManager.State {
    using ICities;
    using TrafficManager.UI.Helpers;
    using TrafficManager.UI;

    public static class OptionsMassEditTab {
        public static CheckboxOption RoundAboutQuickFix_DedicatedExitLanes =
            new CheckboxOption("RoundAboutQuickFix_DedicatedExitLanes") {
            Label = "Roundabout.Option:Allocate dedicated exit lanes",
            Tooltip = "Roundabout.Tooltip:Allocate dedicated exit lanes",
        };

        public static CheckboxOption RoundAboutQuickFix_StayInLaneMainR =
            new CheckboxOption("RoundAboutQuickFix_StayInLaneMainR") {
            Label = "Roundabout.Option:Stay in lane inside roundabout",
        };

        public static CheckboxOption RoundAboutQuickFix_StayInLaneNearRabout =
            new CheckboxOption("RoundAboutQuickFix_StayInLaneNearRabout") {
            Label = "Roundabout.Option:Stay in lane outside roundabout",
            Tooltip = "Roundabout.Tooltip:Stay in lane outside roundabout",
        };

        public static CheckboxOption RoundAboutQuickFix_NoCrossMainR =
            new CheckboxOption("RoundAboutQuickFix_NoCrossMainR") {
            Label = "Roundabout.Option:No crossing inside",
        };

        public static CheckboxOption RoundAboutQuickFix_NoCrossYieldR =
            new CheckboxOption("RoundAboutQuickFix_NoCrossYieldR") {
            Label = "Roundabout.Option:No crossing on incoming roads",
        };

        public static CheckboxOption RoundAboutQuickFix_PrioritySigns =
            new CheckboxOption("RoundAboutQuickFix_PrioritySigns") {
            Label = "Roundabout.Option:Set priority signs",
        };

        public static void MakeSettings_MassEdit(ExtUITabstrip tabStrip, int tabIndex)
        {
            UIHelper panelHelper = tabStrip.AddTabPage(T("Tab:MassEdit"));
            MakePanel_MassEdit(panelHelper);
        }

        internal static void MakePanel_MassEdit(UIHelperBase panelHelper) {
            UIHelperBase raboutGroup = panelHelper.AddGroup(T("MassEdit.Group:Roundabouts"));
            RoundAboutQuickFix_NoCrossMainR.AddUI(raboutGroup);
            RoundAboutQuickFix_NoCrossYieldR.AddUI(raboutGroup);
            RoundAboutQuickFix_StayInLaneMainR.AddUI(raboutGroup);
            RoundAboutQuickFix_StayInLaneNearRabout.AddUI(raboutGroup);
            RoundAboutQuickFix_DedicatedExitLanes.AddUI(raboutGroup);
            RoundAboutQuickFix_PrioritySigns.AddUI(raboutGroup);
        }

        private static string T(string key) => Translation.Options.Get(key);
    } // end class
}
