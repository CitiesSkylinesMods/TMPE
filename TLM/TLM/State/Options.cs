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
    using TrafficManager.Custom.PathFinding;

    // Likely to change or be removed in future
    [Flags]
    public enum Scope {
        None = 0,
        Global = 1,
        Savegame = 2,
        GlobalOrSavegame = Global | Savegame,
    }

    public class SavedGameOptions {
        public bool individualDrivingStyle = true;
        public RecklessDrivers recklessDrivers = RecklessDrivers.HolyCity;

        /// <summary>Option: buses may ignore lane arrows.</summary>
        public bool relaxedBusses = true;

        /// <summary>debug option: all vehicles may ignore lane arrows.</summary>
        public bool allRelaxed;
        public bool evacBussesMayIgnoreRules;

        public bool prioritySignsOverlay = true;
        public bool timedLightsOverlay = true;
        public bool speedLimitsOverlay = true;
        public bool vehicleRestrictionsOverlay = true;
        public bool parkingRestrictionsOverlay = true;
        public bool junctionRestrictionsOverlay = true;
        public bool connectedLanesOverlay = true;
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
        public SimulationAccuracy simulationAccuracy = SimulationAccuracy.VeryHigh;
        public bool realisticPublicTransport;
        public byte altLaneSelectionRatio;
        public bool highwayRules;
        public bool highwayMergingRules;
        public bool automaticallyAddTrafficLightsIfApplicable = true;
        public bool NoDoubleCrossings;
        public bool DedicatedTurningLanes;

        public bool showLanes = VersionUtil.IS_DEBUG;

        public bool strongerRoadConditionEffects;
        public bool parkingAI;
        public bool disableDespawning;
        public bool preferOuterLane;
        //public byte publicTransportUsage = 1;

        public bool prioritySignsEnabled = true;
        public bool timedLightsEnabled = true;
        public bool customSpeedLimitsEnabled = true;
        public bool vehicleRestrictionsEnabled = true;
        public bool parkingRestrictionsEnabled = true;
        public bool junctionRestrictionsEnabled = true;
        public bool turnOnRedEnabled = true;
        public bool laneConnectorEnabled = true;

        public VehicleRestrictionsAggression vehicleRestrictionsAggression = VehicleRestrictionsAggression.Medium;
        public bool RoundAboutQuickFix_DedicatedExitLanes = true;
        public bool RoundAboutQuickFix_StayInLaneMainR = true;
        public bool RoundAboutQuickFix_StayInLaneNearRabout = true;
        public bool RoundAboutQuickFix_NoCrossMainR = true;
        public bool RoundAboutQuickFix_NoCrossYieldR = false;
        public bool RoundAboutQuickFix_PrioritySigns = true;
        public bool RoundAboutQuickFix_KeepClearYieldR = true;
        public bool RoundAboutQuickFix_RealisticSpeedLimits;
        public bool RoundAboutQuickFix_ParkingBanMainR = true;
        public bool RoundAboutQuickFix_ParkingBanYieldR;
        public bool PriorityRoad_CrossMainR;
        public bool PriorityRoad_AllowLeftTurns;
        public bool PriorityRoad_EnterBlockedYeild;
        public bool PriorityRoad_StopAtEntry;

        // See PathfinderUpdates.cs
        public byte SavegamePathfinderEdition = PathfinderUpdates.LatestPathfinderEdition;

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
