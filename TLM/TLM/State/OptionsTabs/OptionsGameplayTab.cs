namespace TrafficManager.State {
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using ICities;
    using TrafficManager.Lifecycle;
    using TrafficManager.Manager.Impl;
    using TrafficManager.UI;
    using TrafficManager.UI.Helpers;
    using UnityEngine;
    using Debug = System.Diagnostics.Debug;

    public static class OptionsGameplayTab {
        private static UICheckBox individualDrivingStyleToggle_;
        private static UIDropDown recklessDriversDropdown_;
        private static UICheckBox disableDespawningToggle_;

        private static UICheckBox strongerRoadConditionEffectsToggle_;
        private static UICheckBox prohibitPocketCarsToggle_;
        private static UICheckBox advancedAIToggle_;
        private static UICheckBox realisticPublicTransportToggle_;
        private static UISlider altLaneSelectionRatioSlider_;

        internal static void MakeSettings_Gameplay(ExtUITabstrip tabStrip) {
            UIHelper panelHelper = tabStrip.AddTabPage(Translation.Options.Get("Tab:Gameplay"));

            UIHelperBase vehBehaviorGroup = panelHelper.AddGroup(
                Translation.Options.Get("Gameplay.Group:Vehicle behavior"));

            recklessDriversDropdown_
                = vehBehaviorGroup.AddDropdown(
                      Translation.Options.Get("Gameplay.Dropdown:Reckless drivers%") + ":",
                      new[] {
                                Translation.Options.Get("Gameplay.Dropdown.Option:Path Of Evil (10%)"),
                                Translation.Options.Get("Gameplay.Dropdown.Option:Rush Hour (5%)"),
                                Translation.Options.Get("Gameplay.Dropdown.Option:Minor Complaints (2%)"),
                                Translation.Options.Get("Gameplay.Dropdown.Option:Holy City (0%)"),
                      },
                      Options.recklessDrivers,
                      OnRecklessDriversChanged) as UIDropDown;

            Debug.Assert(recklessDriversDropdown_ != null, nameof(recklessDriversDropdown_) + " != null");
            recklessDriversDropdown_.width = 350;

            individualDrivingStyleToggle_
                = vehBehaviorGroup.AddCheckbox(
                      Translation.Options.Get("Gameplay.Checkbox:Individual driving styles"),
                      Options.individualDrivingStyle,
                      OnIndividualDrivingStyleChanged) as UICheckBox;

            if (SteamHelper.IsDLCOwned(SteamHelper.DLC.SnowFallDLC)) {
                strongerRoadConditionEffectsToggle_
                    = vehBehaviorGroup.AddCheckbox(
                          Translation.Options.Get("Gameplay.Checkbox:Increase road condition impact"),
                          Options.strongerRoadConditionEffects,
                          OnStrongerRoadConditionEffectsChanged) as UICheckBox;
            }

            // TODO: Duplicates main menu button function
            disableDespawningToggle_ = vehBehaviorGroup.AddCheckbox(
                                          Translation.Options.Get("Maintenance.Checkbox:Disable despawning"),
                                          Options.disableDespawning,
                                          OnDisableDespawningChanged) as UICheckBox;

            UIHelperBase vehAiGroup = panelHelper.AddGroup(
                Translation.Options.Get("Gameplay.Group:Advanced vehicle AI"));
            advancedAIToggle_ = vehAiGroup.AddCheckbox(
                                   Translation.Options.Get("Gameplay.Checkbox:Enable advanced vehicle AI"),
                                   Options.advancedAI,
                                   OnAdvancedAiChanged) as UICheckBox;
            altLaneSelectionRatioSlider_
                = vehAiGroup.AddSlider(
                      text: Translation.Options.Get("Gameplay.Slider:Dynamic lane selection") + ":",
                      min: 0,
                      max: 100,
                      step: 5,
                      defaultValue: Options.altLaneSelectionRatio,
                      eventCallback: OnAltLaneSelectionRatioChanged) as UISlider;

            Debug.Assert(altLaneSelectionRatioSlider_ != null, nameof(altLaneSelectionRatioSlider_) + " != null");
            altLaneSelectionRatioSlider_.parent.Find<UILabel>("Label").width = 450;

            UIHelperBase parkAiGroup = panelHelper.AddGroup(
                Translation.Options.Get("Gameplay.Group:Parking AI"));
            prohibitPocketCarsToggle_
                = parkAiGroup.AddCheckbox(
                      Translation.Options.Get("Gameplay.Checkbox:Enable more realistic parking"),
                      Options.parkingAI,
                      OnProhibitPocketCarsChanged) as UICheckBox;

            UIHelperBase ptGroup = panelHelper.AddGroup(
                Translation.Options.Get("Gameplay.Group:Public transport"));
            realisticPublicTransportToggle_
                = ptGroup.AddCheckbox(
                      Translation.Options.Get("Gameplay.Checkbox:No excessive transfers"),
                      Options.realisticPublicTransport,
                      OnRealisticPublicTransportChanged) as UICheckBox;
        }

        private static void OnRecklessDriversChanged(int newRecklessDrivers) {
            if (!Options.IsGameLoaded()) {
                return;
            }

            Log._Debug($"Reckless driver amount changed to {newRecklessDrivers}");
            Options.recklessDrivers = newRecklessDrivers;
        }

        private static void OnAdvancedAiChanged(bool newAdvancedAi) {
            if (!Options.IsGameLoaded()) {
                return;
            }

            Log._Debug($"advancedAI changed to {newAdvancedAi}");
            SetAdvancedAi(newAdvancedAi);
        }

        private static void OnStrongerRoadConditionEffectsChanged(bool newStrongerRoadConditionEffects) {
            if (!Options.IsGameLoaded()) {
                return;
            }

            Log._Debug($"strongerRoadConditionEffects changed to {newStrongerRoadConditionEffects}");
            Options.strongerRoadConditionEffects = newStrongerRoadConditionEffects;
        }

        private static void OnProhibitPocketCarsChanged(bool newValue) {
            if (!Options.IsGameLoaded()) {
                return;
            }

            Log._Debug($"prohibitPocketCars changed to {newValue}");
            Options.parkingAI = newValue;

            if (Options.parkingAI) {
                AdvancedParkingManager.Instance.OnEnableFeature();
            } else {
                AdvancedParkingManager.Instance.OnDisableFeature();
            }
        }

        private static void OnRealisticPublicTransportChanged(bool newValue) {
            if (!Options.IsGameLoaded()) {
                return;
            }

            Log._Debug($"realisticPublicTransport changed to {newValue}");
            Options.realisticPublicTransport = newValue;
        }

        private static void OnIndividualDrivingStyleChanged(bool value) {
            if (!Options.IsGameLoaded()) {
                return;
            }

            Log._Debug($"individualDrivingStyle changed to {value}");
            SetIndividualDrivingStyle(value);
        }

        private static void OnDisableDespawningChanged(bool value) {
            if (!Options.IsGameLoaded()) {
                return;
            }

            Log._Debug($"disableDespawning changed to {value}");
            Options.disableDespawning = value;
        }

        private static void OnAltLaneSelectionRatioChanged(float newVal) {
            if (!Options.IsGameLoaded()) {
                return;
            }

            SetDlsPercentage((byte)Mathf.RoundToInt(newVal));
            altLaneSelectionRatioSlider_.tooltip =
                Translation.Options.Get("Gameplay.Tooltip:DLS_percentage") + ": " +
                Options.altLaneSelectionRatio + " %";

            // Only call this if the game is running, not during the loading time
            if (TMPELifecycle.Instance.IsGameLoaded) {
                altLaneSelectionRatioSlider_.RefreshTooltip();
            }

            Log._Debug($"altLaneSelectionRatio changed to {Options.altLaneSelectionRatio}");
        }

        public static void SetDlsPercentage(byte val) {
            bool changed = val != Options.altLaneSelectionRatio;
            Options.altLaneSelectionRatio = val;

            if (changed && altLaneSelectionRatioSlider_ != null) {
                altLaneSelectionRatioSlider_.value = val;
            }

            if (changed && Options.altLaneSelectionRatio > 0) {
                SetAdvancedAi(true);
            }
        }

        public static void SetAdvancedAi(bool newAdvancedAi) {
            bool changed = newAdvancedAi != Options.advancedAI;
            Options.advancedAI = newAdvancedAi;

            if (changed && advancedAIToggle_ != null) {
                advancedAIToggle_.isChecked = newAdvancedAi;
            }

            if (changed && !newAdvancedAi) {
                SetDlsPercentage(0);
            }
        }

        public static void SetRecklessDrivers(int newRecklessDrivers) {
            Options.recklessDrivers = newRecklessDrivers;
            if (recklessDriversDropdown_ != null) {
                recklessDriversDropdown_.selectedIndex = newRecklessDrivers;
            }
        }

        public static void SetStrongerRoadConditionEffects(bool newStrongerRoadConditionEffects) {
            if (!SteamHelper.IsDLCOwned(SteamHelper.DLC.SnowFallDLC)) {
                newStrongerRoadConditionEffects = false;
            }

            Options.strongerRoadConditionEffects = newStrongerRoadConditionEffects;

            if (strongerRoadConditionEffectsToggle_ != null) {
                strongerRoadConditionEffectsToggle_.isChecked = newStrongerRoadConditionEffects;
            }
        }

        public static void SetProhibitPocketCars(bool newValue) {
            // bool valueChanged = newValue != Options.parkingAI;
            Options.parkingAI = newValue;

            if (prohibitPocketCarsToggle_ != null) {
                prohibitPocketCarsToggle_.isChecked = newValue;
            }
        }

        public static void SetRealisticPublicTransport(bool newValue) {
            // bool valueChanged = newValue != Options.realisticPublicTransport;
            Options.realisticPublicTransport = newValue;

            if (realisticPublicTransportToggle_ != null) {
                realisticPublicTransportToggle_.isChecked = newValue;
            }
        }

        public static void SetIndividualDrivingStyle(bool newValue) {
            Options.individualDrivingStyle = newValue;

            if (individualDrivingStyleToggle_ != null) {
                individualDrivingStyleToggle_.isChecked = newValue;
            }
        }

        public static void SetDisableDespawning(bool value) {
            Options.disableDespawning = value;

            if (disableDespawningToggle_ != null) {
                disableDespawningToggle_.isChecked = value;
            }
        }
    } // end class
}
