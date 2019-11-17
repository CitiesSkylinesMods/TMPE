namespace TrafficManager.State {
    using ColossalFramework.UI;
    using ICities;
    using Keybinds;
    using UI;
    using Manager.Impl;

    public static class OptionsKeybindsTab {
        internal static void MakeSettings_Keybinds(ExtUITabstrip tabStrip) {
            string keybindsTabText = Translation.Options.Get("Tab:Keybinds");
            UIHelper panelHelper = tabStrip.AddTabPage(keybindsTabText);
            UIHelperBase keyboardGroup = panelHelper.AddGroup(keybindsTabText);
            ((UIPanel)((UIHelper)keyboardGroup).self).gameObject.AddComponent<KeybindSettingsPage>();
        }
    }
}
