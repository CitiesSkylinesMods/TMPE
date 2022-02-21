namespace TrafficManager.State {
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using ICities;
    using TrafficManager.Manager.Impl;
    using TrafficManager.UI.Helpers;
    using TrafficManager.UI;
    using UnityEngine;
    using TrafficManager.Lifecycle;

    public static class GameplayTab {
        private static UICheckBox _prohibitPocketCarsToggle;
        private static UICheckBox _advancedAIToggle;
        private static UICheckBox _realisticPublicTransportToggle;
        private static UISlider _altLaneSelectionRatioSlider;

        internal static void MakeSettings_Gameplay(ExtUITabstrip tabStrip) {
            UIHelper panelHelper = tabStrip.AddTabPage(Translation.Options.Get("Tab:Gameplay"));

            GameplayTab_VehicleBehaviourGroup.AddUI(panelHelper);

            UIHelperBase vehAiGroup = panelHelper.AddGroup(
                Translation.Options.Get("Gameplay.Group:Advanced vehicle AI"));
            _advancedAIToggle = vehAiGroup.AddCheckbox(
                                   Translation.Options.Get("Gameplay.Checkbox:Enable advanced vehicle AI"),
                                   Options.advancedAI,
                                   OnAdvancedAiChanged) as UICheckBox;
            _altLaneSelectionRatioSlider
                = vehAiGroup.AddSlider(
                      Translation.Options.Get("Gameplay.Slider:Dynamic lane selection") + ":",
                      0,
                      100,
                      5,
                      Options.altLaneSelectionRatio,
                      OnAltLaneSelectionRatioChanged) as UISlider;
            _altLaneSelectionRatioSlider.parent.Find<UILabel>("Label").width = 450;

            UIHelperBase parkAiGroup = panelHelper.AddGroup(
                Translation.Options.Get("Gameplay.Group:Parking AI"));
            _prohibitPocketCarsToggle
                = parkAiGroup.AddCheckbox(
                      Translation.Options.Get("Gameplay.Checkbox:Enable more realistic parking"),
                      Options.parkingAI,
                      OnProhibitPocketCarsChanged) as UICheckBox;

            UIHelperBase ptGroup = panelHelper.AddGroup(
                Translation.Options.Get("Gameplay.Group:Public transport"));
            _realisticPublicTransportToggle
                = ptGroup.AddCheckbox(
                      Translation.Options.Get("Gameplay.Checkbox:No excessive transfers"),
                      Options.realisticPublicTransport,
                      OnRealisticPublicTransportChanged) as UICheckBox;
            CheckboxOption.ApplyTextWrap(_realisticPublicTransportToggle);
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

        private static void OnAltLaneSelectionRatioChanged(float newVal) {
            if (!Options.IsGameLoaded()) {
                return;
            }

            SetDLSPercentage((byte)Mathf.RoundToInt(newVal));
            _altLaneSelectionRatioSlider.tooltip =
                Translation.Options.Get("Gameplay.Tooltip:DLS_percentage") + ": " +
                Options.altLaneSelectionRatio + " %";

            // Only call this if the game is running, not during the loading time
            if (TMPELifecycle.Instance.IsGameLoaded) {
                _altLaneSelectionRatioSlider.RefreshTooltip();
            }

            Log._Debug($"altLaneSelectionRatio changed to {Options.altLaneSelectionRatio}");
        }

        public static void SetDLSPercentage(byte val) {
            bool changed = val != Options.altLaneSelectionRatio;
            Options.altLaneSelectionRatio = val;

            if (changed && _altLaneSelectionRatioSlider != null) {
                _altLaneSelectionRatioSlider.value = val;
            }

            if (changed && Options.altLaneSelectionRatio > 0) {
                SetAdvancedAi(true);
            }
        }

        public static void SetAdvancedAi(bool newAdvancedAi) {
            bool changed = newAdvancedAi != Options.advancedAI;
            Options.advancedAI = newAdvancedAi;

            if (changed && _advancedAIToggle != null) {
                _advancedAIToggle.isChecked = newAdvancedAi;
            }

            if (changed && !newAdvancedAi) {
                SetDLSPercentage(0);
            }
        }

        public static void SetProhibitPocketCars(bool newValue) {
            // bool valueChanged = newValue != Options.parkingAI;
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
    } // end class
}
