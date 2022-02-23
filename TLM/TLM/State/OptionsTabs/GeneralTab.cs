namespace TrafficManager.State {
    using System.Collections.Generic;
    using System.Linq;
    using TrafficManager.API.Traffic.Enums;
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using ICities;
    using JetBrains.Annotations;
    using TrafficManager.U;
    using TrafficManager.UI.Helpers;
    using TrafficManager.UI;
    using UnityEngine;
    using TrafficManager.Lifecycle;
    using TrafficManager.State.ConfigData;
    using TrafficManager.UI.Textures;
    using UI.WhatsNew;

    public static class GeneralTab {
        public static ActionButton WhatsNewButton = new() {
            Label = "What's New?",
            Handler = WhatsNew.OpenModal,
        };

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
            UIHelper tab = tabStrip.AddTabPage(T("Tab:General"));
            UIHelperBase group;

#if DEBUG
            GeneralTab_DebugGroup.AddUI(tab);
#endif

            tab.AddSpace(5);
            WhatsNewButton.AddUI(tab);
            tab.AddSpace(5);

            group = tab.AddGroup(T("General.Group:Localisation"));

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

            _languageDropdown = group.AddDropdown(
                                    text: T("General.Dropdown:Select language") + ":",
                                    options: languageLabels,
                                    defaultSelection: languageIndex,
                                    eventCallback: OnLanguageChanged) as UIDropDown;

            SetupSpeedLimitsPanel(group);

            group = tab.AddGroup(T("General.Group:Simulation"));

            string[] simPrecisionOptions = new[] {
                T("General.Dropdown.Option:Very low"),
                T("General.Dropdown.Option:Low"),
                T("General.Dropdown.Option:Medium"),
                T("General.Dropdown.Option:High"),
                T("General.Dropdown.Option:Very high"),
            };
            _simulationAccuracyDropdown = group.AddDropdown(
                                              text: T("General.Dropdown:Simulation accuracy") + ":",
                                              options: simPrecisionOptions,
                                              defaultSelection: (int)Options.simulationAccuracy,
                                              eventCallback: OnSimulationAccuracyChanged) as UIDropDown;

            _instantEffectsToggle = group.AddCheckbox(
                                       text: T("General.Checkbox:Apply AI changes right away"),
                                       defaultValue: Options.instantEffects,
                                       eventCallback: OnInstantEffectsChanged) as UICheckBox;

            GeneralTab_InterfaceGroup.AddUI(tab);

            group = tab.AddGroup(T("General.Group:Compatibility"));

            _scanForKnownIncompatibleModsToggle
                = group.AddCheckbox(
                      Translation.ModConflicts.Get("Checkbox:Scan for known incompatible mods on startup"),
                      GlobalConfig.Instance.Main.ScanForKnownIncompatibleModsAtStartup,
                      OnScanForKnownIncompatibleModsChanged) as UICheckBox;
            _ignoreDisabledModsToggle = group.AddCheckbox(
                                            text: Translation.ModConflicts.Get("Checkbox:Ignore disabled mods"),
                                            defaultValue: GlobalConfig.Instance.Main.IgnoreDisabledMods,
                                            eventCallback: OnIgnoreDisabledModsChanged) as UICheckBox;
            CheckboxOption.ApplyIndent(_ignoreDisabledModsToggle);
            _showCompatibilityCheckErrorToggle
                = group.AddCheckbox(
                      T("General.Checkbox:Notify me about TM:PE startup conflicts"),
                      GlobalConfig.Instance.Main.ShowCompatibilityCheckErrorMessage,
                      OnShowCompatibilityCheckErrorChanged) as UICheckBox;
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

            List<string> themeNames = RoadSignThemes.Instance.ThemeNames;
            var themeOptions = themeNames.Select(FormatThemeName).ToArray();
            int selectedThemeIndex = themeNames.FindIndex(x => x == mainConfig.RoadSignTheme);
            int defaultSignsThemeIndex = RoadSignThemes.Instance.FindDefaultThemeIndex(GlobalConfig.Instance.Main.DisplaySpeedLimitsMph);

            _roadSignsThemeDropdown
                = generalGroup.AddDropdown(
                      text: Translation.SpeedLimits.Get("General.Dropdown:Road signs theme") + ":",
                      options: themeOptions,
                      defaultSelection: selectedThemeIndex >= 0 ? selectedThemeIndex : defaultSignsThemeIndex,
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

            if (Options.IsGameLoaded(false)) {
                // Events will be null when mod is not fully loaded and language changed in main menu
                ModUI.Instance.Events.LanguageChanged();
            }
            Options.RebuildOptions();
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

        private static void OnIgnoreDisabledModsChanged(bool newValue) {
            Log._Debug($"Ignore disabled mods changed to {newValue}");
            GlobalConfig.Instance.Main.IgnoreDisabledMods = newValue;
            GlobalConfig.WriteConfig();
        }

        private static void OnDisplayMphChanged(bool newMphValue) {
            bool supportedByTheme = newMphValue
                                        ? RoadSignThemes.ActiveTheme.SupportsMph
                                        : RoadSignThemes.ActiveTheme.SupportsKmph;
            Main mainConfig = GlobalConfig.Instance.Main;

            if (!supportedByTheme) {
                // Reset to German road signs theme
                _roadSignsThemeDropdown.selectedIndex = RoadSignThemes.Instance.FindDefaultThemeIndex(newMphValue);
                mainConfig.RoadSignTheme = RoadSignThemes.Instance.GetDefaultThemeName(newMphValue);
                Log.Info(
                    $"Display MPH changed to {newMphValue}, but was not supported by current theme, "
                    + "so theme was also reset to German_Kmph");
            } else {
                Log.Info($"Display MPH changed to {newMphValue}");
            }

            mainConfig.DisplaySpeedLimitsMph = newMphValue;

            GlobalConfig.WriteConfig();

            if (Options.IsGameLoaded(false)) {
                ModUI.Instance.Events.DisplayMphChanged(newMphValue);
            }
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

            var newTheme = RoadSignThemes.Instance.ThemeNames[newThemeIndex];

            Main mainConfig = GlobalConfig.Instance.Main;
            switch (RoadSignThemes.Instance.ChangeTheme(
                        newTheme: newTheme,
                        mphEnabled: mainConfig.DisplaySpeedLimitsMph)) {
                case RoadSignThemes.ChangeThemeResult.Success:
                    Log.Info($"Road Sign theme changed to {newTheme}");
                    mainConfig.RoadSignTheme = newTheme;
                    break;
                case RoadSignThemes.ChangeThemeResult.ForceKmph:
                    mainConfig.DisplaySpeedLimitsMph = false;
                    _displayMphToggle.isChecked = false;

                    Log.Info($"Road Sign theme was changed to {newTheme} AND display switched to km/h");

                    if (Options.IsGameLoaded(false)) {
                        ModUI.Instance.Events.DisplayMphChanged(false);
                    }
                    break;
                case RoadSignThemes.ChangeThemeResult.ForceMph:
                    mainConfig.DisplaySpeedLimitsMph = true;
                    _displayMphToggle.isChecked = true;

                    Log.Info($"Road Sign theme was changed to {newTheme} AND display switched to MPH");

                    if (Options.IsGameLoaded(false)) {
                        ModUI.Instance.Events.DisplayMphChanged(true);
                    }
                    break;
            }

            mainConfig.RoadSignTheme = newTheme;
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
