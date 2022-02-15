namespace TrafficManager.State {
    using TrafficManager.API.Traffic.Enums;
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using ICities;
    using JetBrains.Annotations;
    using TrafficManager.U;
    using TrafficManager.UI;
    using TrafficManager.UI.Helpers;
    using TrafficManager.UI.WhatsNew;
    using UnityEngine;
    using TrafficManager.Lifecycle;

    public static class GeneralTab {
        private static UICheckBox _instantEffectsToggle;

        [UsedImplicitly]
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

        private static UICheckBox _useUUI;

        private static string T(string key) => Translation.Options.Get(key);

        internal static void MakeSettings_General(ExtUITabstrip tabStrip) {
            UIHelper tab = tabStrip.AddTabPage(T("Tab:General"));
            UIHelperBase group;

#if DEBUG
            GeneralTab_DebugGroup.AddUI(tab);
#endif

            tab.AddSpace(5);
            tab.AddButton("What's New?", WhatsNew.OpenModal);
            tab.AddSpace(5);

            GeneralTab_LocalisationGroup.AddUI(tab);


            group = tab.AddGroup(T("General.Group:Interface"));

            _lockButtonToggle = group.AddCheckbox(
                                    text: T("General.Checkbox:Lock main menu button position"),
                                    defaultValue: GlobalConfig.Instance.Main.MainMenuButtonPosLocked,
                                    eventCallback: OnLockButtonChanged) as UICheckBox;
            _lockMenuToggle = group.AddCheckbox(
                                  text: T("General.Checkbox:Lock main menu window position"),
                                  defaultValue: GlobalConfig.Instance.Main.MainMenuPosLocked,
                                  eventCallback: OnLockMenuChanged) as UICheckBox;

            _useUUI = group.AddCheckbox(
                text: T("General.Checkbox:Use UnifiedUI"),
                defaultValue: GlobalConfig.Instance.Main.UseUUI,
                eventCallback: OnUseUUIChanged) as UICheckBox;

            _guiScaleSlider = group.AddSlider(
                                  text: T("General.Slider:GUI scale") + ":",
                                  min: 50,
                                  max: 200,
                                  step: 5,
                                  defaultValue: GlobalConfig.Instance.Main.GuiScale,
                                  eventCallback: OnGuiScaleChanged) as UISlider;
            _guiScaleSlider.parent.Find<UILabel>("Label").width = 500;

            _guiOpacitySlider = group.AddSlider(
                                        text: T("General.Slider:Window transparency") + ":",
                                        min: 0,
                                        max: 100,
                                        step: 5,
                                        defaultValue: GlobalConfig.Instance.Main.GuiOpacity,
                                        eventCallback: OnGuiOpacityChanged) as UISlider;
            _guiOpacitySlider.parent.Find<UILabel>("Label").width = 500;

            _overlayTransparencySlider = group.AddSlider(
                                             text: T("General.Slider:Overlay transparency") + ":",
                                             min: 0,
                                             max: 100,
                                             step: 5,
                                             defaultValue: GlobalConfig.Instance.Main.OverlayTransparency,
                                             eventCallback: OnOverlayTransparencyChanged) as UISlider;
            _overlayTransparencySlider.parent.Find<UILabel>("Label").width = 500;
            _enableTutorialToggle = group.AddCheckbox(
                                        T("General.Checkbox:Enable tutorials"),
                                        GlobalConfig.Instance.Main.EnableTutorial,
                                        OnEnableTutorialsChanged) as UICheckBox;

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
            Options.Indent(_ignoreDisabledModsToggle);
            _showCompatibilityCheckErrorToggle
                = group.AddCheckbox(
                      T("General.Checkbox:Notify me about TM:PE startup conflicts"),
                      GlobalConfig.Instance.Main.ShowCompatibilityCheckErrorMessage,
                      OnShowCompatibilityCheckErrorChanged) as UICheckBox;
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
