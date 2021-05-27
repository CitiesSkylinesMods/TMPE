namespace TrafficManager.State {
    using ICities;
    using TrafficManager.UI.Helpers;
    using TrafficManager.UI;

    public static class OptionsMassEditTab {
        #region Roundabout options
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

        public static CheckboxOption RoundAboutQuickFix_KeepClearYieldR =
            new CheckboxOption("RoundAboutQuickFix_KeepClearYieldR") {
            Label = "Roundabout.Option:Yielding vehicles keep clear of blocked roundabout",
            Tooltip = "Roundabout.Tooltip:Yielding vehicles keep clear of blocked roundabout",
        };

        public static CheckboxOption RoundAboutQuickFix_RealisticSpeedLimits =
            new CheckboxOption("RoundAboutQuickFix_RealisticSpeedLimits") {
            Label = "Roundabout.Option:Assign realistic speed limits to roundabouts",
            Tooltip = "Roundabout.Tooltip:Assign realistic speed limits to roundabouts",
        };

        public static CheckboxOption RoundAboutQuickFix_ParkingBanMainR =
            new CheckboxOption("RoundAboutQuickFix_ParkingBanMainR") {
            Label = "Roundabout.Option:Put parking ban inside roundabouts",
        };

        public static CheckboxOption RoundAboutQuickFix_ParkingBanYieldR =
            new CheckboxOption("RoundAboutQuickFix_ParkingBanYieldR") {
            Label = "Roundabout.Option:Put parking ban on roundabout branches",
        };

        #endregion
        #region Prioirty road options
        public static CheckboxOption PriorityRoad_CrossMainR =
            new CheckboxOption("PriorityRoad_CrossMainR") {
                Label = "Priority roads.Option:Allow pedestrian crossings on main road",
        };

        public static CheckboxOption PriorityRoad_AllowLeftTurns =
            new CheckboxOption("PriorityRoad_AllowLeftTurns") {
                Label = "Priority roads.Option:Allow far turns",
                Tooltip = "Priority roads.Tooltip:Allow far turns",
            };

        public static CheckboxOption PriorityRoad_EnterBlockedYeild =
            new CheckboxOption("PriorityRoad_EnterBlockedYeild") {
                Label = "Priority roads.Option:Enter blocked yield road",
        };

        public static CheckboxOption PriorityRoad_StopAtEntry =
            new CheckboxOption("PriorityRoad_StopAtEntry") {
                Label = "Priority roads.Option:Stop signs on entry",
                Tooltip = "Priority roads.Tooltip:Stop signs on entry",
        };
        #endregion

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
            RoundAboutQuickFix_KeepClearYieldR.AddUI(raboutGroup);
            RoundAboutQuickFix_RealisticSpeedLimits.AddUI(raboutGroup);
            RoundAboutQuickFix_ParkingBanMainR.AddUI(raboutGroup);
            RoundAboutQuickFix_ParkingBanYieldR.AddUI(raboutGroup);

            UIHelperBase priorityRoadGroup = panelHelper.AddGroup(T("MassEdit.Group.Priority roads"));
            PriorityRoad_CrossMainR.AddUI(priorityRoadGroup);
            PriorityRoad_AllowLeftTurns.AddUI(priorityRoadGroup);
            PriorityRoad_EnterBlockedYeild.AddUI(priorityRoadGroup);
            PriorityRoad_StopAtEntry.AddUI(priorityRoadGroup);
        }

        private static string T(string key) => Translation.Options.Get(key);
    } // end class
}
