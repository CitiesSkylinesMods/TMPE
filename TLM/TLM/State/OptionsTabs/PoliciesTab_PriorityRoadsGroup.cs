namespace TrafficManager.State {
    using ICities;
    using TrafficManager.UI.Helpers;
    using TrafficManager.UI;
    using System;
    using TrafficManager.Util;

    public static class PoliciesTab_PriorityRoadsGroup {

        public static CheckboxOption PriorityRoad_CrossMainR =
            new (nameof(SavedGameOptions.PriorityRoad_CrossMainR), Scope.Savegame) {
                Label = "Priority roads.Option:Allow pedestrian crossings on main road",
            };

        public static CheckboxOption PriorityRoad_AllowLeftTurns =
            new (nameof(SavedGameOptions.PriorityRoad_AllowLeftTurns), Scope.Savegame) {
                Label = "Priority roads.Option:Allow far turns",
                Tooltip = "Priority roads.Tooltip:Allow far turns",
            };

        public static CheckboxOption PriorityRoad_EnterBlockedYeild =
            new (nameof(SavedGameOptions.PriorityRoad_EnterBlockedYeild), Scope.Savegame) {
                Label = "Priority roads.Option:Enter blocked yield road",
            };

        public static CheckboxOption PriorityRoad_StopAtEntry =
            new (nameof(SavedGameOptions.PriorityRoad_StopAtEntry), Scope.Savegame) {
                Label = "Priority roads.Option:Stop signs on entry",
                Tooltip = "Priority roads.Tooltip:Stop signs on entry",
            };

        static PoliciesTab_PriorityRoadsGroup() {
            try {
                // TODO: This won't work currently as `true` = vanilla behaviour
                PriorityRoad_CrossMainR
                    .PropagateTrueTo(MaintenanceTab_FeaturesGroup.JunctionRestrictionsEnabled);
                PriorityRoad_EnterBlockedYeild
                    .PropagateTrueTo(MaintenanceTab_FeaturesGroup.PrioritySignsEnabled)
                    .PropagateTrueTo(MaintenanceTab_FeaturesGroup.JunctionRestrictionsEnabled);
                PriorityRoad_StopAtEntry
                    .PropagateTrueTo(MaintenanceTab_FeaturesGroup.PrioritySignsEnabled);
            }
            catch (Exception ex) {
                ex.LogException();
            }
        }

        internal static void AddUI(UIHelperBase tab) {

            var group = tab.AddGroup(T("MassEdit.Group.Priority roads"));

            PriorityRoad_CrossMainR.AddUI(group);
            PriorityRoad_AllowLeftTurns.AddUI(group);
            PriorityRoad_EnterBlockedYeild.AddUI(group);
            PriorityRoad_StopAtEntry.AddUI(group);
        }

        private static string T(string key) => Translation.Options.Get(key);
    }
}
