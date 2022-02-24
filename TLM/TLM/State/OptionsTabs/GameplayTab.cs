namespace TrafficManager.State {
    using TrafficManager.UI.Helpers;
    using TrafficManager.UI;
    using TrafficManager.Lifecycle;

    public static class GameplayTab {

        internal static void MakeSettings_Gameplay(ExtUITabstrip tabStrip) {
            UIHelper tab = tabStrip.AddTabPage(Translation.Options.Get("Tab:Gameplay"));

            GameplayTab_VehicleBehaviourGroup.AddUI(tab);

            GameplayTab_AIGroups.AddUI(tab);
        }

        private static void OnAltLaneSelectionRatioChanged(float newVal) {
            // Only call this if the game is running, not during the loading time
            if (TMPELifecycle.Instance.IsGameLoaded) {
                //_altLaneSelectionRatioSlider.RefreshTooltip();
            }
        }
    }
}
