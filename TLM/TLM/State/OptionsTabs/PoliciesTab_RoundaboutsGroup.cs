namespace TrafficManager.State {
    using ICities;
    using TrafficManager.UI.Helpers;
    using TrafficManager.UI;
    using System;
    using TrafficManager.Util;
    using TrafficManager.State.Helpers;

    public class PoliciesTab_RoundaboutsGroup {
        //public static CheckboxOption RoundAboutQuickFix_DedicatedExitLanes =new () {
        //    Scope = Scope.Savegame,
        //    Label = "Roundabout.Option:Allocate dedicated exit lanes",
        //    Tooltip = "Roundabout.Tooltip:Allocate dedicated exit lanes",
        //};

        //public static CheckboxOption RoundAboutQuickFix_StayInLaneMainR =
        //    new (nameof(Options.RoundAboutQuickFix_StayInLaneMainR), Scope.Savegame) {
        //        DefaultValue = true,
        //        Label = "Roundabout.Option:Stay in lane inside roundabout",
        //    };

        //public static CheckboxOption RoundAboutQuickFix_StayInLaneNearRabout =
        //    new (nameof(Options.RoundAboutQuickFix_StayInLaneNearRabout), Scope.Savegame) {
        //        DefaultValue = true,
        //        Label = "Roundabout.Option:Stay in lane outside roundabout",
        //        Tooltip = "Roundabout.Tooltip:Stay in lane outside roundabout",
        //    };

        public static CheckboxOption RoundAboutQuickFix_NoCrossMainR = new() {
            Option = Options.Instance.RoundAboutQuickFix_NoCrossMainR,
            Scope = Scope.Savegame,
            Label = "Roundabout.Option:No crossing inside",
        };

        //public static CheckboxOption RoundAboutQuickFix_NoCrossYieldR =
        //    new (nameof(Options.RoundAboutQuickFix_NoCrossYieldR), Scope.Savegame) {
        //        Label = "Roundabout.Option:No crossing on incoming roads",
        //    };

        //public static CheckboxOption RoundAboutQuickFix_PrioritySigns =
        //    new (nameof(Options.RoundAboutQuickFix_PrioritySigns), Scope.Savegame) {
        //        DefaultValue = true,
        //        Label = "Roundabout.Option:Set priority signs",
        //    };

        //public static CheckboxOption RoundAboutQuickFix_KeepClearYieldR =
        //    new (nameof(Options.RoundAboutQuickFix_KeepClearYieldR), Scope.Savegame) {
        //        DefaultValue = true,
        //        Label = "Roundabout.Option:Yielding vehicles keep clear of blocked roundabout",
        //        Tooltip = "Roundabout.Tooltip:Yielding vehicles keep clear of blocked roundabout",
        //    };

        //public static CheckboxOption RoundAboutQuickFix_RealisticSpeedLimits =
        //    new (nameof(Options.RoundAboutQuickFix_RealisticSpeedLimits), Scope.Savegame) {
        //        Label = "Roundabout.Option:Assign realistic speed limits to roundabouts",
        //        Tooltip = "Roundabout.Tooltip:Assign realistic speed limits to roundabouts",
        //    };

        //public static CheckboxOption RoundAboutQuickFix_ParkingBanMainR =
        //    new (nameof(Options.RoundAboutQuickFix_ParkingBanMainR), Scope.Savegame) {
        //        DefaultValue = true,
        //        Label = "Roundabout.Option:Put parking ban inside roundabouts",
        //    };

        //public static CheckboxOption RoundAboutQuickFix_ParkingBanYieldR =
        //    new (nameof(Options.RoundAboutQuickFix_ParkingBanYieldR), Scope.Savegame) {
        //        Label = "Roundabout.Option:Put parking ban on roundabout branches",
        //    };

        public void AddUI(UIHelperBase tab) {

            var group = tab.AddGroup(T("MassEdit.Group:Roundabouts"));

            RoundAboutQuickFix_NoCrossMainR.AddUI(group);
            //RoundAboutQuickFix_NoCrossYieldR.AddUI(group);
            //RoundAboutQuickFix_StayInLaneMainR.AddUI(group);
            //RoundAboutQuickFix_StayInLaneNearRabout.AddUI(group);
            //RoundAboutQuickFix_DedicatedExitLanes.AddUI(group);
            //RoundAboutQuickFix_PrioritySigns.AddUI(group);
            //RoundAboutQuickFix_KeepClearYieldR.AddUI(group);
            //RoundAboutQuickFix_RealisticSpeedLimits.AddUI(group);
            //RoundAboutQuickFix_ParkingBanMainR.AddUI(group);
            //RoundAboutQuickFix_ParkingBanYieldR.AddUI(group);
        }

        private static string T(string key) => Translation.Options.Get(key);
    }
}
