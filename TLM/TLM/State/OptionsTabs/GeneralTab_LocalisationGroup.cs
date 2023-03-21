namespace TrafficManager.State {
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using ICities;
    using System.Collections.Generic;
    using System.Linq;
    using TrafficManager.Lifecycle;
    using TrafficManager.Manager.Impl;
    using TrafficManager.State.ConfigData;
    using TrafficManager.UI;
    using TrafficManager.UI.Helpers;
    using TrafficManager.UI.Textures;
    using TrafficManager.Util.Extensions;

    /// <summary>
    /// Adds localisation options to General options tab.
    /// </summary>
    public static class GeneralTab_LocalisationGroup {

        // TODO: Implement global config updates direct from CheckboxOption
        public static CheckboxOption DisplaySpeedLimitsMph =
            new (nameof(GlobalConfig.Instance.Main.DisplaySpeedLimitsMph), Scope.Global) {
                Label = "Checkbox:Display speed limits mph",
                Translator = TSpeedLimits,
                Handler = OnDisplaySpeedLimitsMphChanged,
            };

        private static UIDropDown _roadSignsThemeDropdown;

        internal static void AddUI(UIHelperBase tab) {

            var group = tab.AddGroup(T("General.Group:Localisation"));

            AddLanguageDropDown(group);

            DisplaySpeedLimitsMph.AddUI(group)
                .Value = GlobalConfig.Instance.Main.DisplaySpeedLimitsMph;

            AddRoadSignThemeDropDown(group);
        }

        private static string T(string key) => Translation.Options.Get(key);

        private static string TLang(string key, int langNum) =>
            Translation.Options.Get(lang: Translation.AvailableLanguageCodes[langNum], key: key);

        // TODO: Create a UI component helper for drop-down lists
        private static void AddLanguageDropDown(UIHelperBase group) {
            string[] languageLabels = new string[Translation.AvailableLanguageCodes.Count + 1];
            languageLabels[0] = T("General.Dropdown.Option:Game language");

            for (int i = 0; i < Translation.AvailableLanguageCodes.Count; ++i) {
                languageLabels[i + 1] = TLang("General.Dropdown.Option:Language Name", i);
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

            group.AddCustomDropDown(
                text: T("General.Dropdown:Select language") + ":",
                options: languageLabels,
                defaultSelection: languageIndex,
                eventCallback: OnLanguageChanged);
        }

        private static string TSpeedLimits(string key) => Translation.SpeedLimits.Get(key);

        private static string TranslateThemeName(string themeName) => TSpeedLimits($"RoadSignTheme:{themeName}");

        // TODO: Create a UI component helper for drop-down lists
        private static void AddRoadSignThemeDropDown(UIHelperBase group) {
            Main mainConfig = GlobalConfig.Instance.Main;

            List<string> themeNames = RoadSignThemeManager.Instance.ThemeNames;
            var themeOptions = themeNames.Select(TranslateThemeName).ToArray();
            int selectedThemeIndex = themeNames.FindIndex(x => x == mainConfig.RoadSignTheme);
            int defaultSignsThemeIndex = RoadSignThemeManager.Instance.FindDefaultThemeIndex(mainConfig.DisplaySpeedLimitsMph);

            _roadSignsThemeDropdown = group.AddCustomDropDown(
                text: TSpeedLimits("General.Dropdown:Road signs theme") + ":",
                options: themeOptions,
                defaultSelection: selectedThemeIndex >= 0 ? selectedThemeIndex : defaultSignsThemeIndex,
                eventCallback: OnRoadSignsThemeChanged) as UIDropDown;

            _roadSignsThemeDropdown.width *= 2.0f;
        }

        private static void OnLanguageChanged(int newLanguageIndex) {
            if (newLanguageIndex <= 0) {
                // use game language
                GlobalConfig.Instance.LanguageCode = null;
                GlobalConfig.WriteConfig();

                // TODO: Move this to the owner class and implement IObserver<ModUI.EventPublishers.LanguageChangeNotification>
                Translation.SetCurrentLanguageToGameLanguage();
                OptionsManager.RebuildMenu(true);
            } else if (newLanguageIndex - 1 < Translation.AvailableLanguageCodes.Count) {
                // use tmpe language
                string newLang = Translation.AvailableLanguageCodes[newLanguageIndex - 1];
                GlobalConfig.Instance.LanguageCode = newLang;
                GlobalConfig.WriteConfig();

                // TODO: Move this to the owner class and implement IObserver<ModUI.EventPublishers.LanguageChangeNotification>
                Translation.SetCurrentLanguageToTMPELanguage();
                OptionsManager.RebuildMenu(true);
            } else {
                Log.Warning($"GeneralTab.LocalisationGroup.onLanguageChanged({newLanguageIndex}): Invalid language index");
                return;
            }

            if (TMPELifecycle.InGameOrEditor()) {
                // Events will be null when mod is not fully loaded and language changed in main menu
                ModUI.Instance.Events.LanguageChanged();
            }

            TMPESettings.RebuildOptions();
        }

        private static void OnDisplaySpeedLimitsMphChanged(bool value) {
            bool supportedByTheme = value
                                        ? RoadSignThemeManager.ActiveTheme.SupportsMph
                                        : RoadSignThemeManager.ActiveTheme.SupportsKmph;

            Main mainConfig = GlobalConfig.Instance.Main;

            if (!supportedByTheme) {
                // Reset to German road signs theme
                _roadSignsThemeDropdown.selectedIndex = RoadSignThemeManager.Instance.FindDefaultThemeIndex(value);
                mainConfig.RoadSignTheme = RoadSignThemeManager.Instance.GetDefaultThemeName(value);
                Log.Info(
                    $"Display MPH changed to {value}, but was not supported by current theme, "
                    + $"so theme was also reset to {mainConfig.RoadSignTheme}");
            } else {
                Log.Info($"Display MPH changed to {value}");
            }

            mainConfig.DisplaySpeedLimitsMph = value;
            GlobalConfig.WriteConfig();

            if (TMPESettings.IsGameLoaded(false)) {
                ModUI.Instance.Events.DisplayMphChanged(value);
            }
        }

        private static void OnRoadSignsThemeChanged(int newThemeIndex) {
            if (!TMPESettings.IsGameLoaded(false)) {
                // update global config only, skip reloading theme
                var selectedTheme = RoadSignThemeManager.Instance.ThemeNames[newThemeIndex];
                // TODO: Should be simplified / ChangeTheme support change in main menu (without reloading)
                if (RoadSignThemeManager.Instance.Themes.TryGetValue(selectedTheme, out RoadSignTheme theme)) {
                    Main config = GlobalConfig.Instance.Main;
                    if (config.DisplaySpeedLimitsMph && !theme.SupportsMph) {
                        // Theme requires KM/H display to be on
                        config.DisplaySpeedLimitsMph = false;
                        DisplaySpeedLimitsMph.Value = false;
                    } else if (!config.DisplaySpeedLimitsMph && !theme.SupportsKmph) {
                        // Theme requires MPH display to be on
                        config.DisplaySpeedLimitsMph = true;
                        DisplaySpeedLimitsMph.Value = true;
                    }
                    config.RoadSignTheme = selectedTheme;
                    GlobalConfig.WriteConfig();
                }
                return;
            }

            var newTheme = RoadSignThemeManager.Instance.ThemeNames[newThemeIndex];

            Main mainConfig = GlobalConfig.Instance.Main;

            switch (RoadSignThemeManager.Instance.ChangeTheme(
                        newTheme: newTheme,
                        mphEnabled: mainConfig.DisplaySpeedLimitsMph)) {
                case RoadSignThemeManager.ChangeThemeResult.Success:
                    Log.Info($"Road Sign theme changed to {newTheme}");
                    mainConfig.RoadSignTheme = newTheme;
                    break;
                case RoadSignThemeManager.ChangeThemeResult.ForceKmph:
                    mainConfig.DisplaySpeedLimitsMph = false;
                    DisplaySpeedLimitsMph.Value = false;

                    Log.Info($"Road Sign theme was changed to {newTheme} AND display switched to km/h");

                    if (TMPESettings.IsGameLoaded(false)) {
                        ModUI.Instance.Events.DisplayMphChanged(false);
                    }
                    break;
                case RoadSignThemeManager.ChangeThemeResult.ForceMph:
                    mainConfig.DisplaySpeedLimitsMph = true;
                    DisplaySpeedLimitsMph.Value = true;

                    Log.Info($"Road Sign theme was changed to {newTheme} AND display switched to MPH");

                    if (TMPESettings.IsGameLoaded(false)) {
                        ModUI.Instance.Events.DisplayMphChanged(true);
                    }
                    break;
            }

            mainConfig.RoadSignTheme = newTheme;
            GlobalConfig.WriteConfig();
        }
    }
}