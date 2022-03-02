namespace TrafficManager.State {
    using TrafficManager.UI.Helpers;
    using TrafficManager.UI;

    public static class GameplayTab {

        internal static void MakeSettings_Gameplay(ExtUITabstrip tabStrip) {
            UIHelper tab = tabStrip.AddTabPage(Translation.Options.Get("Tab:Gameplay"));

            GameplayTab_VehicleBehaviourGroup.AddUI(tab);

            GameplayTab_AIGroups.AddUI(tab);
        }
    }
}
