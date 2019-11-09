namespace TrafficManager.State {
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using ICities;
    using JetBrains.Annotations;
    using UI;

    public static class OptionsMassEditTab {
        private static UICheckBox _rabout_SwitchLanesAtEntryToggle;
        private static UICheckBox _rabout_NoLaneSwitchingInRaboutToggle;
        private static UICheckBox _rabout_NoLaneSwitchingNearEntriesToggle;
        private static UICheckBox _rabout_DecicatedExitLanesToggle;
        private static UICheckBox _rabout_NoCrossingRAboutToggle;
        private static UICheckBox _rabout_NoCrossingAtConnectionsToggle;

        private static UICheckBox _avn_NoZebraCrossingAcrossAvnToggle;

        internal static void MakeSettings_MasEdit(UITabstrip tabStrip, int tabIndex) {
            Options.AddOptionTab(tabStrip, Translation.Options.Get("Tab:MassEdit"));
            tabStrip.selectedIndex = tabIndex;

            var currentPanel = tabStrip.tabContainer.components[tabIndex] as UIPanel;
            currentPanel.autoLayout = true;
            currentPanel.autoLayoutDirection = LayoutDirection.Vertical;
            currentPanel.autoLayoutPadding.top = 5;
            currentPanel.autoLayoutPadding.left = 10;
            currentPanel.autoLayoutPadding.right = 10;

            var panelHelper = new UIHelper(currentPanel);

            // TODO figure out the difference between tooltip, short text, description.

            UIHelperBase raboutGroup = panelHelper.AddGroup(
                Translation.Options.Get("MassEdit.Group:Roundabout Roundabouts)"));

            _rabout_DecicatedExitLanesToggle = panelHelper.AddCheckbox(
                    Translation.Options.Get("MassEdit.Group.Roundabout: dedicated exit lanes"),
                    Options.rabout_DecicatedExitLanes,
                    On_RAbout_DecicatedExitLanes_Changed) as UICheckBox;

            _rabout_NoLaneSwitchingInRaboutToggle = panelHelper.AddCheckbox(
                    Translation.Options.Get("MassEdit.Group.Roundabout: No lane switching in the middle of roundabout"),
                    Options.rabout_NoLaneSwitchingInRabout,
                    On_RAbout_NoLaneSwitchingInRabout_Changed) as UICheckBox;

            _rabout_NoLaneSwitchingNearEntriesToggle = panelHelper.AddCheckbox(
                    Translation.Options.Get("MassEdit.Group.Roundabout: No lane switching too close to entries"),
                    Options.rabout_NoLaneSwitchingNearEntries,
                    On_RAbout_NoLaneSwitchingNearEntries_Changed) as UICheckBox;

            _rabout_SwitchLanesAtEntryToggle = panelHelper.AddCheckbox(
                    Translation.Options.Get("MassEdit.Group:Roundabout Switch lanes at all entries to roundabouts (if turned off switch lanes sign only applies to highway junctions"),
                    Options.rabout_SwitchLanesAtEntry,
                    On_RAbout_SwitchLanesAtEntry_Changed) as UICheckBox;

            _rabout_NoCrossingRAboutToggle = panelHelper.AddCheckbox(
                    Translation.Options.Get("MassEdit.Group.Roundabout: no pedasterians crossing across the roundabout"),
                    Options.rabout_NoCrossingRAbout,
                    On_RAbout_NoCrossingRAbout_Changed) as UICheckBox;

            _rabout_NoCrossingAtConnectionsToggle = panelHelper.AddCheckbox(
                    Translation.Options.Get("MassEdit.Group.Roundabout: no pedasterians crossing at roads connected to round abouts"),
                    Options.rabout_NoCrossingAtConnections,
                    On_RAbout_NoCrossingAtConnections_Changed) as UICheckBox;

            UIHelperBase avnGroup = panelHelper.AddGroup(
                Translation.Options.Get("MassEdit.Group.Priority Priority roads)"));

            _avn_NoZebraCrossingAcrossAvnToggle = panelHelper.AddCheckbox(
                    Translation.Options.Get("MassEdit.Group.Priority: No pedasterian crossing across priority roads"),
                    Options.avn_NoZebraCrossingAcrossAvn,
                    On_Avn_NoZebraCrossingAcrossAvn_Changed) as UICheckBox;
        }

        public static void Set_RAbout_SwitchLanesAtEntry(bool newVal) {
            Options.prioritySignsOverlay = newVal;

            if (_rabout_SwitchLanesAtEntryToggle != null) {
                _rabout_SwitchLanesAtEntryToggle.isChecked = newVal;
            }
        }

        public static void Set_RAbout_NoLaneSwitchingInRabout(bool newVal) {
            Options.prioritySignsOverlay = newVal;

            if (_rabout_NoLaneSwitchingInRaboutToggle != null) {
                _rabout_NoLaneSwitchingInRaboutToggle.isChecked = newVal;
            }
        }

        public static void Set_RAbout_NoLaneSwitchingNearEntries(bool newVal) {
            Options.prioritySignsOverlay = newVal;

            if (_rabout_NoLaneSwitchingNearEntriesToggle != null) {
                _rabout_NoLaneSwitchingNearEntriesToggle.isChecked = newVal;
            }
        }

        public static void Set_RAbout_DecicatedExitLanes(bool newVal) {
            Options.prioritySignsOverlay = newVal;

            if (_rabout_DecicatedExitLanesToggle != null) {
                _rabout_DecicatedExitLanesToggle.isChecked = newVal;
            }
        }

        public static void Set_RAbout_NoCrossingRAbout(bool newVal) {
            Options.prioritySignsOverlay = newVal;

            if (_rabout_NoCrossingRAboutToggle != null) {
                _rabout_NoCrossingRAboutToggle.isChecked = newVal;
            }
        }

        public static void Set_RAbout_NoCrossingAtConnections(bool newVal) {
            Options.prioritySignsOverlay = newVal;

            if (_rabout_NoCrossingAtConnectionsToggle != null) {
                _rabout_NoCrossingAtConnectionsToggle.isChecked = newVal;
            }
        }

        public static void Set_Avn_NoZebraCrossingAcrossAvn(bool newVal) {
            Options.prioritySignsOverlay = newVal;

            if (_avn_NoZebraCrossingAcrossAvnToggle != null) {
                _avn_NoZebraCrossingAcrossAvnToggle.isChecked = newVal;
            }
        }

        private static void On_RAbout_SwitchLanesAtEntry_Changed(bool newVal) {
            if (!Options.IsGameLoaded()) {
                return;
            }

            Log._Debug($" changed to {newVal}");
            Options.rabout_SwitchLanesAtEntry = newVal;
        }

        private static void On_RAbout_NoLaneSwitchingInRabout_Changed(bool newVal) {
            if (!Options.IsGameLoaded()) {
                return;
            }

            Log._Debug($" changed to {newVal}");
            Options.rabout_NoLaneSwitchingInRabout = newVal;
        }

        private static void On_RAbout_NoLaneSwitchingNearEntries_Changed(bool newVal) {
            if (!Options.IsGameLoaded()) {
                return;
            }

            Log._Debug($" changed to {newVal}");
            Options.rabout_NoLaneSwitchingNearEntries = newVal;
        }

        private static void On_RAbout_DecicatedExitLanes_Changed(bool newVal) {
            if (!Options.IsGameLoaded()) {
                return;
            }

            Log._Debug($" changed to {newVal}");
            Options.rabout_DecicatedExitLanes = newVal;
        }

        private static void On_RAbout_NoCrossingRAbout_Changed(bool newVal) {
            if (!Options.IsGameLoaded()) {
                return;
            }

            Log._Debug($" changed to {newVal}");
            Options.rabout_NoCrossingRAbout = newVal;
        }

        private static void On_RAbout_NoCrossingAtConnections_Changed(bool newVal) {
            if (!Options.IsGameLoaded()) {
                return;
            }

            Log._Debug($" changed to {newVal}");
            Options.rabout_NoCrossingAtConnections = newVal;
        }

        private static void On_Avn_NoZebraCrossingAcrossAvn_Changed(bool newVal) {
            if (!Options.IsGameLoaded()) {
                return;
            }

            Log._Debug($" changed to {newVal}");
            Options.avn_NoZebraCrossingAcrossAvn = newVal;
        }

    } // end class
}
