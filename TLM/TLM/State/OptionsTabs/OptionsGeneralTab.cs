namespace TrafficManager.State {
    using System;
    using System.Linq;
    using TrafficManager.API.Traffic.Enums;
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using ICities;
    using JetBrains.Annotations;
    using TrafficManager.U;
    using TrafficManager.UI.Helpers;
    using TrafficManager.UI.SubTools.SpeedLimits;
    using TrafficManager.UI;
    using UnityEngine;
    using TrafficManager.Lifecycle;
    using TrafficManager.State.ConfigData;
    using TrafficManager.UI.Textures;

    public static class OptionsGeneralTab {
        private static UICheckBox _instantEffectsToggle;

        [UsedImplicitly]
        private static UIDropDown _languageDropdown;
        private static UIDropDown _simulationAccuracyDropdown;

        [UsedImplicitly]
        private static UICheckBox _lockButtonToggle;

        [UsedImplicitly]
        private static UICheckBox _lockMenuToggle;

        private static UISlider _guiOpacitySlider;
        private static UISlider _guiScaleSlider;
        private static UISlider _overlayTransparencySlider;

        [UsedImplicitly]
        private static UICheckBox _enableTutorialToggle;

        [UsedImplicitly]
        private static UICheckBox _showCompatibilityCheckErrorToggle;

        private static UICheckBox _scanForKnownIncompatibleModsToggle;
        private static UICheckBox _ignoreDisabledModsToggle;

        private static UICheckBox _displayMphToggle;

        private static UIDropDown _roadSignsThemeDropdown;

        private static UICheckBox _useUUI;


        private static string T(string key) => Translation.Options.Get(key);

        internal static void MakeSettings_General(ExtUITabstrip tabStrip) {
            UIHelper panelHelper = tabStrip.AddTabPage(T("Tab:General"));

            UIHelperBase generalGroup = panelHelper.AddGroup(T("Tab:General"));
            string[] languageLabels = new string[Translation.AvailableLanguageCodes.Count + 1];
            languageLabels[0] = T("General.Dropdown.Option:Game language");

            for (int i = 0; i < Translation.AvailableLanguageCodes.Count; ++i) {
                languageLabels[i + 1] = Translation.Options.Get(
                    lang: Translation.AvailableLanguageCodes[i],
                    key: "General.Dropdown.Option:Language Name");
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
                                    text: T("General.Dropdown:Select language") + ":",
                                    options: languageLabels,
                                    defaultSelection: languageIndex,
                                    eventCallback: OnLanguageChanged) as UIDropDown;
            _lockButtonToggle = generalGroup.AddCheckbox(
                                    text: T("General.Checkbox:Lock main menu button position"),
                                    defaultValue: GlobalConfig.Instance.Main.MainMenuButtonPosLocked,
                                    eventCallback: OnLockButtonChanged) as UICheckBox;
            _lockMenuToggle = generalGroup.AddCheckbox(
                                  text: T("General.Checkbox:Lock main menu window position"),
                                  defaultValue: GlobalConfig.Instance.Main.MainMenuPosLocked,
                                  eventCallback: OnLockMenuChanged) as UICheckBox;

            _useUUI = generalGroup.AddCheckbox(
                text: T("General.Checkbox:Use UnifiedUI"),
                defaultValue: GlobalConfig.Instance.Main.UseUUI,
                eventCallback: OnUseUUIChanged) as UICheckBox;

            _guiScaleSlider = generalGroup.AddSlider(
                                  text: T("General.Slider:GUI scale") + ":",
                                  min: 50,
                                  max: 200,
                                  step: 5,
                                  defaultValue: GlobalConfig.Instance.Main.GuiScale,
                                  eventCallback: OnGuiScaleChanged) as UISlider;
            _guiScaleSlider.parent.Find<UILabel>("Label").width = 500;

            _guiOpacitySlider = generalGroup.AddSlider(
                                        text: T("General.Slider:Window transparency") + ":",
                                        min: 0,
                                        max: 100,
                                        step: 5,
                                        defaultValue: GlobalConfig.Instance.Main.GuiOpacity,
                                        eventCallback: OnGuiOpacityChanged) as UISlider;
            _guiOpacitySlider.parent.Find<UILabel>("Label").width = 500;

            _overlayTransparencySlider = generalGroup.AddSlider(
                                             text: T("General.Slider:Overlay transparency") + ":",
                                             min: 0,
                                             max: 100,
                                             step: 5,
                                             defaultValue: GlobalConfig.Instance.Main.OverlayTransparency,
                                             eventCallback: OnOverlayTransparencyChanged) as UISlider;
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
                                            text: Translation.ModConflicts.Get("Checkbox:Ignore disabled mods"),
                                            defaultValue: GlobalConfig.Instance.Main.IgnoreDisabledMods,
                                            eventCallback: OnIgnoreDisabledModsChanged) as UICheckBox;
            Options.Indent(_ignoreDisabledModsToggle);

            // General: Speed Limits
            SetupSpeedLimitsPanel(generalGroup);

            // General: Simulation
            UIHelperBase simGroup = panelHelper.AddGroup(T("General.Group:Simulation"));
            string[] simPrecisionOptions = new[] {
                T("General.Dropdown.Option:Very low"),
                T("General.Dropdown.Option:Low"),
                T("General.Dropdown.Option:Medium"),
                T("General.Dropdown.Option:High"),
                T("General.Dropdown.Option:Very high"),
            };
            _simulationAccuracyDropdown = simGroup.AddDropdown(
                                              text: T("General.Dropdown:Simulation accuracy") + ":",
                                              options: simPrecisionOptions,
                                              defaultSelection: (int)Options.simulationAccuracy,
                                              eventCallback: OnSimulationAccuracyChanged) as UIDropDown;

            _instantEffectsToggle = simGroup.AddCheckbox(
                                       text: T("General.Checkbox:Apply AI changes right away"),
                                       defaultValue: Options.instantEffects,
                                       eventCallback: OnInstantEffectsChanged) as UICheckBox;
        }

        private static void SetupSpeedLimitsPanel(UIHelperBase generalGroup) {
            Main mainConfig = GlobalConfig.Instance.Main;

            _displayMphToggle = generalGroup.AddCheckbox(
                                    text: Translation.SpeedLimits.Get("Checkbox:Display speed limits mph"),
                                    defaultValue: mainConfig.DisplaySpeedLimitsMph,
                                    eventCallback: OnDisplayMphChanged) as UICheckBox;

            string FormatThemeName(string themeName) {
                return Translation.SpeedLimits.Get($"RoadSignTheme:{themeName}");
            }

            var themeOptions = SpeedLimitTextures.ThemeNames
                                                    .Select(FormatThemeName)
                                                    .ToArray();
            int selectedThemeIndex = SpeedLimitTextures.ThemeNames.FindIndex(x => x == mainConfig.RoadSignTheme);
            int defaultGermanSignsThemeIndex = SpeedLimitTextures.ThemeNames.FindIndex(x => x == SpeedLimitTextures.GERMAN_KM_SIGNS);
            _roadSignsThemeDropdown
                = generalGroup.AddDropdown(
                      text: Translation.SpeedLimits.Get("General.Dropdown:Road signs theme") + ":",
                      options: themeOptions,
                      defaultSelection: selectedThemeIndex >= 0 ? selectedThemeIndex : defaultGermanSignsThemeIndex,
                      eventCallback: OnRoadSignsThemeChanged) as UIDropDown;
            _roadSignsThemeDropdown.width *= 2.0f;
        }

        private static void OnLanguageChanged(int newLanguageIndex) {
            if (newLanguageIndex <= 0) {
                GlobalConfig.Instance.LanguageCode = null;
                GlobalConfig.WriteConfig();

                // TODO: Move this to the owner class and implement IObserver<ModUI.EventPublishers.LanguageChangeNotification>
                Translation.SetCurrentLanguageToGameLanguage();
                Options.RebuildMenu();
            } else if (newLanguageIndex - 1 < Translation.AvailableLanguageCodes.Count) {
                string newLang = Translation.AvailableLanguageCodes[newLanguageIndex - 1];
                GlobalConfig.Instance.LanguageCode = newLang;
                GlobalConfig.WriteConfig();
                // TODO: Move this to the owner class and implement IObserver<ModUI.EventPublishers.LanguageChangeNotification>
                Translation.SetCurrentLanguageToTMPELanguage();
                Options.RebuildMenu();
            } else {
                Log.Warning($"Options.onLanguageChanged: Invalid language index: {newLanguageIndex}");
                return;
            }

            // Events will be null when mod is not fully loaded and language changed in main menu
            ModUI.Instance.Events?.LanguageChanged();
            Options.RebuildOptions();
        }

        private static void OnLockButtonChanged(bool newValue) {
            Log._Debug($"Button lock changed to {newValue}");
            if (Options.IsGameLoaded(false)) {
                ModUI.Instance.MainMenuButton.SetPosLock(newValue);
            }

            GlobalConfig.Instance.Main.MainMenuButtonPosLocked = newValue;
            GlobalConfig.WriteConfig();
        }

        private static void OnLockMenuChanged(bool newValue) {
            Log._Debug($"Menu lock changed to {newValue}");
            if (Options.IsGameLoaded(false)) {
                ModUI.Instance.MainMenu.SetPosLock(newValue);
            }

            GlobalConfig.Instance.Main.MainMenuPosLocked = newValue;
            GlobalConfig.WriteConfig();
        }

        private static void OnUseUUIChanged(bool newValue) {
            Log._Debug($"Use UUI set to {newValue}");
            GlobalConfig.Instance.Main.UseUUI = newValue;
            GlobalConfig.WriteConfig();
            var button = ModUI.GetTrafficManagerTool()?.UUIButton;
            if (button) {
                button.isVisible = newValue;
            }
            ModUI.Instance?.MainMenuButton?.UpdateButtonSkinAndTooltip();
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

        private static void OnGuiOpacityChanged(float newVal) {
            if (!Options.IsGameLoaded()) {
                return;
            }

            SetGuiTransparency((byte)Mathf.RoundToInt(newVal));
            _guiOpacitySlider.tooltip
                = string.Format(
                    T("General.Tooltip.Format:Window transparency: {0}%"),
                    GlobalConfig.Instance.Main.GuiOpacity);
            if (TMPELifecycle.Instance.IsGameLoaded) {
                _guiOpacitySlider.RefreshTooltip();
            }

            GlobalConfig.WriteConfig();
            Log._Debug($"GuiTransparency changed to {GlobalConfig.Instance.Main.GuiOpacity}");
        }

        private static void OnGuiScaleChanged(float newVal) {
            ModUI.Instance.Events.UiScaleChanged();
            SetGuiScale(newVal);
            _guiScaleSlider.tooltip
                = string.Format(
                    T("General.Tooltip.Format:GUI scale: {0}%"),
                    GlobalConfig.Instance.Main.GuiScale);
            if (TMPELifecycle.Instance.IsGameLoaded) {
                _guiScaleSlider.RefreshTooltip();
            }

            GlobalConfig.WriteConfig();
            Log._Debug($"GuiScale changed to {GlobalConfig.Instance.Main.GuiScale}");
        }

        /// <summary>User clicked [scale GUI to screen resolution] checkbox.</summary>
        private static void OnGuiScaleToResChanged(float newVal) {
            SetGuiScale(newVal);
            if (TMPELifecycle.Instance.IsGameLoaded) {
                _guiScaleSlider.RefreshTooltip();
            }

            GlobalConfig.WriteConfig();
            Log._Debug($"GuiScale changed to {GlobalConfig.Instance.Main.GuiScale}");
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
            if (TMPELifecycle.Instance.IsGameLoaded) {
                _overlayTransparencySlider.RefreshTooltip();
            }

            Log._Debug($"Overlay transparency changed to {GlobalConfig.Instance.Main.OverlayTransparency}");
        }

        private static void OnIgnoreDisabledModsChanged(bool newValue) {
            Log._Debug($"Ignore disabled mods changed to {newValue}");
            GlobalConfig.Instance.Main.IgnoreDisabledMods = newValue;
            GlobalConfig.WriteConfig();
        }

        private static void OnDisplayMphChanged(bool newValue) {
            bool supportedByTheme = newValue
                                        ? SpeedLimitTextures.ActiveTheme.SupportsMph
                                        : SpeedLimitTextures.ActiveTheme.SupportsKmph;
            Main mainConfig = GlobalConfig.Instance.Main;

            if (!supportedByTheme) {
                // Reset to German road signs theme
                _roadSignsThemeDropdown.selectedIndex = SpeedLimitTextures.ThemeNames.FindIndex(
                    x => x == SpeedLimitTextures.GERMAN_KM_SIGNS);
                mainConfig.RoadSignTheme = SpeedLimitTextures.GERMAN_KM_SIGNS;
                Log.Info(
                    $"Display MPH changed to {newValue}, but was not supported by current theme, "
                    + "so theme was also reset to German_Kmph");
            } else {
                Log.Info($"Display MPH changed to {newValue}");
            }

            mainConfig.DisplaySpeedLimitsMph = newValue;

            GlobalConfig.WriteConfig();

            ModUI.Instance.Events.DisplayMphChanged(newValue);
        }

        public static void SetDisplayInMph(bool value) {
            if (_displayMphToggle != null) {
                _displayMphToggle.isChecked = value;
            }
        }

        private static void OnRoadSignsThemeChanged(int newThemeIndex) {
            if (!Options.IsGameLoaded()) {
                return;
            }

            var newTheme = SpeedLimitTextures.ThemeNames[newThemeIndex];

            Main mainConfig = GlobalConfig.Instance.Main;
            if (SpeedLimitTextures.OnThemeChanged(
                newTheme: newTheme,
                mphEnabled: mainConfig.DisplaySpeedLimitsMph))
            {
                Log.Info($"Road Sign theme changed to {newTheme}");
                mainConfig.RoadSignTheme = newTheme;
            } else {
                Log.Info(
                    $"Road Sign theme was not changed to {newTheme} (doesn't support MPH={mainConfig.DisplaySpeedLimitsMph}). "
                    + "Toggling MPH and trying once again.");

                bool invertedMph = !mainConfig.DisplaySpeedLimitsMph;
                if (SpeedLimitTextures.OnThemeChanged(
                    newTheme: newTheme,
                    mphEnabled: invertedMph))
                {
                    Log.Info($"Road Sign theme changed to {newTheme}. ShowMPH config value is now {invertedMph}.");
                    _displayMphToggle.isChecked = invertedMph;
                    mainConfig.DisplaySpeedLimitsMph = invertedMph;
                    mainConfig.RoadSignTheme = newTheme;
                }
            }

            GlobalConfig.WriteConfig();
        }

        private static void OnSimulationAccuracyChanged(int newAccuracy) {
            if (!Options.IsGameLoaded()) {
                return;
            }

            Log._Debug($"Simulation accuracy changed to {newAccuracy}");
            Options.simulationAccuracy = (SimulationAccuracy)newAccuracy;
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
            bool isChanged = val != GlobalConfig.Instance.Main.GuiOpacity;
            GlobalConfig.Instance.Main.GuiOpacity = val;

            if (isChanged && _guiOpacitySlider != null) {
                _guiOpacitySlider.value = val;

                U.UOpacityValue opacity = UOpacityValue.FromOpacity(0.01f * val);
                ModUI.Instance.Events.OpacityChanged(opacity);
            }
        }

        public static void SetGuiScale(float val) {
            bool changed = (int)val != (int)GlobalConfig.Instance.Main.GuiScale;
            GlobalConfig.Instance.Main.GuiScale = val;

            if (changed && _guiScaleSlider != null) {
                _guiScaleSlider.value = val;
                ModUI.Instance.Events.UiScaleChanged();
            }
        }

        public static void SetOverlayTransparency(byte val) {
            bool changed = val != GlobalConfig.Instance.Main.OverlayTransparency;
            GlobalConfig.Instance.Main.OverlayTransparency = val;

            if (changed && _overlayTransparencySlider != null) {
                _overlayTransparencySlider.value = val;
            }
        }

        public static void SetSimulationAccuracy(SimulationAccuracy newAccuracy) {
            Options.simulationAccuracy = newAccuracy;
            if (_simulationAccuracyDropdown != null) {
                _simulationAccuracyDropdown.selectedIndex = (int)newAccuracy;
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
