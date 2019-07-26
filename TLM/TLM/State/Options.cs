namespace TrafficManager.State {
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using API.Traffic.Enums;
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using ICities;
    using Keybinds;
    using Manager.Impl;
    using Traffic.Data;
    using UI;
    using UnityEngine;

    public partial class Options : MonoBehaviour {
        private static UIDropDown languageDropdown;
        private static UICheckBox instantEffectsToggle;
        private static UICheckBox lockButtonToggle;
        private static UICheckBox lockMenuToggle;
        private static UISlider guiTransparencySlider;
        private static UISlider overlayTransparencySlider;
        private static UICheckBox tinyMenuToggle;
        private static UICheckBox enableTutorialToggle;
        private static UICheckBox showCompatibilityCheckErrorToggle;
        private static UICheckBox scanForKnownIncompatibleModsToggle;
        private static UICheckBox ignoreDisabledModsToggle;
        private static UICheckBox displayMphToggle;
        private static UIDropDown roadSignsMphThemeDropdown;
        private static UICheckBox individualDrivingStyleToggle;
        private static UIDropDown recklessDriversDropdown;
        private static UICheckBox disableDespawningToggle;

        private static UICheckBox strongerRoadConditionEffectsToggle;
        private static UICheckBox prohibitPocketCarsToggle;
        private static UICheckBox advancedAIToggle;
        private static UICheckBox realisticPublicTransportToggle;
        private static UISlider altLaneSelectionRatioSlider;

#if DEBUG
        private static List<UICheckBox> debugSwitchFields = new List<UICheckBox>();
        private static List<UITextField> debugValueFields = new List<UITextField>();
        // private static UITextField pathCostMultiplicatorField = null;
        // private static UITextField pathCostMultiplicator2Field = null;
#endif

        public static int roadSignMphStyleInt;
        public static bool instantEffects = true;
        public static bool individualDrivingStyle = true;
        public static int recklessDrivers = 3;

        /// <summary>
        /// Option: buses may ignore lane arrows
        /// </summary>
        public static bool relaxedBusses;

        /// <summary>
        /// debug option: all vehicles may ignore lane arrows
        /// </summary>
        public static bool allRelaxed;
        public static bool evacBussesMayIgnoreRules;
        public static bool prioritySignsOverlay;
        public static bool timedLightsOverlay;
        public static bool speedLimitsOverlay;
        public static bool vehicleRestrictionsOverlay;
        public static bool parkingRestrictionsOverlay;
        public static bool junctionRestrictionsOverlay;
        public static bool connectedLanesOverlay;
#if QUEUEDSTATS
    #if DEBUG
        public static bool showPathFindStats = true;
    #else
        public static bool showPathFindStats = false;
    #endif
#endif

#if DEBUG
        public static bool nodesOverlay;
        public static bool vehicleOverlay;
        public static bool citizenOverlay;
        public static bool buildingOverlay;
#else
        public static bool nodesOverlay = false;
        public static bool vehicleOverlay = false;
        public static bool citizenOverlay = false;
        public static bool buildingOverlay = false;
#endif
        public static bool allowEnterBlockedJunctions;
        public static bool allowUTurns;
        public static bool allowNearTurnOnRed;
        public static bool allowFarTurnOnRed;
        public static bool allowLaneChangesWhileGoingStraight;
        public static bool trafficLightPriorityRules;
        public static bool banRegularTrafficOnBusLanes;
        public static bool advancedAI;
        public static bool realisticPublicTransport;
        public static byte altLaneSelectionRatio;
        public static bool highwayRules;
#if DEBUG
        public static bool showLanes = true;
#else
        public static bool showLanes = false;
#endif
        public static bool strongerRoadConditionEffects;
        public static bool parkingAI;
        public static bool disableDespawning;
        public static bool preferOuterLane;
        //public static byte publicTransportUsage = 1;

        public static bool prioritySignsEnabled = true;
        public static bool timedLightsEnabled = true;
        public static bool customSpeedLimitsEnabled = true;
        public static bool vehicleRestrictionsEnabled = true;
        public static bool parkingRestrictionsEnabled = true;
        public static bool junctionRestrictionsEnabled = true;
        public static bool turnOnRedEnabled = true;
        public static bool laneConnectorEnabled = true;
        public static bool scanForKnownIncompatibleModsEnabled = true;
        public static bool ignoreDisabledModsEnabled;

        public static VehicleRestrictionsAggression vehicleRestrictionsAggression =
            VehicleRestrictionsAggression.Medium;

        internal static bool MenuRebuildRequired {
            set {
                if (value) {
                    if (LoadingExtension.BaseUI != null) {
                        LoadingExtension.BaseUI.RebuildMenu();
                    }
                }
            }
        }

        public static void MakeSettings(UIHelperBase helper) {
            // tabbing code is borrowed from RushHour mod
            // https://github.com/PropaneDragon/RushHour/blob/release/RushHour/Options/OptionHandler.cs
            UIHelper actualHelper = helper as UIHelper;
            UIComponent container = actualHelper.self as UIComponent;

            UITabstrip tabStrip = container.AddUIComponent<UITabstrip>();
            tabStrip.relativePosition = new Vector3(0, 0);
            tabStrip.size = new Vector2(container.width - 20, 40);

            UITabContainer tabContainer = container.AddUIComponent<UITabContainer>();
            tabContainer.relativePosition = new Vector3(0, 40);
            tabContainer.size = new Vector2(container.width - 20, container.height - tabStrip.height - 20);
            tabStrip.tabPages = tabContainer;

            int tabIndex = 0;

            // GENERAL
            MakeSettings_General(tabStrip, tabIndex);

            // GAMEPLAY
            ++tabIndex;
            MakeSettings_Gameplay(tabStrip, tabIndex);

            // VEHICLE RESTRICTIONS
            ++tabIndex;
            OptionsVehicleRestrictionsTab.MakeSettings_VehicleRestrictions(tabStrip, tabIndex);

            // OVERLAYS
            ++tabIndex;
            OptionsOverlaysTab.MakeSettings_Overlays(tabStrip, tabIndex);

            // MAINTENANCE
            ++tabIndex;
            OptionsMaintenanceTab.MakeSettings_Maintenance(tabStrip, tabIndex);

            // KEYBOARD
            ++tabIndex;
            OptionsKeybindsTab.MakeSettings_Keybinds(tabStrip, tabIndex);

            tabStrip.selectedIndex = 0;
        }

        private static string T(string s) {
            return Translation.GetString(s);
        }

        private static void MakeSettings_Gameplay(UITabstrip tabStrip, int tabIndex) {
            AddOptionTab(tabStrip, T("Gameplay"));
            tabStrip.selectedIndex = tabIndex;
            var currentPanel = tabStrip.tabContainer.components[tabIndex] as UIPanel;
            currentPanel.autoLayout = true;
            currentPanel.autoLayoutDirection = LayoutDirection.Vertical;
            currentPanel.autoLayoutPadding.top = 5;
            currentPanel.autoLayoutPadding.left = 10;
            currentPanel.autoLayoutPadding.right = 10;
            var panelHelper = new UIHelper(currentPanel);
            UIHelperBase vehBehaviorGroup = panelHelper.AddGroup(T("Vehicle_behavior"));

            recklessDriversDropdown
                = vehBehaviorGroup.AddDropdown(
                      T("Reckless_driving") + ":",
                      new[] {
                          T("Path_Of_Evil_(10_%)"), T("Rush_Hour_(5_%)"),
                          T("Minor_Complaints_(2_%)"), T("Holy_City_(0_%)")
                      },
                      recklessDrivers,
                      OnRecklessDriversChanged) as UIDropDown;
            recklessDriversDropdown.width = 300;
            individualDrivingStyleToggle = vehBehaviorGroup.AddCheckbox(
                                               T("Individual_driving_styles"),
                                               individualDrivingStyle,
                                               onIndividualDrivingStyleChanged) as UICheckBox;

            if (SteamHelper.IsDLCOwned(SteamHelper.DLC.SnowFallDLC)) {
                strongerRoadConditionEffectsToggle
                    = vehBehaviorGroup.AddCheckbox(
                          T("Road_condition_has_a_bigger_impact_on_vehicle_speed"),
                          strongerRoadConditionEffects,
                          OnStrongerRoadConditionEffectsChanged) as UICheckBox;
            }

            disableDespawningToggle = vehBehaviorGroup.AddCheckbox(
                                          T("Disable_despawning"),
                                          disableDespawning,
                                          onDisableDespawningChanged) as UICheckBox;

            UIHelperBase vehAiGroup = panelHelper.AddGroup(T("Advanced_Vehicle_AI"));
            advancedAIToggle = vehAiGroup.AddCheckbox(
                                   T("Enable_Advanced_Vehicle_AI"),
                                   advancedAI,
                                   OnAdvancedAiChanged) as UICheckBox;
            altLaneSelectionRatioSlider = vehAiGroup.AddSlider(
                                              T("Dynamic_lane_section") + ":",
                                              0,
                                              100,
                                              5,
                                              altLaneSelectionRatio,
                                              OnAltLaneSelectionRatioChanged) as UISlider;
            altLaneSelectionRatioSlider.parent.Find<UILabel>("Label").width = 450;

            UIHelperBase parkAiGroup = panelHelper.AddGroup(T("Parking_AI"));
            prohibitPocketCarsToggle = parkAiGroup.AddCheckbox(
                                           T("Enable_more_realistic_parking"),
                                           parkingAI,
                                           OnProhibitPocketCarsChanged) as UICheckBox;

            UIHelperBase ptGroup = panelHelper.AddGroup(T("Public_transport"));
            realisticPublicTransportToggle = ptGroup.AddCheckbox(
                                                 T(
                                                     "Prevent_excessive_transfers_at_public_transport_stations"),
                                                 realisticPublicTransport,
                                                 OnRealisticPublicTransportChanged) as UICheckBox;
        }

        private static void MakeSettings_General(UITabstrip tabStrip, int tabIndex) {
            AddOptionTab(tabStrip, T("General"));
            tabStrip.selectedIndex = tabIndex;
            UIPanel currentPanel = tabStrip.tabContainer.components[tabIndex] as UIPanel;
            currentPanel.autoLayout = true;
            currentPanel.autoLayoutDirection = LayoutDirection.Vertical;
            currentPanel.autoLayoutPadding.top = 5;
            currentPanel.autoLayoutPadding.left = 10;
            currentPanel.autoLayoutPadding.right = 10;
            UIHelper panelHelper = new UIHelper(currentPanel);

            UIHelperBase generalGroup = panelHelper.AddGroup(T("General"));
            string[] languageLabels = new string[Translation.AVAILABLE_LANGUAGE_CODES.Count + 1];
            languageLabels[0] = T("Game_language");

            for (int i = 0; i < Translation.AVAILABLE_LANGUAGE_CODES.Count; ++i) {
                languageLabels[i + 1] =
                    Translation.LANGUAGE_LABELS[Translation.AVAILABLE_LANGUAGE_CODES[i]];
            }

            int languageIndex = 0;
            string curLangCode = GlobalConfig.Instance.LanguageCode;

            if (curLangCode != null) {
                languageIndex = Translation.AVAILABLE_LANGUAGE_CODES.IndexOf(curLangCode);
                if (languageIndex < 0) {
                    languageIndex = 0;
                } else {
                    ++languageIndex;
                }
            }

            languageDropdown = generalGroup.AddDropdown(
                                   T("Language") + ":",
                                   languageLabels,
                                   languageIndex,
                                   OnLanguageChanged) as UIDropDown;
            lockButtonToggle = generalGroup.AddCheckbox(
                                   T("Lock_main_menu_button_position"),
                                   GlobalConfig.Instance.Main.MainMenuButtonPosLocked,
                                   OnLockButtonChanged) as UICheckBox;
            lockMenuToggle = generalGroup.AddCheckbox(
                                 T("Lock_main_menu_position"),
                                 GlobalConfig.Instance.Main.MainMenuPosLocked,
                                 OnLockMenuChanged) as UICheckBox;
            tinyMenuToggle = generalGroup.AddCheckbox(
                                 T("Compact_main_menu"),
                                 GlobalConfig.Instance.Main.TinyMainMenu,
                                 OnCompactMainMenuChanged) as UICheckBox;
            guiTransparencySlider = generalGroup.AddSlider(
                                        T("Window_transparency") + ":",
                                        0,
                                        90,
                                        5,
                                        GlobalConfig.Instance.Main.GuiTransparency,
                                        OnGuiTransparencyChanged) as UISlider;
            guiTransparencySlider.parent.Find<UILabel>("Label").width = 500;
            overlayTransparencySlider = generalGroup.AddSlider(
                                            T("Overlay_transparency") + ":",
                                            0,
                                            90,
                                            5,
                                            GlobalConfig.Instance.Main.OverlayTransparency,
                                            OnOverlayTransparencyChanged) as UISlider;
            overlayTransparencySlider.parent.Find<UILabel>("Label").width = 500;
            enableTutorialToggle = generalGroup.AddCheckbox(
                                       T("Enable_tutorial_messages"),
                                       GlobalConfig.Instance.Main.EnableTutorial,
                                       OnEnableTutorialsChanged) as UICheckBox;
            showCompatibilityCheckErrorToggle
                = generalGroup.AddCheckbox(
                      T("Notify_me_if_there_is_an_unexpected_mod_conflict"),
                      GlobalConfig.Instance.Main.ShowCompatibilityCheckErrorMessage,
                      OnShowCompatibilityCheckErrorChanged) as UICheckBox;
            scanForKnownIncompatibleModsToggle
                = generalGroup.AddCheckbox(
                      T("Scan_for_known_incompatible_mods_on_startup"),
                      GlobalConfig.Instance.Main.ScanForKnownIncompatibleModsAtStartup,
                      OnScanForKnownIncompatibleModsChanged) as UICheckBox;
            ignoreDisabledModsToggle = generalGroup.AddCheckbox(
                                           T("Ignore_disabled_mods"),
                                           GlobalConfig.Instance.Main.IgnoreDisabledMods,
                                           OnIgnoreDisabledModsChanged) as UICheckBox;
            Indent(ignoreDisabledModsToggle);

            // General: Speed Limits
            SetupSpeedLimitsPanel(generalGroup);

            // General: Simulation
            UIHelperBase simGroup = panelHelper.AddGroup(T("Simulation"));
            instantEffectsToggle = simGroup.AddCheckbox(
                                       T("Customizations_come_into_effect_instantaneously"),
                                       instantEffects,
                                       OnInstantEffectsChanged) as UICheckBox;
        }

        private static void SetupSpeedLimitsPanel(UIHelperBase generalGroup) {
            displayMphToggle = generalGroup.AddCheckbox(
                                   T("Display_speed_limits_mph"),
                                   GlobalConfig.Instance.Main.DisplaySpeedLimitsMph,
                                   OnDisplayMphChanged) as UICheckBox;
            string[] mphThemeOptions = {
                T("theme_Square_US"),
                T("theme_Round_UK"),
                T("theme_Round_German")
            };
            roadSignMphStyleInt = (int)GlobalConfig.Instance.Main.MphRoadSignStyle;
            roadSignsMphThemeDropdown = generalGroup.AddDropdown(
                                            T("Road_signs_theme_mph") + ":",
                                            mphThemeOptions,
                                            roadSignMphStyleInt,
                                            OnRoadSignsMphThemeChanged) as UIDropDown;
            roadSignsMphThemeDropdown.width = 400;
        }

        internal static void Indent<T>(T component) where T : UIComponent {
            UILabel label = component.Find<UILabel>("Label");

            if (label != null) {
                label.padding = new RectOffset(22, 0, 0, 0);
            }

            UISprite check = component.Find<UISprite>("Unchecked");

            if (check != null) {
                check.relativePosition += new Vector3(22.0f, 0);
            }
        }

        public static void AddOptionTab(UITabstrip tabStrip, string caption) {
            UIButton tabButton = tabStrip.AddTab(caption);

            tabButton.normalBgSprite = "SubBarButtonBase";
            tabButton.disabledBgSprite = "SubBarButtonBaseDisabled";
            tabButton.focusedBgSprite = "SubBarButtonBaseFocused";
            tabButton.hoveredBgSprite = "SubBarButtonBaseHovered";
            tabButton.pressedBgSprite = "SubBarButtonBasePressed";

            tabButton.textPadding = new RectOffset(10, 10, 10, 10);
            tabButton.autoSize = true;
            tabButton.tooltip = caption;
        }

        /// <summary>
        /// If the game is not loaded and warn is true, will display a warning about options being
        /// local to each savegame.
        /// </summary>
        /// <param name="warn">Whether to display a warning popup</param>
        /// <returns>The game is loaded</returns>
        internal static bool IsGameLoaded(bool warn = true) {
            if (SerializableDataExtension.StateLoading || LoadingExtension.IsGameLoaded) {
                return true;
            }

            if (warn) {
                UIView.library.ShowModal<ExceptionPanel>("ExceptionPanel").SetMessage(
                    "Nope!",
                    T("Settings_are_defined_for_each_savegame_separately") +
                    ". https://www.viathinksoft.de/tmpe/#options",
                    false);
            }

            return false;
        }

        private static void OnGuiTransparencyChanged(float newVal) {
            if (!IsGameLoaded()) {
                return;
            }

            SetGuiTransparency((byte)Mathf.RoundToInt(newVal));
            guiTransparencySlider.tooltip = T("Window_transparency") + ": " +
                                            GlobalConfig.Instance.Main.GuiTransparency + " %";

            GlobalConfig.WriteConfig();
            Log._Debug($"GuiTransparency changed to {GlobalConfig.Instance.Main.GuiTransparency}");
        }

        private static void OnOverlayTransparencyChanged(float newVal) {
            if (!IsGameLoaded()) {
                return;
            }

            SetOverlayTransparency((byte)Mathf.RoundToInt(newVal));
            overlayTransparencySlider.tooltip = T("Overlay_transparency") + ": " +
                                                GlobalConfig.Instance.Main.OverlayTransparency +
                                                " %";
            GlobalConfig.WriteConfig();
            Log._Debug($"OverlayTransparency changed to {GlobalConfig.Instance.Main.OverlayTransparency}");
        }

        private static void OnAltLaneSelectionRatioChanged(float newVal) {
            if (!IsGameLoaded()) {
                return;
            }

            SetAltLaneSelectionRatio((byte)Mathf.RoundToInt(newVal));
            altLaneSelectionRatioSlider.tooltip =
                T("Percentage_of_vehicles_performing_dynamic_lane_section") + ": " +
                altLaneSelectionRatio + " %";

            Log._Debug($"altLaneSelectionRatio changed to {altLaneSelectionRatio}");
        }

        private static void OnLanguageChanged(int newLanguageIndex) {
            bool localeChanged = false;

            if (newLanguageIndex <= 0) {
                GlobalConfig.Instance.LanguageCode = null;
                GlobalConfig.WriteConfig();
                MenuRebuildRequired = true;
                localeChanged = true;
            } else if (newLanguageIndex - 1 < Translation.AVAILABLE_LANGUAGE_CODES.Count) {
                GlobalConfig.Instance.LanguageCode =
                    Translation.AVAILABLE_LANGUAGE_CODES[newLanguageIndex - 1];
                GlobalConfig.WriteConfig();
                MenuRebuildRequired = true;
                localeChanged = true;
            } else {
                Log.Warning(
                    $"Options.onLanguageChanged: Invalid language index: {newLanguageIndex}");
            }

            if (localeChanged) {
                MethodInfo onChangedHandler = typeof(OptionsMainPanel).GetMethod(
                    "OnLocaleChanged",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (onChangedHandler != null) {
                    onChangedHandler.Invoke(
                        UIView.library.Get<OptionsMainPanel>("OptionsPanel"),
                        new object[0] { });
                }
            }
        }

        private static void OnLockButtonChanged(bool newValue) {
            Log._Debug($"Button lock changed to {newValue}");
            if (IsGameLoaded(false)) {
                LoadingExtension.BaseUI.MainMenuButton.SetPosLock(newValue);
            }

            GlobalConfig.Instance.Main.MainMenuButtonPosLocked = newValue;
            GlobalConfig.WriteConfig();
        }

        private static void OnLockMenuChanged(bool newValue) {
            Log._Debug($"Menu lock changed to {newValue}");
            if (IsGameLoaded(false)) {
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
                setIgnoreDisabledMods(false);
                OnIgnoreDisabledModsChanged(false);
            }
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
            if (displayMphToggle != null) {
                displayMphToggle.isChecked = value;
            }
        }

        private static void OnRoadSignsMphThemeChanged(int newRoadSignStyle) {
            if (!IsGameLoaded()) {
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
            if (!IsGameLoaded()) {
                return;
            }

            Log._Debug($"Instant effects changed to {newValue}");
            instantEffects = newValue;
        }

        private static void OnRecklessDriversChanged(int newRecklessDrivers) {
            if (!IsGameLoaded()) {
                return;
            }

            Log._Debug($"Reckless driver amount changed to {newRecklessDrivers}");
            recklessDrivers = newRecklessDrivers;
        }

        private static void OnAdvancedAiChanged(bool newAdvancedAI) {
            if (!IsGameLoaded()) {
                return;
            }

            Log._Debug($"advancedAI changed to {newAdvancedAI}");
            setAdvancedAI(newAdvancedAI);
        }

        private static void OnStrongerRoadConditionEffectsChanged(bool newStrongerRoadConditionEffects) {
            if (!IsGameLoaded())
                return;

            Log._Debug($"strongerRoadConditionEffects changed to {newStrongerRoadConditionEffects}");
            strongerRoadConditionEffects = newStrongerRoadConditionEffects;
        }

        private static void OnProhibitPocketCarsChanged(bool newValue) {
            if (!IsGameLoaded())
                return;

            Log._Debug($"prohibitPocketCars changed to {newValue}");

            parkingAI = newValue;
            if (parkingAI) {
                AdvancedParkingManager.Instance.OnEnableFeature();
            } else {
                AdvancedParkingManager.Instance.OnDisableFeature();
            }
        }

        private static void OnRealisticPublicTransportChanged(bool newValue) {
            if (!IsGameLoaded())
                return;

            Log._Debug($"realisticPublicTransport changed to {newValue}");
            realisticPublicTransport = newValue;
        }

        private static void onIndividualDrivingStyleChanged(bool value) {
            if (!IsGameLoaded())
                return;

            Log._Debug($"individualDrivingStyle changed to {value}");
            setIndividualDrivingStyle(value);
        }

        private static void onDisableDespawningChanged(bool value) {
            if (!IsGameLoaded())
                return;

            Log._Debug($"disableDespawning changed to {value}");
            disableDespawning = value;
        }

//        private static void onFloatValueChanged(string varName, string newValueStr, ref float var) {
//            if (!IsGameLoaded())
//                return;
//
//            try {
//                float newValue = Single.Parse(newValueStr);
//                var = newValue;
//                Log._Debug($"{varName} changed to {newValue}");
//            } catch (Exception e) {
//                Log.Warning($"An invalid value was inserted: '{newValueStr}'. Error: {e}");
//                //UIView.library.ShowModal<ExceptionPanel>("ExceptionPanel").SetMessage("Invalid value", "An invalid value was inserted.", false);
//            }
//        }

//        private static void onBoolValueChanged(string varName, bool newVal, ref bool var) {
//            if (!IsGameLoaded())
//                return;
//
//            var = newVal;
//            Log._Debug($"{varName} changed to {newVal}");
//        }


        public static void SetRecklessDrivers(int newRecklessDrivers) {
            recklessDrivers = newRecklessDrivers;
            if (recklessDriversDropdown != null)
                recklessDriversDropdown.selectedIndex = newRecklessDrivers;
        }

        internal static bool IsStockLaneChangerUsed() {
            return !advancedAI;
        }

        private static void setAdvancedAI(bool newAdvancedAI) {
            bool changed = newAdvancedAI != advancedAI;
            advancedAI = newAdvancedAI;

            if (changed && advancedAIToggle != null) {
                advancedAIToggle.isChecked = newAdvancedAI;
            }

            if (changed && !newAdvancedAI) {
                SetAltLaneSelectionRatio(0);
            }
        }

        public static void SetGuiTransparency(byte val) {
            bool changed = val != GlobalConfig.Instance.Main.GuiTransparency;
            GlobalConfig.Instance.Main.GuiTransparency = val;

            if (changed && guiTransparencySlider != null) {
                guiTransparencySlider.value = val;
            }
        }

        public static void SetOverlayTransparency(byte val) {
            bool changed = val != GlobalConfig.Instance.Main.OverlayTransparency;
            GlobalConfig.Instance.Main.OverlayTransparency = val;

            if (changed && overlayTransparencySlider != null) {
                overlayTransparencySlider.value = val;
            }
        }

        public static void SetAltLaneSelectionRatio(byte val) {
            bool changed = val != altLaneSelectionRatio;
            altLaneSelectionRatio = val;

            if (changed && altLaneSelectionRatioSlider != null) {
                altLaneSelectionRatioSlider.value = val;
            }

            if (changed && altLaneSelectionRatio > 0) {
                setAdvancedAI(true);
            }
        }

        public static void SetInstantEffects(bool value) {
            instantEffects = value;
            if (instantEffectsToggle != null)
                instantEffectsToggle.isChecked = value;
        }

        public static void SetStrongerRoadConditionEffects(bool newStrongerRoadConditionEffects) {
            if (!SteamHelper.IsDLCOwned(SteamHelper.DLC.SnowFallDLC)) {
                newStrongerRoadConditionEffects = false;
            }

            strongerRoadConditionEffects = newStrongerRoadConditionEffects;
            if (strongerRoadConditionEffectsToggle != null)
                strongerRoadConditionEffectsToggle.isChecked = newStrongerRoadConditionEffects;
        }

		public static void setProhibitPocketCars(bool newValue) {
			bool valueChanged = newValue != parkingAI;
			parkingAI = newValue;
			if (prohibitPocketCarsToggle != null)
				prohibitPocketCarsToggle.isChecked = newValue;
		}

        public static void setRealisticPublicTransport(bool newValue) {
            bool valueChanged = newValue != realisticPublicTransport;
            realisticPublicTransport = newValue;
            if (realisticPublicTransportToggle != null)
                realisticPublicTransportToggle.isChecked = newValue;
        }

        public static void setIndividualDrivingStyle(bool newValue) {
            individualDrivingStyle = newValue;

            if (individualDrivingStyleToggle != null) {
                individualDrivingStyleToggle.isChecked = newValue;
            }
        }

        public static void setDisableDespawning(bool value) {
            disableDespawning = value;

            if (disableDespawningToggle != null)
                disableDespawningToggle.isChecked = value;
        }

        public static void setBanRegularTrafficOnBusLanes(bool value) {
            banRegularTrafficOnBusLanes = value;

            if (OptionsVehicleRestrictionsTab.banRegularTrafficOnBusLanesToggle != null) {
                OptionsVehicleRestrictionsTab.banRegularTrafficOnBusLanesToggle.isChecked = value;
            }

            VehicleRestrictionsManager.Instance.ClearCache();
            UIBase.GetTrafficManagerTool(false)?.InitializeSubTools();
        }

        public static void setScanForKnownIncompatibleMods(bool value) {
            scanForKnownIncompatibleModsEnabled = value;
            if (scanForKnownIncompatibleModsToggle != null) {
                scanForKnownIncompatibleModsToggle.isChecked = value;
            }
            if (!value) {
                setIgnoreDisabledMods(false);
            }
        }

        public static void setIgnoreDisabledMods(bool value) {
            ignoreDisabledModsEnabled = value;
            if (ignoreDisabledModsToggle != null) {
                ignoreDisabledModsToggle.isChecked = value;
            }
        }
        /*internal static int getLaneChangingRandomizationTargetValue() {
			int ret = 100;
			switch (laneChangingRandomization) {
				case 0:
					ret = 2;
					break;
				case 1:
					ret = 4;
					break;
				case 2:
					ret = 10;
					break;
				case 3:
					ret = 20;
					break;
				case 4:
					ret = 50;
					break;
			}
			return ret;
		}*/

        /*internal static float getLaneChangingProbability() {
			switch (laneChangingRandomization) {
				case 0:
					return 0.5f;
				case 1:
					return 0.25f;
				case 2:
					return 0.1f;
				case 3:
					return 0.05f;
				case 4:
					return 0.01f;
			}
			return 0.01f;
		}*/

        internal static int getRecklessDriverModulo() {
            switch (recklessDrivers) {
                case 0:
                    return 10;
                case 1:
                    return 20;
                case 2:
                    return 50;
                case 3:
                    return 10000;
            }
            return 10000;
        }

        /// <summary>
        /// Determines whether Dynamic Lane Selection (DLS) is enabled.
        /// </summary>
        /// <returns></returns>
        public static bool IsDynamicLaneSelectionActive() {
            return advancedAI && altLaneSelectionRatio > 0;
        }
    }
}
