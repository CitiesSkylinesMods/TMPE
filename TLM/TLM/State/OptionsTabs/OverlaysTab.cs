namespace TrafficManager.State {
    using TrafficManager.UI.Helpers;
    using TrafficManager.UI;

    public static class OverlaysTab {
        internal static void MakeSettings_Overlays(ExtUITabstrip tabStrip) {
            var tab = tabStrip.AddTabPage(Translation.Options.Get("Tab:Overlays"));

            OverlaysTab_OverlaysGroup.AddUI(tab);
        }
    }
}
