namespace TrafficManager.State {
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using System.Collections.Generic;
    using System.Reflection;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.UI.Helpers;
    using TrafficManager.UI;
    using TrafficManager.Util;
    using UnityEngine;
    using TrafficManager.Lifecycle;
    using System;

    public class Options : MonoBehaviour {
#if DEBUG
        private static List<UICheckBox> debugSwitchFields = new List<UICheckBox>();
        private static List<UITextField> debugValueFields = new List<UITextField>();

        // private static UITextField pathCostMultiplicatorField = null;
        // private static UITextField pathCostMultiplicator2Field = null;
#endif

        // Likely to change or be removed in future
        [Flags]
        public enum PersistTo {
            None = 0,
            Global = 1,
            Savegame = 2,
            GlobalOrSavegame = Global | Savegame,
        }

        /// <summary>
        /// When <c>true</c>, options are safe to query.
        /// </summary>
        /// <remarks>
        /// Is set <c>true</c> after options are loaded via <see cref="Manager.Impl.OptionsManager"/>.
        /// Is set <c>false</c> while options are being loaded, and also when level unloads.
        /// </remarks>
        public static bool Available = false;

        // OptionsManager.CompileOverlaysFlags() sets this value
        // based on other mod options. Not currently persisted,
        // but could be in future.
        public static Overlays PersistentOverlays;

        public static bool individualDrivingStyle;
        public static int recklessDrivers;

        /// <summary>Option: buses may ignore lane arrows.</summary>
        public static bool relaxedBusses;

        /// <summary>debug option: all vehicles may ignore lane arrows.</summary>
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
        public static bool showPathFindStats = VersionUtil.IS_DEBUG;
#endif

        public static bool nodesOverlay;
        public static bool vehicleOverlay;
        public static bool citizenOverlay;
        public static bool buildingOverlay;

        public static bool allowEnterBlockedJunctions;
        public static bool allowUTurns;
        public static bool allowNearTurnOnRed;
        public static bool allowFarTurnOnRed;
        public static bool allowLaneChangesWhileGoingStraight;
        public static bool trafficLightPriorityRules;
        public static bool banRegularTrafficOnBusLanes;
        public static bool advancedAI;
        public static SimulationAccuracy simulationAccuracy;
        public static bool realisticPublicTransport;
        public static byte altLaneSelectionRatio;
        public static bool highwayRules;
        public static bool automaticallyAddTrafficLightsIfApplicable;
        public static bool NoDoubleCrossings;
        public static bool DedicatedTurningLanes;

        public static bool showLanes = VersionUtil.IS_DEBUG;

        public static bool strongerRoadConditionEffects;
        public static bool parkingAI;
        public static bool disableDespawning;
        public static bool preferOuterLane;
        //public static byte publicTransportUsage = 1;

        public static bool prioritySignsEnabled;
        public static bool timedLightsEnabled;
        public static bool customSpeedLimitsEnabled;
        public static bool vehicleRestrictionsEnabled;
        public static bool parkingRestrictionsEnabled;
        public static bool junctionRestrictionsEnabled;
        public static bool turnOnRedEnabled;
        public static bool laneConnectorEnabled;

        public static VehicleRestrictionsAggression vehicleRestrictionsAggression =
            VehicleRestrictionsAggression.Medium;

        public static bool RoundAboutQuickFix_DedicatedExitLanes;
        public static bool RoundAboutQuickFix_StayInLaneMainR;
        public static bool RoundAboutQuickFix_StayInLaneNearRabout;
        public static bool RoundAboutQuickFix_NoCrossMainR;
        public static bool RoundAboutQuickFix_NoCrossYieldR;
        public static bool RoundAboutQuickFix_PrioritySigns;
        public static bool RoundAboutQuickFix_KeepClearYieldR;
        public static bool RoundAboutQuickFix_RealisticSpeedLimits;
        public static bool RoundAboutQuickFix_ParkingBanMainR;
        public static bool RoundAboutQuickFix_ParkingBanYieldR;
        public static bool PriorityRoad_CrossMainR;
        public static bool PriorityRoad_AllowLeftTurns;
        public static bool PriorityRoad_EnterBlockedYeild;
        public static bool PriorityRoad_StopAtEntry;

        // See PathfinderUpdates.cs
        public static byte SavegamePathfinderEdition;

        public static bool showDefaultSpeedSubIcon;

        public static void MakeSettings(UIHelper helper) {
            Log.Info("Options.MakeSettings() - Adding UI to mod options tabs");

            try {
                ExtUITabstrip tabStrip = ExtUITabstrip.Create(helper);
                GeneralTab.MakeSettings_General(tabStrip);
                GameplayTab.MakeSettings_Gameplay(tabStrip);
                PoliciesTab.MakeSettings_VehicleRestrictions(tabStrip);
                OverlaysTab.MakeSettings_Overlays(tabStrip);
                MaintenanceTab.MakeSettings_Maintenance(tabStrip);
                KeybindsTab.MakeSettings_Keybinds(tabStrip);
                tabStrip.Invalidate();
            } catch (Exception ex) {
                ex.LogException();
            }
        }

        /// <summary>
        /// If the game is not loaded and warn is true, will display a warning about options being
        /// local to each savegame.
        /// </summary>
        /// <param name="warn">Whether to display a warning popup</param>
        /// <returns>The game is loaded</returns>
        internal static bool IsGameLoaded(bool warn = true) {
            if (TMPELifecycle.InGameOrEditor()) {
                return true;
            }

            if (warn) {
                Prompt.Warning(
                    "Nope!",
                    Translation.Options.Get("Dialog.Text:Settings are stored in savegame")
                    + " https://github.com/CitiesSkylinesMods/TMPE/wiki/Settings");
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
