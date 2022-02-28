namespace TrafficManager.State {
    using TrafficManager.UI;
    using TrafficManager.UI.Helpers;
    using TrafficManager.UI.WhatsNew;

    public static class GeneralTab {
        public static ActionButton WhatsNewButton = new() {
            Label = "What's New?",
            Handler = WhatsNew.OpenModal,
        };

        internal static void MakeSettings_General(ExtUITabstrip tabStrip) {

            UIHelper tab = tabStrip.AddTabPage(T("Tab:General"));

            tab.AddSpace(5);
            WhatsNewButton.AddUI(tab);
            tab.AddSpace(5);

            GeneralTab_LocalisationGroup.AddUI(tab);

            GeneralTab_SimulationGroup.AddUI(tab);

            GeneralTab_InterfaceGroup.AddUI(tab);

            GeneralTab_CompatibilityGroup.AddUI(tab);
        }

        private static string T(string key) => Translation.Options.Get(key);
    }
}
