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

    // Likely to change or be removed in future
    [Flags]
    public enum Scope {
        None = 0,
        Global = 1,
        Savegame = 2,
        GlobalOrSavegame = Global | Savegame,
    }

    public class SavedGameOptions {
        public bool individualDrivingStyle;
        public RecklessDrivers recklessDrivers;

        /// <summary>Option: buses may ignore lane arrows.</summary>
        public bool relaxedBusses;

        /// <summary>debug option: all vehicles may ignore lane arrows.</summary>
        public bool allRelaxed;
        public bool evacBussesMayIgnoreRules;
        public bool prioritySignsOverlay;
        public bool timedLightsOverlay;
        public bool speedLimitsOverlay;
        public bool vehicleRestrictionsOverlay;
        public bool parkingRestrictionsOverlay;
        public bool junctionRestrictionsOverlay;
        public bool connectedLanesOverlay;
#if QUEUEDSTATS
        public bool showPathFindStats = VersionUtil.IS_DEBUG;
#endif

        public bool nodesOverlay;
        public bool vehicleOverlay;
        public bool citizenOverlay;
        public bool buildingOverlay;

        public bool allowEnterBlockedJunctions;
        public bool allowUTurns;
        public bool allowNearTurnOnRed;
        public bool allowFarTurnOnRed;
        public bool allowLaneChangesWhileGoingStraight;
        public bool trafficLightPriorityRules;
        public bool banRegularTrafficOnBusLanes;
        public bool advancedAI;
        public SimulationAccuracy simulationAccuracy;
        public bool realisticPublicTransport;
        public byte altLaneSelectionRatio;
        public bool highwayRules;
        public bool highwayMergingRules;
        public bool automaticallyAddTrafficLightsIfApplicable;
        public bool NoDoubleCrossings;
        public bool DedicatedTurningLanes;

        public bool showLanes = VersionUtil.IS_DEBUG;

        public bool strongerRoadConditionEffects;
        public bool parkingAI;
        public bool disableDespawning;
        public bool preferOuterLane;
        //public byte publicTransportUsage = 1;

        public bool prioritySignsEnabled;
        public bool timedLightsEnabled;
        public bool customSpeedLimitsEnabled;
        public bool vehicleRestrictionsEnabled;
        public bool parkingRestrictionsEnabled;
        public bool junctionRestrictionsEnabled;
        public bool turnOnRedEnabled;
        public bool laneConnectorEnabled;

        public VehicleRestrictionsAggression vehicleRestrictionsAggression;
        public bool RoundAboutQuickFix_DedicatedExitLanes;
        public bool RoundAboutQuickFix_StayInLaneMainR;
        public bool RoundAboutQuickFix_StayInLaneNearRabout;
        public bool RoundAboutQuickFix_NoCrossMainR;
        public bool RoundAboutQuickFix_NoCrossYieldR;
        public bool RoundAboutQuickFix_PrioritySigns;
        public bool RoundAboutQuickFix_KeepClearYieldR;
        public bool RoundAboutQuickFix_RealisticSpeedLimits;
        public bool RoundAboutQuickFix_ParkingBanMainR;
        public bool RoundAboutQuickFix_ParkingBanYieldR;
        public bool PriorityRoad_CrossMainR;
        public bool PriorityRoad_AllowLeftTurns;
        public bool PriorityRoad_EnterBlockedYeild;
        public bool PriorityRoad_StopAtEntry;

        // See PathfinderUpdates.cs
        public byte SavegamePathfinderEdition; // Persist to save-game only

        public bool showDefaultSpeedSubIcon;

        internal int getRecklessDriverModulo() => CalculateRecklessDriverModulo(recklessDrivers);

        internal static int CalculateRecklessDriverModulo(RecklessDrivers level) => level switch {
            RecklessDrivers.PathOfEvil => 10,
            RecklessDrivers.RushHour => 20,
            RecklessDrivers.MinorComplaints => 50,
            RecklessDrivers.HolyCity => 10000,
            _ => 10000,
        };

        /// <summary>
        /// Determines whether Dynamic Lane Selection (DLS) is enabled.
        /// </summary>
        public bool IsDynamicLaneSelectionActive() {
            return advancedAI && altLaneSelectionRatio > 0;
        }

        /// <summary>
        /// When <c>true</c>, options are safe to query.
        /// </summary>
        /// <remarks>
        /// Is set <c>true</c> after options are loaded via <see cref="Manager.Impl.OptionsManager"/>.
        /// Is set <c>false</c> while options are being loaded, and also when level unloads.
        /// </remarks>
        public static bool Available { get; set; } = false;

        public static SavedGameOptions Instance { get; private set; }
        public static void Ensure() {
            if (Instance == null) {
                Create();
            }
        }
        private static void Create() {
            Instance = new();
            Instance.Awake();
        }
        public static void Release() {
            Instance = null;
            Available = false;
        }

        private void Awake() {

        }
    }

    public static class TMPESettings {
        public static void MakeSettings(UIHelper helper) {
            Log.Info("SavedGameOptions.Instance.MakeSettings() - Adding UI to mod options tabs");

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
