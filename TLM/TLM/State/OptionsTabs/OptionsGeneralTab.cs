namespace TrafficManager.State {
    using System.Reflection;
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using ICities;
    using JetBrains.Annotations;
    using UI;
    using UI.SubTools.SpeedLimits;
    using UnityEngine;

    public static class OptionsGeneralTab {
        private static UICheckBox _instantEffectsToggle;

        [UsedImplicitly]
        private static UIDropDown _languageDropdown;

        [UsedImplicitly]
        private static UICheckBox _lockButtonToggle;

        [UsedImplicitly]
        private static UICheckBox _lockMenuToggle;

        [UsedImplicitly]
        private static UICheckBox _tinyMenuToggle;

        private static UISlider _guiTransparencySlider;
        private static UISlider _overlayTransparencySlider;

        [UsedImplicitly]
        private static UICheckBox _enableTutorialToggle;

        [UsedImplicitly]
        private static UICheckBox _showCompatibilityCheckErrorToggle;

        private static UICheckBox _scanForKnownIncompatibleModsToggle;
        private static UICheckBox _ignoreDisabledModsToggle;

        private static UICheckBox _displayMphToggle;
        private static UIDropDown _roadSignsMphThemeDropdown;
        private static int _roadSignMphStyleInt;

        private static string T(string s) {
            return Translation.GetString(s);
        }

        internal static void MakeSettings_General(UITabstrip tabStrip, int tabIndex) {
            Options.AddOptionTab(tabStrip, T("General"));
            tabStrip.selectedIndex = tabIndex;

            UIPanel currentPanel = tabStrip.tabContainer.components[tabIndex] as UIPanel;
            currentPanel.autoLayout = true;
            currentPanel.autoLayoutDirection = LayoutDirection.Vertical;
            currentPanel.autoLayoutPadding.top = 5;
            currentPanel.autoLayoutPadding.left = 10;
            currentPanel.autoLayoutPadding.right = 10;
            UIHelper panelHelper = new UIHelper(currentPanel);

            UIHelperBase generalGroup = panelHelper.AddGroup(T("General"));
            string[] languageLabels = new string[Translation.AvailableLanguageCodes.Count + 1];
            languageLabels[0] = T("Game_language");

            for (int i = 0; i < Translation.AvailableLanguageCodes.Count; ++i) {
                languageLabels[i + 1] =
                    Translation.LanguageLabels[Translation.AvailableLanguageCodes[i]];
            }

            int languageIndex = 0;
            string curLangCode = GlobalConfig.Instance.LanguageCode;

            if (curLangCode != null) {
                languageIndex = Translation.AvailableLanguageCodes.IndexOf(curLangCode);
                if (languageIndex < 0) {
                    languageIndex = 0;
                } else {
                    ++languageIndex;
                }
            }

            _languageDropdown = generalGroup.AddDropdown(
                                   T("Language") + ":",
                                   languageLabels,
                                   languageIndex,
                                   OnLanguageChanged) as UIDropDown;
            _lockButtonToggle = generalGroup.AddCheckbox(
                                   T("Lock_main_menu_button_position"),
                                   GlobalConfig.Instance.Main.MainMenuButtonPosLocked,
                                   OnLockButtonChanged) as UICheckBox;
            _lockMenuToggle = generalGroup.AddCheckbox(
                                 T("Lock_main_menu_position"),
                                 GlobalConfig.Instance.Main.MainMenuPosLocked,
                                 OnLockMenuChanged) as UICheckBox;
            _tinyMenuToggle = generalGroup.AddCheckbox(
                                 T("Compact_main_menu"),
                                 GlobalConfig.Instance.Main.TinyMainMenu,
                                 OnCompactMainMenuChanged) as UICheckBox;
            _guiTransparencySlider = generalGroup.AddSlider(
                                        T("Window_transparency") + ":",
                                        0,
                                        90,
                                        5,
                                        GlobalConfig.Instance.Main.GuiTransparency,
                                        OnGuiTransparencyChanged) as UISlider;
            _guiTransparencySlider.parent.Find<UILabel>("Label").width = 500;
            _overlayTransparencySlider = generalGroup.AddSlider(
                                            T("Overlay_transparency") + ":",
                                            0,
                                            90,
                                            5,
                                            GlobalConfig.Instance.Main.OverlayTransparency,
                                            OnOverlayTransparencyChanged) as UISlider;
            _overlayTransparencySlider.parent.Find<UILabel>("Label").width = 500;
            _enableTutorialToggle = generalGroup.AddCheckbox(
                                       T("Enable_tutorial_messages"),
                                       GlobalConfig.Instance.Main.EnableTutorial,
                                       OnEnableTutorialsChanged) as UICheckBox;
            _showCompatibilityCheckErrorToggle
                = generalGroup.AddCheckbox(
                      T("Notify_me_if_there_is_an_unexpected_mod_conflict"),
                      GlobalConfig.Instance.Main.ShowCompatibilityCheckErrorMessage,
                      OnShowCompatibilityCheckErrorChanged) as UICheckBox;
            _scanForKnownIncompatibleModsToggle
                = generalGroup.AddCheckbox(
                      T("Scan_for_known_incompatible_mods_on_startup"),
                      GlobalConfig.Instance.Main.ScanForKnownIncompatibleModsAtStartup,
                      OnScanForKnownIncompatibleModsChanged) as UICheckBox;
            _ignoreDisabledModsToggle = generalGroup.AddCheckbox(
                                           T("Ignore_disabled_mods"),
                                           GlobalConfig.Instance.Main.IgnoreDisabledMods,
                                           OnIgnoreDisabledModsChanged) as UICheckBox;
            Options.Indent(_ignoreDisabledModsToggle);

            // General: Speed Limits
            SetupSpeedLimitsPanel(generalGroup);

            // General: Simulation
            UIHelperBase simGroup = panelHelper.AddGroup(T("Simulation"));
            _instantEffectsToggle = simGroup.AddCheckbox(
                                       T("Customizations_come_into_effect_instantaneously"),
                                       Options.instantEffects,
                                       OnInstantEffectsChanged) as UICheckBox;
        }

        private static void SetupSpeedLimitsPanel(UIHelperBase generalGroup) {
            _displayMphToggle = generalGroup.AddCheckbox(
                                   T("Display_speed_limits_mph"),
                                   GlobalConfig.Instance.Main.DisplaySpeedLimitsMph,
                                   OnDisplayMphChanged) as UICheckBox;
            string[] mphThemeOptions = {
                T("theme_Square_US"),
                T("theme_Round_UK"),
                T("theme_Round_German")
            };
            _roadSignMphStyleInt = (int)GlobalConfig.Instance.Main.MphRoadSignStyle;
            _roadSignsMphThemeDropdown = generalGroup.AddDropdown(
                                            T("Road_signs_theme_mph") + ":",
                                            mphThemeOptions,
                                            _roadSignMphStyleInt,
                                            OnRoadSignsMphThemeChanged) as UIDropDown;
            _roadSignsMphThemeDropdown.width = 400;
        }

        private static void OnLanguageChanged(int newLanguageIndex) {
            if (newLanguageIndex <= 0) {
                GlobalConfig.Instance.LanguageCode = null;
                GlobalConfig.WriteConfig();
                Options.RebuildMenu();
            } else if (newLanguageIndex - 1 < Translation.AvailableLanguageCodes.Count) {
                string newLang = Translation.AvailableLanguageCodes[newLanguageIndex - 1];
                GlobalConfig.Instance.LanguageCode = newLang;
                GlobalConfig.WriteConfig();
                Options.RebuildMenu();
            } else {
                Log.Warning($"Options.onLanguageChanged: Invalid language index: {newLanguageIndex}");
                return;
            }

            Options.RebuildOptions();
        }

        private static void OnLockButtonChanged(bool newValue) {
            Log._Debug($"Button lock changed to {newValue}");
            if (Options.IsGameLoaded(false)) {
                LoadingExtension.BaseUI.MainMenuButton.SetPosLock(newValue);
            }

            GlobalConfig.Instance.Main.MainMenuButtonPosLocked = newValue;
            GlobalConfig.WriteConfig();
        }

        private static void OnLockMenuChanged(bool newValue) {
            Log._Debug($"Menu lock changed to {newValue}");
            if (Options.IsGameLoaded(false)) {
                LoadingExtension.BaseUI.MainMenu.SetPosLock(newValue);
            }

            GlobalConfig.Instance.Main.MainMenuPosLocked = newValue;
            GlobalConfig.WriteConfig();
        }

        private static void OnCompactMainMenuChanged(bool newValue) {
            Log._Debug($"Compact main menu changed to {newValue}");
            GlobalConfig.Instance.Main.TinyMainMenu = newValue;
            GlobalConfig.Instance.NotifyObservers(GlobalConfig.Instance);
            GlobalConfig.WriteConfig();
        }

        private static void OnEnableTutorialsChanged(bool newValue) {
            Log._Debug($"Enable tutorial messages changed to {newValue}");
            GlobalConfig.Instance.Main.EnableTutorial = newValue;
            GlobalConfig.WriteConfig();
        }

        private static void OnShowCompatibilityCheckErrorChanged(bool newValue) {
            Log._Debug($"Show mod compatibility error changed to {newValue}");
            GlobalConfig.Instance.Main.ShowCompatibilityCheckErrorMessage = newValue;
            GlobalConfig.WriteConfig();
        }

        private static void OnScanForKnownIncompatibleModsChanged(bool newValue) {
            Log._Debug($"Show incompatible mod checker warnings changed to {newValue}");
            GlobalConfig.Instance.Main.ScanForKnownIncompatibleModsAtStartup = newValue;
            if (newValue) {
                GlobalConfig.WriteConfig();
            } else {
                SetIgnoreDisabledMods(false);
                OnIgnoreDisabledModsChanged(false);
            }
        }

        private static void OnGuiTransparencyChanged(float newVal) {
            if (!Options.IsGameLoaded()) {
                return;
            }

            SetGuiTransparency((byte)Mathf.RoundToInt(newVal));
            _guiTransparencySlider.tooltip = T("Window_transparency") + ": " +
                                            GlobalConfig.Instance.Main.GuiTransparency + " %";

            GlobalConfig.WriteConfig();
            Log._Debug($"GuiTransparency changed to {GlobalConfig.Instance.Main.GuiTransparency}");
        }

        private static void OnOverlayTransparencyChanged(float newVal) {
            if (!Options.IsGameLoaded()) {
                return;
            }

            SetOverlayTransparency((byte)Mathf.RoundToInt(newVal));
            _overlayTransparencySlider.tooltip = T("Overlay_transparency") + ": " +
                                                GlobalConfig.Instance.Main.OverlayTransparency +
                                                " %";
            GlobalConfig.WriteConfig();
            Log._Debug($"OverlayTransparency changed to {GlobalConfig.Instance.Main.OverlayTransparency}");
        }

        private static void OnIgnoreDisabledModsChanged(bool newValue) {
            Log._Debug($"Ignore disabled mods changed to {newValue}");
            GlobalConfig.Instance.Main.IgnoreDisabledMods = newValue;
            GlobalConfig.WriteConfig();
        }

        private static void OnDisplayMphChanged(bool newValue) {
            Log._Debug($"Display MPH changed to {newValue}");
            GlobalConfig.Instance.Main.DisplaySpeedLimitsMph = newValue;
            GlobalConfig.WriteConfig();
        }

        public static void SetDisplayInMph(bool value) {
            if (_displayMphToggle != null) {
                _displayMphToggle.isChecked = value;
            }
        }

        private static void OnRoadSignsMphThemeChanged(int newRoadSignStyle) {
            if (!Options.IsGameLoaded()) {
                return;
            }

            // The UI order is: US, UK, German
            var newStyle = MphSignStyle.RoundGerman;
            switch (newRoadSignStyle) {
                case 1:
                    newStyle = MphSignStyle.RoundUK;
                    break;
                case 0:
                    newStyle = MphSignStyle.SquareUS;
                    break;
            }

            Log._Debug($"Road Sign theme changed to {newStyle}");
            GlobalConfig.Instance.Main.MphRoadSignStyle = newStyle;
        }

        private static void OnInstantEffectsChanged(bool newValue) {
            if (!Options.IsGameLoaded()) {
                return;
            }

            Log._Debug($"Instant effects changed to {newValue}");
            Options.instantEffects = newValue;
        }

        public static void SetIgnoreDisabledMods(bool value) {
            Options.ignoreDisabledModsEnabled = value;
            if (_ignoreDisabledModsToggle != null) {
                _ignoreDisabledModsToggle.isChecked = value;
            }
        }

        public static void SetGuiTransparency(byte val) {
            bool changed = val != GlobalConfig.Instance.Main.GuiTransparency;
            GlobalConfig.Instance.Main.GuiTransparency = val;

            if (changed && _guiTransparencySlider != null) {
                _guiTransparencySlider.value = val;
            }
        }

        public static void SetOverlayTransparency(byte val) {
            bool changed = val != GlobalConfig.Instance.Main.OverlayTransparency;
            GlobalConfig.Instance.Main.OverlayTransparency = val;

            if (changed && _overlayTransparencySlider != null) {
                _overlayTransparencySlider.value = val;
            }
        }

        public static void SetInstantEffects(bool value) {
            Options.instantEffects = value;

            if (_instantEffectsToggle != null) {
                _instantEffectsToggle.isChecked = value;
            }
        }

        public static void SetScanForKnownIncompatibleMods(bool value) {
            Options.scanForKnownIncompatibleModsEnabled = value;
            if (_scanForKnownIncompatibleModsToggle != null) {
                _scanForKnownIncompatibleModsToggle.isChecked = value;
            }

            if (!value) {
                SetIgnoreDisabledMods(false);
            }
        }

    } // end class
}