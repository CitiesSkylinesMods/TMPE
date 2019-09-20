namespace TrafficManager.State {
    using ColossalFramework.UI;
    using ICities;
    using Keybinds;
    using UI;

    public static class OptionsKeybindsTab {
        internal static void MakeSettings_Keybinds(UITabstrip tabStrip, int tabIndex) {
            string keybindsTabText = Translation.Options.Get("Tab:Keybinds");
            Options.AddOptionTab(tabStrip, keybindsTabText);
            tabStrip.selectedIndex = tabIndex;

            var currentPanel = tabStrip.tabContainer.components[tabIndex] as UIPanel;
            currentPanel.autoLayout = true;
            currentPanel.autoLayoutDirection = LayoutDirection.Vertical;
            currentPanel.autoLayoutPadding.top = 5;
            currentPanel.autoLayoutPadding.left = 10;
            currentPanel.autoLayoutPadding.right = 10;

            var panelHelper = new UIHelper(currentPanel);

            UIHelperBase keyboardGroup = panelHelper.AddGroup(keybindsTabText);
            ((UIPanel)((UIHelper)keyboardGroup).self).gameObject.AddComponent<KeybindSettingsPage>();
        }
    }
}
