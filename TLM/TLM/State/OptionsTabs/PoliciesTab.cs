namespace TrafficManager.State {
    using TrafficManager.UI.Helpers;
    using TrafficManager.UI;

    public class PoliciesTab {
        PoliciesTab_RoundaboutsGroup PoliciesTab_RoundaboutsGroup = new();

        internal void MakeSettings_VehicleRestrictions(ExtUITabstrip tabStrip) {

            UIHelper tab = tabStrip.AddTabPage(Translation.Options.Get("Tab:Policies & Restrictions"));

            //PoliciesTab_AtJunctionsGroup.AddUI(tab);

            //PoliciesTab_OnRoadsGroup.AddUI(tab);

            //PoliciesTab_OnHighwaysGroup.AddUI(tab);

            //PoliciesTab_InEmergenciesGroup.AddUI(tab);

            //PoliciesTab_PriorityRoadsGroup.AddUI(tab);

            PoliciesTab_RoundaboutsGroup.AddUI(tab);
        }
    }
}
