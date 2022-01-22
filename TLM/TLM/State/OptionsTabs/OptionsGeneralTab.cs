namespace TrafficManager.State {
    using System;
    using System.Collections.Generic;
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
    using UI.WhatsNew;

    public static class OptionsGeneralTab {
        // suppress "this option can only be set in-game" warning
        // when setting is changed from main menu.
        internal const bool GLOBAL_SETTING = true;

        // Group: Localisation
        [UsedImplicitly]
        private static UIDropDown _languageDropdown;
        private static UICheckBox _displayMphToggle;
        private static UIDropDown _roadSignsThemeDropdown;
        // Group: Interface
        private static UISlider _guiScaleSlider;
        private static UISlider _guiOpacitySlider;
        private static UISlider _overlayTransparencySlider;
        // Group: Simulation
        private static UIDropDown _simulationAccuracyDropdown;
        // Group: Compatibility

        // GlobalConfig.Instance.Main.DisplaySpeedLimitsMph
        public static CheckboxOption DisplaySpeedLimitsMph =
            new(nameof(Options.DisplaySpeedLimitsMph), GLOBAL_SETTING) {
                Label = "Checkbox:Display speed limits mph",
                Handler = OnDisplaySpeedLimitsMphChange,
                Translator = nameof(Translation.SpeedLimits),
            };

        // GlobalConfig.Instance.Main.MainMenuButtonPosLocked
        public static CheckboxOption MainMenuButtonPosLocked =
            new(nameof(Options.MainMenuButtonPosLocked), GLOBAL_SETTING) {
                Label = "General.Checkbox:Lock main menu button position",
                Handler = OnMainMenuButtonPosLockedChanged,
            };

        // GlobalConfig.Instance.Main.MainMenuPosLocked
        public static CheckboxOption MainMenuPosLocked =
            new(nameof(Options.MainMenuPosLocked), GLOBAL_SETTING) {
                Label = "General.Checkbox:Lock main menu window position",
                Handler = OnMainMenuPosLockedChanged,
            };

        // GlobalConfig.Instance.Main.UseUUI
        public static CheckboxOption UseUUI =
            new(nameof(Options.UseUUI), GLOBAL_SETTING) {
                Label = "General.Checkbox:Use UnifiedUI",
                Handler = OnUseUUIChanged,
            };

        // GlobalConfig.Instance.Main.EnableTutorial
        public static CheckboxOption EnableTutorial =
            new(nameof(Options.EnableTutorial), GLOBAL_SETTING) {
                Label = "General.Checkbox:Enable tutorials",
                Handler = OnEnableTutorialChanged,
            };

        public static CheckboxOption InstantEffects =
            new(nameof(Options.instantEffects)) {
                Label = "General.Checkbox:Apply AI changes right away",
                Handler = OnInstantEffectsChanged,
            };

        // GlobalConfig.Instance.Main.ScanForKnownIncompatibleModsAtStartup
        public static CheckboxOption ScanForKnownIncompatibleModsAtStartup =
            new(nameof(Options.ScanForKnownIncompatibleModsAtStartup), GLOBAL_SETTING) {
                Label = "Checkbox:Scan for known incompatible mods on startup",
                Handler = OnScanForKnownIncompatibleModsAtStartupChanged,
                Translator = nameof(Translation.ModConflicts),
            };

        // GlobalConfig.Instance.Main.IgnoreDisabledMods
        public static CheckboxOption IgnoreDisabledMods =
            new(nameof(Options.IgnoreDisabledMods), GLOBAL_SETTING) {
                Label = "Checkbox:Ignore disabled mods",
                Indent = true,
                Handler = OnIgnoreDisabledModsChanged,
                Translator = nameof(Translation.ModConflicts),
            };

        // GlobalConfig.Instance.Main.ShowCompatibilityCheckErrorMessage
        public static CheckboxOption ShowCompatibilityCheckErrorMessage =
            new(nameof(Options.ShowCompatibilityCheckErrorMessage), GLOBAL_SETTING) {
                Label = "General.Checkbox:Notify me about TM:PE startup conflicts",
                Handler = OnShowCompatibilityCheckErrorMessageChanged,
            };

        private static string T(string key) => Translation.Options.Get(key);

        internal static void MakeSettings_General(ExtUITabstrip tabStrip) {
            UIHelper tab = tabStrip.AddTabPage(T("Tab:General"));
            UIHelperBase group;

            // Set checkbox values based on global config
            DisplaySpeedLimitsMph.Value = GlobalConfig.Instance.Main.DisplaySpeedLimitsMph;
            MainMenuButtonPosLocked.Value = GlobalConfig.Instance.Main.MainMenuButtonPosLocked;
            MainMenuPosLocked.Value = GlobalConfig.Instance.Main.MainMenuPosLocked;
            UseUUI.Value = GlobalConfig.Instance.Main.UseUUI;
            EnableTutorial.Value = GlobalConfig.Instance.Main.EnableTutorial;
            ScanForKnownIncompatibleModsAtStartup.Value = GlobalConfig.Instance.Main.ScanForKnownIncompatibleModsAtStartup;
            IgnoreDisabledMods.Value = GlobalConfig.Instance.Main.IgnoreDisabledMods;
            ShowCompatibilityCheckErrorMessage.Value = GlobalConfig.Instance.Main.ShowCompatibilityCheckErrorMessage;

            tab.AddSpace(5);
            tab.AddButton("What's New?", WhatsNew.OpenModal);
            tab.AddSpace(10);

            // Group: Localisation
            group = tab.AddGroup(T("General.Group:Localisation"));
            AddLanguageDropdown(group, GlobalConfig.Instance.LanguageCode);
            AddCrowdinButton(group);
            AddLocalisationWikiButton(group);
            // TODO: #1221 Ditch separate MPH vs. km/h setting? selected icon theme should determine that?
            DisplaySpeedLimitsMph.AddUI(group);
            AddIconThemeDropdown(group, GlobalConfig.Instance.Main.RoadSignTheme);

            // Group: Interface
            group = tab.AddGroup(T("General.Group:Interface"));
            MainMenuButtonPosLocked.AddUI(group);
            MainMenuPosLocked.AddUI(group);
            UseUUI.AddUI(group);
            AddGuiScaleSlider(group,
                GlobalConfig.Instance.Main.GuiScale);
            // TODO: #1268 These should both be `Opacity`
            AddGuiTransparencySliders(group,
                GlobalConfig.Instance.Main.GuiOpacity,
                GlobalConfig.Instance.Main.OverlayTransparency);
            EnableTutorial.AddUI(group);

            // Group: Simulation
            group = tab.AddGroup(T("General.Group:Simulation"));
            AddSimAccuracyDropdown(group,
                (int)Options.simulationAccuracy);
            InstantEffects.AddUI(group);

            // Group: Compatibility
            group = tab.AddGroup(T("General.Group:Compatibility"));
            ScanForKnownIncompatibleModsAtStartup.AddUI(group);
            IgnoreDisabledMods.AddUI(group);
            IgnoreDisabledMods.Enabled = ScanForKnownIncompatibleModsAtStartup.Value;
            ShowCompatibilityCheckErrorMessage.AddUI(group);
        }

        // Additional UI creation stuff:

        private static void AddLanguageDropdown(UIHelperBase group, string? currentLangCode) {
            string[] languageLabels = new string[Translation.AvailableLanguageCodes.Count + 1];
            languageLabels[0] = T("General.Dropdown.Option:Game language");

            for (int i = 0; i < Translation.AvailableLanguageCodes.Count; ++i) {
                languageLabels[i + 1] = Translation.Options.Get(
                    lang: Translation.AvailableLanguageCodes[i],
                    key: "General.Dropdown.Option:Language Name");
            }

            int languageIndex = 0;

            if (currentLangCode != null) {
                languageIndex = Translation.AvailableLanguageCodes.IndexOf(currentLangCode);
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
        }

        private static void AddLocalisationButtons(UIHelperBase group) {
            UIButton button;

            button = group.AddButton(
                text: T("General.Button:Help us translate on Crowdin"),
                eventCallback: OnCrowdinButonClick) as UIButton;
            button.tooltip = "https://crowdin.com/project/tmpe";
            button.textScale = 0.8f;

            button = group.AddButton(
                text: T("General.Button:View localisation guide"),
                eventCallback: OnLocalisationWikiButtonClick) as UIButton;
            button.tooltip = "https://github.com/CitiesSkylinesMods/TMPE/wiki/Localisation";
            button.textScale = 0.8f;

        }

        private static void OnCrowdinButonClick() {
            if (TMPELifecycle.InGameOrEditor()) {
                SimulationManager.instance.SimulationPaused = true;
            }
            Application.OpenURL("https://crowdin.com/project/tmpe");
        }

        private static void OnLocalisationWikiButtonClick() {
            if (TMPELifecycle.InGameOrEditor()) {
                SimulationManager.instance.SimulationPaused = true;
            }
            Application.OpenURL("https://github.com/CitiesSkylinesMods/TMPE/wiki/Localisation");
        }

        private static void AddIconThemeDropdown(UIHelperBase group, string? roadSignTheme) {
            string FormatThemeName(string themeName) {
                return Translation.SpeedLimits.Get($"RoadSignTheme:{themeName}");
            }

            var themeNames = RoadSignThemes.Instance.ThemeNames;
            var themeOptions = themeNames.Select(FormatThemeName).ToArray();
            int selectedThemeIndex = themeNames.FindIndex(x => x == roadSignTheme);
            int defaultSignsThemeIndex = RoadSignThemes.Instance.FindDefaultThemeIndex(GlobalConfig.Instance.Main.DisplaySpeedLimitsMph);

            _roadSignsThemeDropdown
                = group.AddDropdown(
                      text: Translation.SpeedLimits.Get("General.Dropdown:Road signs theme") + ":",
                      options: themeOptions,
                      defaultSelection: selectedThemeIndex >= 0 ? selectedThemeIndex : defaultSignsThemeIndex,
                      eventCallback: OnRoadSignsThemeChanged) as UIDropDown;

            _roadSignsThemeDropdown.width *= 2.0f;
        }

        private static void AddGuiScaleSlider(UIHelperBase group, float guiScale) {
            _guiScaleSlider = group.AddSlider(
                                  text: T("General.Slider:GUI scale") + ":",
                                  min: 50,
                                  max: 200,
                                  step: 5,
                                  defaultValue: guiScale,
                                  eventCallback: OnGuiScaleChanged) as UISlider;
            _guiScaleSlider.parent.Find<UILabel>("Label").width = 500;
        }

        private static void AddGuiTransparencySliders(UIHelperBase group, byte guiOpacity, byte overlayTransparency) {
            _guiOpacitySlider = group.AddSlider(
                                        text: T("General.Slider:Window transparency") + ":",
                                        min: 0,
                                        max: 100,
                                        step: 5,
                                        defaultValue: guiOpacity,
                                        eventCallback: OnGuiOpacityChanged) as UISlider;
            _guiOpacitySlider.parent.Find<UILabel>("Label").width = 500;

            _overlayTransparencySlider = group.AddSlider(
                                             text: T("General.Slider:Overlay transparency") + ":",
                                             min: 0,
                                             max: 100,
                                             step: 5,
                                             defaultValue: overlayTransparency,
                                             eventCallback: OnOverlayTransparencyChanged) as UISlider;
            _overlayTransparencySlider.parent.Find<UILabel>("Label").width = 500;
        }

        private static void AddSimAccuracyDropdown(UIHelperBase group, int simulationAccuracy) {
            string[] simPrecisionOptions = new[] {
                T("General.Dropdown.Option:Very low"),
                T("General.Dropdown.Option:Low"),
                T("General.Dropdown.Option:Medium"),
                T("General.Dropdown.Option:High"),
                T("General.Dropdown.Option:Very high"),
            };

            _simulationAccuracyDropdown
                = group.AddDropdown(
                    text: T("General.Dropdown:Simulation accuracy") + ":",
                    options: simPrecisionOptions,
                    defaultSelection: simulationAccuracy,
                    eventCallback: OnSimulationAccuracyChanged) as UIDropDown;
        }

        // Handlers: Localization

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

        private static void OnDisplaySpeedLimitsMphChange(bool newMphValue) {
            bool supportedByTheme = newMphValue
                                        ? RoadSignThemes.ActiveTheme.SupportsMph
                                        : RoadSignThemes.ActiveTheme.SupportsKmph;

            if (!supportedByTheme) {
                // Reset to German road signs theme
                _roadSignsThemeDropdown.selectedIndex = RoadSignThemes.Instance.FindDefaultThemeIndex(newMphValue);
                GlobalConfig.Instance.Main.RoadSignTheme = RoadSignThemes.Instance.GetDefaultThemeName(newMphValue);
                Log.Info(
                    $"DisplaySpeedLimitsMph -> {newMphValue}, but was not supported by current theme, "
                    + "so theme was also reset to German_Kmph");
            } else {
                Log.Info($"DisplaySpeedLimitsMph -> {newMphValue}");
            }

            GlobalConfig.Instance.Main.DisplaySpeedLimitsMph = newMphValue;
            GlobalConfig.WriteConfig();

            if (Options.IsGameLoaded(false)) {
                ModUI.Instance.Events.DisplayMphChanged(newMphValue);
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
                    Log.Info($"GlobalConfig.Instance.Main.RoadSignTheme -> {newTheme}");
                    mainConfig.RoadSignTheme = newTheme;
                    break;
                case RoadSignThemes.ChangeThemeResult.ForceKmph:
                    mainConfig.DisplaySpeedLimitsMph = false;
                    _displayMphToggle.isChecked = false;

                    Log.Info($"GlobalConfig.Instance.Main.RoadSignTheme -> {newTheme} AND display switched to km/h");

                    if (Options.IsGameLoaded(false)) {
                        ModUI.Instance.Events.DisplayMphChanged(false);
                    }
                    break;
                case RoadSignThemes.ChangeThemeResult.ForceMph:
                    mainConfig.DisplaySpeedLimitsMph = true;
                    _displayMphToggle.isChecked = true;

                    Log.Info($"GlobalConfig.Instance.Main.RoadSignTheme -> {newTheme} AND display switched to MPH");

                    if (Options.IsGameLoaded(false)) {
                        ModUI.Instance.Events.DisplayMphChanged(true);
                    }
                    break;
            }

            mainConfig.RoadSignTheme = newTheme;
            GlobalConfig.WriteConfig();
        }

        // Handlers: Interface

        private static void OnMainMenuButtonPosLockedChanged(bool newValue) {
            Log._Debug($"GlobalConfig.Instance.Main.MainMenuButtonPosLocked -> {newValue}");
            GlobalConfig.Instance.Main.MainMenuButtonPosLocked = newValue;
            GlobalConfig.WriteConfig();

            if (Options.IsGameLoaded(false)) {
                ModUI.Instance.MainMenuButton.SetPosLock(newValue);
            }
        }

        private static void OnMainMenuPosLockedChanged(bool newValue) {
            Log._Debug($"GlobalConfig.Instance.Main.MainMenuPosLocked -> {newValue}");
            GlobalConfig.Instance.Main.MainMenuPosLocked = newValue;
            GlobalConfig.WriteConfig();

            if (Options.IsGameLoaded(false)) {
                ModUI.Instance.MainMenu.SetPosLock(newValue);
            }
        }

        private static void OnUseUUIChanged(bool newValue) {
            Log._Debug($"GlobalConfig.Instance.Main.UseUUI -> {newValue}");
            GlobalConfig.Instance.Main.UseUUI = newValue;
            GlobalConfig.WriteConfig();

            var button = ModUI.GetTrafficManagerTool()?.UUIButton;
            if (button) {
                button.isVisible = newValue;
            }
            ModUI.Instance?.MainMenuButton?.UpdateButtonSkinAndTooltip();
        }

        public static void SetGuiScale(float val) {
            bool changed = (int)val != (int)GlobalConfig.Instance.Main.GuiScale;
            GlobalConfig.Instance.Main.GuiScale = val;

            if (changed && _guiScaleSlider != null) {
                _guiScaleSlider.value = val;
                ModUI.Instance.Events.UiScaleChanged();
            }
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
            Log._Debug($"GlobalConfig.Instance.Main.GuiScale -> {GlobalConfig.Instance.Main.GuiScale}");
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

        public static void SetOverlayTransparency(byte val) {
            bool changed = val != GlobalConfig.Instance.Main.OverlayTransparency;
            GlobalConfig.Instance.Main.OverlayTransparency = val;

            if (changed && _overlayTransparencySlider != null) {
                _overlayTransparencySlider.value = val;
            }
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

            Log._Debug($"GlobalConfig.Instance.Main.OverlayTransparency -> {GlobalConfig.Instance.Main.OverlayTransparency}");
        }

        private static void OnEnableTutorialChanged(bool newValue) {
            Log._Debug($"GlobalConfig.Instance.Main.EnableTutorial -> {newValue}");
            GlobalConfig.Instance.Main.EnableTutorial = newValue;
            GlobalConfig.WriteConfig();
        }

        // Handlers: Simulation

        public static void SetSimulationAccuracy(SimulationAccuracy newAccuracy) {
            Options.simulationAccuracy = newAccuracy;
            if (_simulationAccuracyDropdown != null) {
                _simulationAccuracyDropdown.selectedIndex = (int)newAccuracy;
            }
        }

        private static void OnSimulationAccuracyChanged(int newAccuracy) {
            if (!Options.IsGameLoaded()) {
                return;
            }

            Log._Debug($"Options.simulationAccuracy -> {newAccuracy}");
            Options.simulationAccuracy = (SimulationAccuracy)newAccuracy;
        }

        private static void OnInstantEffectsChanged(bool newValue) {
            Log._Debug($"Options.instantEffects -> {newValue}");
            Options.instantEffects = newValue;
        }

        // Handlers: Compatibility

        private static void OnShowCompatibilityCheckErrorMessageChanged(bool newValue) {
            Log._Debug($"GlobalConfig.Instance.Main.ShowCompatibilityCheckErrorMessage -> {newValue}");
            GlobalConfig.Instance.Main.ShowCompatibilityCheckErrorMessage = newValue;
            GlobalConfig.WriteConfig();
        }

        private static void OnScanForKnownIncompatibleModsAtStartupChanged(bool newValue) {
            Log._Debug($"GlobalConfig.Instance.Main.ScanForKnownIncompatibleModsAtStartup -> {newValue}");
            GlobalConfig.Instance.Main.ScanForKnownIncompatibleModsAtStartup = newValue;
            GlobalConfig.WriteConfig();

            IgnoreDisabledMods.Enabled = newValue;
        }

        private static void OnIgnoreDisabledModsChanged(bool newValue) {
            Log._Debug($"GlobalConfig.Instance.Main.IgnoreDisabledMods -> {newValue}");
            GlobalConfig.Instance.Main.IgnoreDisabledMods = newValue;
            GlobalConfig.WriteConfig();
        }

        // Obsolete?

        /// <summary>User clicked [scale GUI to screen resolution] checkbox.</summary>
        private static void OnGuiScaleToResChanged(float newVal) {
            SetGuiScale(newVal);
            if (TMPELifecycle.Instance.IsGameLoaded) {
                _guiScaleSlider.RefreshTooltip();
            }

            GlobalConfig.WriteConfig();
            Log._Debug($"GlobalConfig.Instance.Main.GuiScale -> {GlobalConfig.Instance.Main.GuiScale}");
        }

    } // end class
}
