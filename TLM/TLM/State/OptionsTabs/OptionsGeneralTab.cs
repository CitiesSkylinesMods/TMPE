namespace TrafficManager.State {
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
        private static UIDropDown _roadSignsMphThemeDropdown;
        private static int _roadSignMphStyleInt;

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
                                        min: 10,
                                        max: 100,
                                        step: 5,
                                        defaultValue: GlobalConfig.Instance.Main.GuiOpacity,
                                        eventCallback: OnGuiOpacityChanged) as UISlider;
            _guiOpacitySlider.parent.Find<UILabel>("Label").width = 500;

            _overlayTransparencySlider = generalGroup.AddSlider(
                                             text: T("General.Slider:Overlay transparency") + ":",
                                             min: 0,
                                             max: 90,
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
            _simulationAccuracyDropdown = simGroup.AddDropdown(
                                       text: T("General.Dropdown:Simulation accuracy") + ":",
                                       options: new[] {
                                                          T("General.Dropdown.Option:Very low"),
                                                          T("General.Dropdown.Option:Low"),
                                                          T("General.Dropdown.Option:Medium"),
                                                          T("General.Dropdown.Option:High"),
                                                          T("General.Dropdown.Option:Very high"),
                                                      },
                                       defaultSelection: (int)Options.simulationAccuracy,
                                       eventCallback: OnSimulationAccuracyChanged) as UIDropDown;

            _instantEffectsToggle = simGroup.AddCheckbox(
                                       text: T("General.Checkbox:Apply AI changes right away"),
                                       defaultValue: Options.instantEffects,
                                       eventCallback: OnInstantEffectsChanged) as UICheckBox;
        }

        private static void SetupSpeedLimitsPanel(UIHelperBase generalGroup) {
            _displayMphToggle = generalGroup.AddCheckbox(
                                    text: Translation.SpeedLimits.Get("Checkbox:Display speed limits mph"),
                                    defaultValue: GlobalConfig.Instance.Main.DisplaySpeedLimitsMph,
                                    eventCallback: OnDisplayMphChanged) as UICheckBox;
            string[] mphThemeOptions = {
                Translation.SpeedLimits.Get("General.Theme.Option:Square US"),
                Translation.SpeedLimits.Get("General.Theme.Option:Round UK"),
                Translation.SpeedLimits.Get("General.Theme.Option:Round German"),
            };
            _roadSignMphStyleInt = (int)GlobalConfig.Instance.Main.MphRoadSignStyle;
            _roadSignsMphThemeDropdown
                = generalGroup.AddDropdown(
                      text: Translation.SpeedLimits.Get("General.Dropdown:Road signs theme mph") + ":",
                      options: mphThemeOptions,
                      defaultSelection: _roadSignMphStyleInt,
                      eventCallback: OnRoadSignsMphThemeChanged) as UIDropDown;
            _roadSignsMphThemeDropdown.width = 400;
        }

        private static void OnLanguageChanged(int newLanguageIndex) {
            if (newLanguageIndex <= 0) {
                GlobalConfig.Instance.LanguageCode = null;
                GlobalConfig.WriteConfig();
                Translation.SetCurrentLanguageToGameLanguage();
                Options.RebuildMenu();
            } else if (newLanguageIndex - 1 < Translation.AvailableLanguageCodes.Count) {
                string newLang = Translation.AvailableLanguageCodes[newLanguageIndex - 1];
                GlobalConfig.Instance.LanguageCode = newLang;
                GlobalConfig.WriteConfig();
                Translation.SetCurrentLanguageToTMPELanguage();
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
                ModUI.Instance.uiOpacityObservable.NotifyObservers(
                          new ModUI.UIOpacityNotification {
                                                              Opacity = opacity,
                                                          });
            }
        }

        public static void SetGuiScale(float val) {
            bool changed = (int)val != (int)GlobalConfig.Instance.Main.GuiScale;
            GlobalConfig.Instance.Main.GuiScale = val;

            if (changed && _guiScaleSlider != null) {
                _guiScaleSlider.value = val;
                ModUI.Instance.UiScaleObservable.NotifyObservers(
                    new ModUI.UIScaleNotification { NewScale = val });
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
