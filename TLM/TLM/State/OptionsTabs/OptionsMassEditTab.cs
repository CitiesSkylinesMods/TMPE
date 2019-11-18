namespace TrafficManager.State {
    using ColossalFramework.UI;
    using ICities;
    using UI;
    using Manager.Impl;

    using static Options;
    using UnityEngine;

    public static class OptionsMassEditTab {
        //TODO: Add options to a list.

        //rabout_DecicatedExitLanes
        //rabout_SwitchLanesYeildR<
        //rabout_SwitchLanesMainR*<
        //rabout_StayInLaneMainR
        //rabout_StayInLaneNearRabout
        //rabout_NoCrossMainR
        //rabout_NoCrossYeildR<
        //rabout_PrioritySigns*

        //avn_NoCrossMainR
        //avn_NoCrossYield*<
        //avn_NoLeftTurns*<
        //avn_EnterBlockedMain*<
        //avn_EnterBlockedYeild*
        //avn_StopEntry*
        //avn_PrioritySigns*
        //avn_SwitchLanesMain*<
        //avn_SwtichLanesYeild*<

        //TODO remove comments after adding translations.
        public static CheckboxOption rabout_DecicatedExitLanes = new CheckboxOption(
            key: "rabout_DecicatedExitLanes",
            default_value: false,
            group_name: "MassEdit",
            tooltip: true);
        // label: dedicated exit lanes.
        // tooltip: one dedicated lane for each exit, the rest of lanes go forw

        public static CheckboxOption rabout_SwitchLanesYeildR = new CheckboxOption(
            key: "rabout_SwitchLanesYeildR",
            default_value: false,
            group_name: "MassEdit",
            true);
        // Vehicles may choose their lane while entring the roundabout
        // If turned off, only highways may choose lane.

        public static CheckboxOption rabout_StayInLaneMainR = new CheckboxOption(
            key: "rabout_StayInLaneMainR",
            default_value: false,
            group_name: "MassEdit",
            true);
        // Stay in lane inside the roundabout.
        // If activated, vehicles will stay in lane inside the roundabout may only switch at exits.

        public static CheckboxOption rabout_StayInLaneNearRabout = new CheckboxOption(
            key: "rabout_StayInLaneNearRabout",
            default_value: false,
            group_name: "MassEdit",
            true);
        // Stay in lane near the roundabout.
        // vehicles shall not jam triffc by lane switching too close to the roundabout.

        public static CheckboxOption rabout_NoCrossMainR = new CheckboxOption(
            key: "rabout_NoCrossMainR",
            default_value: true,
            group_name: "MassEdit");
        // Pedesterians shall not cross to the center of roundabout.

        public static CheckboxOption rabout_NoCrossYeildR = new CheckboxOption(
            key: "rabout_NoCrossYeildR",
            default_value: false,
            group_name: "MassEdit");
        // Pedesterians shall not cross the roads around the roundabout.

        public static CheckboxOption avn_NoCrossMainR = new CheckboxOption(
            key: "avn_NoCrossMainR",
            default_value: true,
            group_name: "MassEdit");
        // Pedesterians shall not cross Main avenue at small junctions.

        internal static void MakeSettings_MasEdit(ExtUITabstrip tabStrip, int tabIndex) {
            UIHelper panelHelper = tabStrip.AddTabPage(Translation.Options.Get("Tab:MassEdit"));
            MakePanel_MasEdit(panelHelper);
        }

        internal static void MakePanel_MasEdit(UIHelperBase panelHelper) {
            UIHelperBase raboutGroup = panelHelper.AddGroup(
                Translation.Options.Get("MassEdit.Group: Roundabouts"));
            rabout_NoCrossMainR.AddUI(raboutGroup);
            rabout_NoCrossYeildR.AddUI(raboutGroup);
            rabout_StayInLaneMainR.AddUI(raboutGroup);
            rabout_StayInLaneNearRabout.AddUI(raboutGroup);
            rabout_SwitchLanesYeildR.AddUI(raboutGroup);
            rabout_DecicatedExitLanes.AddUI(raboutGroup);

            UIHelperBase avnGroup = panelHelper.AddGroup(
                    Translation.Options.Get("MassEdit.Group.Priority: Priority roads"));
            avn_NoCrossMainR.AddUI(avnGroup);
        }
    } // end class
}
