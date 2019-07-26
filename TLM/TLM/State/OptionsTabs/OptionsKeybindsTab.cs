namespace TrafficManager.State {
    using ColossalFramework.UI;
    using ICities;
    using Keybinds;
    using UI;

    public static class OptionsKeybindsTab {
        private static string T(string s) {
            return Translation.GetString(s);
        }

        internal static void MakeSettings_Keybinds(UITabstrip tabStrip, int tabIndex) {
            Options.AddOptionTab(tabStrip, T("Keybinds"));
            tabStrip.selectedIndex = tabIndex;

            var currentPanel = tabStrip.tabContainer.components[tabIndex] as UIPanel;
            currentPanel.autoLayout = true;
            currentPanel.autoLayoutDirection = LayoutDirection.Vertical;
            currentPanel.autoLayoutPadding.top = 5;
            currentPanel.autoLayoutPadding.left = 10;
            currentPanel.autoLayoutPadding.right = 10;

            var panelHelper = new UIHelper(currentPanel);

            UIHelperBase keyboardGroup = panelHelper.AddGroup(T("Keybinds"));
            ((UIPanel)((UIHelper)keyboardGroup).self).gameObject.AddComponent<KeybindSettingsPage>();
        }
    }
}