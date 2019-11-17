namespace TrafficManager.State {
    using System.Collections.Generic;
    using System.Reflection;
    using API.Traffic.Enums;
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using ICities;
    using UI;
    using UnityEngine;
    using Manager.Impl;

    public class Options : MonoBehaviour {
#if DEBUG
        private static List<UICheckBox> debugSwitchFields = new List<UICheckBox>();
        private static List<UITextField> debugValueFields = new List<UITextField>();

        // private static UITextField pathCostMultiplicatorField = null;
        // private static UITextField pathCostMultiplicator2Field = null;
#endif

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
        public static bool automaticallyAddTrafficLightsIfApplicable = true;
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

        /// <summary>
        /// Invoked on options change to refresh the main menu and possibly update the labels for
        /// a new language. Takes a second, very slow.
        /// </summary>
        internal static void RebuildMenu() {
            if (LoadingExtension.BaseUI != null) {
                Log._Debug("Rebuilding the TM:PE menu...");
                LoadingExtension.BaseUI.RebuildMenu();

                // TM:PE main button also needs to be uidated
                if (LoadingExtension.BaseUI.MainMenuButton != null) {
                    LoadingExtension.BaseUI.MainMenuButton.UpdateTooltip();
                }

                LoadingExtension.TranslationDatabase.ReloadTutorialTranslations();
            } else {
                Log._Debug("Rebuilding the TM:PE menu: ignored, BaseUI is null");
            }
        }

        public static void MakeSettings(UIHelperBase helper) {
            //ExtUITabstrip.Test.OnSettingsUI(helper);return;
            ExtUITabstrip tabStrip = ExtUITabstrip.Create(helper);
            OptionsGeneralTab.MakeSettings_General(tabStrip);
            OptionsGameplayTab.MakeSettings_Gameplay(tabStrip);
            OptionsVehicleRestrictionsTab.MakeSettings_VehicleRestrictions(tabStrip);
            OptionsOverlaysTab.MakeSettings_Overlays(tabStrip);
            OptionsMaintenanceTab.MakeSettings_Maintenance(tabStrip);
            OptionsKeybindsTab.MakeSettings_Keybinds(tabStrip);
            tabStrip.Invalidate();
        }

        public abstract class SerializableOptionBase {
            public abstract void Load(byte data);
            public abstract byte Save();
        }

        public abstract class SerializableUIOptionBase<TVal, TUI> : SerializableOptionBase
            where TUI : UIComponent
        {
            protected TVal _value;
            public readonly TVal DefaultValue;
            public TVal Value { get => _value; }

            public abstract void AddUI(UIHelperBase container);

            public abstract void SetValue(TVal newVal);

            protected TUI _ui;
            protected readonly bool _tooltip;
            public string Key;
            public string GroupName;
            public string Label { get => $"{GroupName}.CheckBox: {Key}"; }
            public string Tooltip { get => $"{GroupName}.Tooltip: {Key}"; }

            public void DefaultOnValueChanged(TVal newVal) {
                Options.IsGameLoaded();
                Log._Debug($"{GroupName}.{Label} changed to {newVal}");
                _value = newVal;
            }

            public SerializableUIOptionBase(
                string key,
                TVal default_value,
                string group_name,
                bool tooltip = false)
            {
                Key = key;
                DefaultValue = _value =  default_value;
                GroupName = group_name;
                _tooltip = tooltip;
            }
        }

        public sealed class CheckboxOption : SerializableUIOptionBase<bool, UICheckBox> {
            public event ICities.OnCheckChanged OnValueChanged;
            public CheckboxOption(
                string key,
                bool default_value,
                string group_name,
                bool tooltip = false) :
                base( key, default_value, group_name, tooltip) {
                OnValueChanged = DefaultOnValueChanged;
            }

            public override void Load(byte data) => SetValue(data != 0);
            public override byte Save() => Value ? (byte)1 : (byte)0;
            public override void SetValue(bool newVal) {
                if (_ui != null) {
                    _ui.isChecked = newVal;
                }
                _value = newVal;
            }

            public override void AddUI(UIHelperBase container) {
                _ui = container.AddCheckbox(
                    Translation.Options.Get(Label),
                    DefaultValue,
                    this.OnValueChanged) as UICheckBox;
                if (_tooltip) {
                    _ui.tooltip = Translation.Options.Get(Tooltip);
                }
            }
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
                    Translation.Options.Get("Dialog.Text:Settings are stored in savegame")
                    + " https://www.viathinksoft.de/tmpe/#options",
                    false);
            }

            return false;
        }

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

        /// <summary>
        /// Inform the main Options window of the C:S about language change. This should rebuild the
        /// options tab for TM:PE.
        /// </summary>
        public static void RebuildOptions() {
            // Inform the Main Options Panel about locale change, recreate the categories
            MethodInfo onChangedHandler = typeof(OptionsMainPanel)
                .GetMethod(
                    "OnLocaleChanged",
                    BindingFlags.Instance | BindingFlags.NonPublic);
            if (onChangedHandler == null) {
                Log.Error("Cannot rebuild options panel, OnLocaleChanged handler is null");
                return;
            }

            Log._Debug("Informing the main OptionsPanel about the locale change...");
            onChangedHandler.Invoke(
                UIView.library.Get<OptionsMainPanel>("OptionsPanel"),
                new object[] { });
        }
    }
}
