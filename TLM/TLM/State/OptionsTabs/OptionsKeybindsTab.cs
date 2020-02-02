namespace TrafficManager.State {
    using ColossalFramework.UI;
    using ICities;
    using TrafficManager.State.Keybinds;
    using TrafficManager.UI.Helpers;
    using TrafficManager.UI;

    public static class OptionsKeybindsTab {
        internal static void MakeSettings_Keybinds(ExtUITabstrip tabStrip) {
            string keybindsTabText = Translation.Options.Get("Tab:Keybinds");
            UIHelper panelHelper = tabStrip.AddTabPage(keybindsTabText, false);
            UIHelperBase keyboardGroup = panelHelper.AddGroup(keybindsTabText);
            ((UIPanel)((UIHelper)keyboardGroup).self).gameObject.AddComponent<KeybindSettingsPage>();
        }
    }
}
