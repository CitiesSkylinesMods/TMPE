namespace TrafficManager.State;
using CSUtil.Commons;
using TrafficManager.API.Traffic.Enums;
using TrafficManager.Custom.PathFinding;
using TrafficManager.Util;

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
        Log.Info("SavedGameOptions.Ensure() called");
        if (Instance == null) {
            Create();
        }
    }
    private static void Create() {
        Log.Info("SavedGameOptions.Create() called");
        Instance = new();
        Instance.Awake();
    }
    public static void Release() {
        Log.Info("SavedGameOptions.Release() called");
        Instance = null;
        Available = false;
    }

    private void Awake() {
        Log.Info("SavedGameOptions.Awake() called");
    }
}
