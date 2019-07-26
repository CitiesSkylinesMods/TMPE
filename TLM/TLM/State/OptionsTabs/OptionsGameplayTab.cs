namespace TrafficManager.State {
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using ICities;
    using Manager.Impl;
    using UI;
    using UnityEngine;

    public static class OptionsGameplayTab {
        private static UICheckBox _individualDrivingStyleToggle;
        private static UIDropDown _recklessDriversDropdown;
        private static UICheckBox _disableDespawningToggle;

        private static UICheckBox _strongerRoadConditionEffectsToggle;
        private static UICheckBox _prohibitPocketCarsToggle;
        private static UICheckBox _advancedAIToggle;
        private static UICheckBox _realisticPublicTransportToggle;
        private static UISlider _altLaneSelectionRatioSlider;

        private static string T(string s) {
            return Translation.GetString(s);
        }

        internal static void MakeSettings_Gameplay(UITabstrip tabStrip, int tabIndex) {
            Options.AddOptionTab(tabStrip, T("Gameplay"));
            tabStrip.selectedIndex = tabIndex;
            var currentPanel = tabStrip.tabContainer.components[tabIndex] as UIPanel;
            currentPanel.autoLayout = true;
            currentPanel.autoLayoutDirection = LayoutDirection.Vertical;
            currentPanel.autoLayoutPadding.top = 5;
            currentPanel.autoLayoutPadding.left = 10;
            currentPanel.autoLayoutPadding.right = 10;
            var panelHelper = new UIHelper(currentPanel);
            UIHelperBase vehBehaviorGroup = panelHelper.AddGroup(T("Vehicle_behavior"));

            _recklessDriversDropdown
                = vehBehaviorGroup.AddDropdown(
                      T("Reckless_driving") + ":",
                      new[] {
                          T("Path_Of_Evil_(10_%)"), T("Rush_Hour_(5_%)"),
                          T("Minor_Complaints_(2_%)"), T("Holy_City_(0_%)")
                      },
                      Options.recklessDrivers,
                      OnRecklessDriversChanged) as UIDropDown;
            _recklessDriversDropdown.width = 300;
            _individualDrivingStyleToggle = vehBehaviorGroup.AddCheckbox(
                                               T("Individual_driving_styles"),
                                               Options.individualDrivingStyle,
                                               onIndividualDrivingStyleChanged) as UICheckBox;

            if (SteamHelper.IsDLCOwned(SteamHelper.DLC.SnowFallDLC)) {
                _strongerRoadConditionEffectsToggle
                    = vehBehaviorGroup.AddCheckbox(
                          T("Road_condition_has_a_bigger_impact_on_vehicle_speed"),
                          Options.strongerRoadConditionEffects,
                          OnStrongerRoadConditionEffectsChanged) as UICheckBox;
            }

            _disableDespawningToggle = vehBehaviorGroup.AddCheckbox(
                                          T("Disable_despawning"),
                                          Options.disableDespawning,
                                          onDisableDespawningChanged) as UICheckBox;

            UIHelperBase vehAiGroup = panelHelper.AddGroup(T("Advanced_Vehicle_AI"));
            _advancedAIToggle = vehAiGroup.AddCheckbox(
                                   T("Enable_Advanced_Vehicle_AI"),
                                   Options.advancedAI,
                                   OnAdvancedAiChanged) as UICheckBox;
            _altLaneSelectionRatioSlider = vehAiGroup.AddSlider(
                                              T("Dynamic_lane_section") + ":",
                                              0,
                                              100,
                                              5,
                                              Options.altLaneSelectionRatio,
                                              OnAltLaneSelectionRatioChanged) as UISlider;
            _altLaneSelectionRatioSlider.parent.Find<UILabel>("Label").width = 450;

            UIHelperBase parkAiGroup = panelHelper.AddGroup(T("Parking_AI"));
            _prohibitPocketCarsToggle = parkAiGroup.AddCheckbox(
                                           T("Enable_more_realistic_parking"),
                                           Options.parkingAI,
                                           OnProhibitPocketCarsChanged) as UICheckBox;

            UIHelperBase ptGroup = panelHelper.AddGroup(T("Public_transport"));
            _realisticPublicTransportToggle = ptGroup.AddCheckbox(
                                                 T(
                                                     "Prevent_excessive_transfers_at_public_transport_stations"),
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

        private static void OnAdvancedAiChanged(bool newAdvancedAI) {
            if (!Options.IsGameLoaded()) {
                return;
            }

            Log._Debug($"advancedAI changed to {newAdvancedAI}");
            SetAdvancedAi(newAdvancedAI);
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

        private static void onIndividualDrivingStyleChanged(bool value) {
            if (!Options.IsGameLoaded()) {
                return;
            }

            Log._Debug($"individualDrivingStyle changed to {value}");
            SetIndividualDrivingStyle(value);
        }

        private static void onDisableDespawningChanged(bool value) {
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

            SetAltLaneSelectionRatio((byte)Mathf.RoundToInt(newVal));
            _altLaneSelectionRatioSlider.tooltip =
                T("Percentage_of_vehicles_performing_dynamic_lane_section") + ": " +
                Options.altLaneSelectionRatio + " %";

            Log._Debug($"altLaneSelectionRatio changed to {Options.altLaneSelectionRatio}");
        }

        public static void SetAltLaneSelectionRatio(byte val) {
            bool changed = val != Options.altLaneSelectionRatio;
            Options.altLaneSelectionRatio = val;

            if (changed && _altLaneSelectionRatioSlider != null) {
                _altLaneSelectionRatioSlider.value = val;
            }

            if (changed && Options.altLaneSelectionRatio > 0) {
                SetAdvancedAi(true);
            }
        }

        public static void SetAdvancedAi(bool newAdvancedAI) {
            bool changed = newAdvancedAI != Options.advancedAI;
            Options.advancedAI = newAdvancedAI;

            if (changed && _advancedAIToggle != null) {
                _advancedAIToggle.isChecked = newAdvancedAI;
            }

            if (changed && !newAdvancedAI) {
                SetAltLaneSelectionRatio(0);
            }
        }

        public static void SetRecklessDrivers(int newRecklessDrivers) {
            Options.recklessDrivers = newRecklessDrivers;
            if (_recklessDriversDropdown != null)
                _recklessDriversDropdown.selectedIndex = newRecklessDrivers;
        }

        public static void SetStrongerRoadConditionEffects(bool newStrongerRoadConditionEffects) {
            if (!SteamHelper.IsDLCOwned(SteamHelper.DLC.SnowFallDLC)) {
                newStrongerRoadConditionEffects = false;
            }

            Options.strongerRoadConditionEffects = newStrongerRoadConditionEffects;

            if (_strongerRoadConditionEffectsToggle != null) {
                _strongerRoadConditionEffectsToggle.isChecked = newStrongerRoadConditionEffects;
            }
        }

        public static void SetProhibitPocketCars(bool newValue) {
            bool valueChanged = newValue != Options.parkingAI;
            Options.parkingAI = newValue;

            if (_prohibitPocketCarsToggle != null) {
                _prohibitPocketCarsToggle.isChecked = newValue;
            }
        }

        public static void SetRealisticPublicTransport(bool newValue) {
            bool valueChanged = newValue != Options.realisticPublicTransport;
            Options.realisticPublicTransport = newValue;

            if (_realisticPublicTransportToggle != null) {
                _realisticPublicTransportToggle.isChecked = newValue;
            }
        }

        public static void SetIndividualDrivingStyle(bool newValue) {
            Options.individualDrivingStyle = newValue;

            if (_individualDrivingStyleToggle != null) {
                _individualDrivingStyleToggle.isChecked = newValue;
            }
        }

        public static void SetDisableDespawning(bool value) {
            Options.disableDespawning = value;

            if (_disableDespawningToggle != null) {
                _disableDespawningToggle.isChecked = value;
            }
        }

    } // end class
}