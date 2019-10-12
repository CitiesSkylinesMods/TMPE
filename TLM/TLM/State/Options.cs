namespace TrafficManager.State {
    using System.Collections.Generic;
    using System.Reflection;
    using API.Traffic.Enums;
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using ICities;
    using UI;
    using UnityEngine;

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
            OptionsGeneralTab.MakeSettings_General(tabStrip, tabIndex);

            // GAMEPLAY
            ++tabIndex;
            OptionsGameplayTab.MakeSettings_Gameplay(tabStrip, tabIndex);

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
