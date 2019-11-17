namespace TrafficManager.State {
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using ICities;
    using JetBrains.Annotations;
    using UI;
    using UI.SubTools.SpeedLimits;
    using UnityEngine;
    using TrafficManager.Manager.Impl;

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

        private static string T(string key) {
            return Translation.Options.Get(key);
        }

        internal static void MakeSettings_General(ExtUITabstrip tabStrip) {

            UIHelper panelHelper = tabStrip.AddTabPage(T("Tab:General"));

            UIHelperBase generalGroup = panelHelper.AddGroup(
                T("Tab:General"));
            string[] languageLabels = new string[Translation.AvailableLanguageCodes.Count + 1];
            languageLabels[0] = T("General.Dropdown.Option:Game language");

            for (int i = 0; i < Translation.AvailableLanguageCodes.Count; ++i) {
                languageLabels[i + 1] = Translation.Options.Get(
                    Translation.AvailableLanguageCodes[i], "General.Dropdown.Option:Language Name");
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
                                    T("General.Dropdown:Select language") + ":",
                                    languageLabels,
                                    languageIndex,
                                    OnLanguageChanged) as UIDropDown;
            _lockButtonToggle = generalGroup.AddCheckbox(
                                    T("General.Checkbox:Lock main menu button position"),
                                    GlobalConfig.Instance.Main.MainMenuButtonPosLocked,
                                    OnLockButtonChanged) as UICheckBox;
            _lockMenuToggle = generalGroup.AddCheckbox(
                                  T("General.Checkbox:Lock main menu window position"),
                                  GlobalConfig.Instance.Main.MainMenuPosLocked,
                                  OnLockMenuChanged) as UICheckBox;
            _tinyMenuToggle = generalGroup.AddCheckbox(
                                  T("General.Checkbox:Compact main menu"),
                                  GlobalConfig.Instance.Main.TinyMainMenu,
                                  OnCompactMainMenuChanged) as UICheckBox;
            _guiTransparencySlider = generalGroup.AddSlider(
                                        T("General.Slider:Window transparency") + ":",
                                        0,
                                        90,
                                        5,
                                        GlobalConfig.Instance.Main.GuiTransparency,
                                        OnGuiTransparencyChanged) as UISlider;
            _guiTransparencySlider.parent.Find<UILabel>("Label").width = 500;
            _overlayTransparencySlider = generalGroup.AddSlider(
                                             T("General.Slider:Overlay transparency") + ":",
                                            0,
                                            90,
                                            5,
                                            GlobalConfig.Instance.Main.OverlayTransparency,
                                            OnOverlayTransparencyChanged) as UISlider;
            _overlayTransparencySlider.parent.Find<UILabel>("Label").width = 500;
            _enableTutorialToggle = generalGroup.AddCheckbox(
                                        T("General.Checkbox:Enable tutorials"),
                                        GlobalConfig.Instance.Main.EnableTutorial,
                                        OnEnableTutorialsChanged) as UICheckBox;
            _showCompatibilityCheckErrorToggle
                = generalGroup.AddCheckbox(
                      T("General.Checkbox:Notify me about TM:PE startup conflicts"),
                      GlobalConfig.Instance.Main.ShowCompatibilityCheckErrorMessage,
                      OnShowCompatibilityCheckErrorChanged) as UICheckBox;
            _scanForKnownIncompatibleModsToggle
                = generalGroup.AddCheckbox(
                      Translation.ModConflicts.Get("Checkbox:Scan for known incompatible mods on startup"),
                      GlobalConfig.Instance.Main.ScanForKnownIncompatibleModsAtStartup,
                      OnScanForKnownIncompatibleModsChanged) as UICheckBox;
            _ignoreDisabledModsToggle = generalGroup.AddCheckbox(
                                            Translation.ModConflicts.Get("Checkbox:Ignore disabled mods"),
                                            GlobalConfig.Instance.Main.IgnoreDisabledMods,
                                            OnIgnoreDisabledModsChanged) as UICheckBox;
            Options.Indent(_ignoreDisabledModsToggle);

            // General: Speed Limits
            SetupSpeedLimitsPanel(generalGroup);

            // General: Simulation
            UIHelperBase simGroup = panelHelper.AddGroup(T("General.Group:Simulation"));
            _instantEffectsToggle = simGroup.AddCheckbox(
                                       T("General.Checkbox:Apply AI changes right away"),
                                       Options.instantEffects,
                                       OnInstantEffectsChanged) as UICheckBox;
        }

        private static void SetupSpeedLimitsPanel(UIHelperBase generalGroup) {
            _displayMphToggle = generalGroup.AddCheckbox(
                                    Translation.SpeedLimits.Get("Checkbox:Display speed limits mph"),
                                    GlobalConfig.Instance.Main.DisplaySpeedLimitsMph,
                                    OnDisplayMphChanged) as UICheckBox;
            string[] mphThemeOptions = {
                Translation.SpeedLimits.Get("General.Theme.Option:Square US"),
                Translation.SpeedLimits.Get("General.Theme.Option:Round UK"),
                Translation.SpeedLimits.Get("General.Theme.Option:Round German")
            };
            _roadSignMphStyleInt = (int)GlobalConfig.Instance.Main.MphRoadSignStyle;
            _roadSignsMphThemeDropdown
                = generalGroup.AddDropdown(
                      Translation.SpeedLimits.Get("General.Dropdown:Road signs theme mph") + ":",
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
            _guiTransparencySlider.tooltip
                = string.Format(
                    T("General.Tooltip.Format:Window transparency: {0}%"),
                    GlobalConfig.Instance.Main.GuiTransparency);

            GlobalConfig.WriteConfig();
            Log._Debug($"GuiTransparency changed to {GlobalConfig.Instance.Main.GuiTransparency}");
        }

        private static void OnOverlayTransparencyChanged(float newVal) {
            if (!Options.IsGameLoaded()) {
                return;
            }

            SetOverlayTransparency((byte)Mathf.RoundToInt(newVal));
            _overlayTransparencySlider.tooltip = string.Format(
                T("General.Tooltip.Format:Overlay transparency: {0}%"),
                GlobalConfig.Instance.Main.OverlayTransparency);
            GlobalConfig.WriteConfig();
            Log._Debug($"Overlay transparency changed to {GlobalConfig.Instance.Main.OverlayTransparency}");
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
