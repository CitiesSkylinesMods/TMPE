namespace TrafficManager.State.ConfigData {
    using ExtVehicleType = TrafficManager.Traffic.ExtVehicleType;
    using JetBrains.Annotations;
    using System;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.Traffic;

#if DEBUG
    /// <summary>
    /// DebugSettings is a part of GlobalConfig, enabled only in Debug mode
    /// </summary>
    public class DebugSettings {
        /// <summary>
        /// Do not use directly.
        /// Use DebugSwitch.$EnumName$.Get() to access the switch values.
        /// </summary>
        public bool[] Switches = {
            false, // 0: path-finding debug log
            false, // 1: routing basic debug log
            false, // 2: parking ai debug log (basic)
            false, // 3: do not actually repair stuck vehicles/cims, just report
            false, // 4: parking ai debug log (extended)
            false, // 5: geometry debug log
            false, // 6: debug parking AI distance issue
            false, // 7: debug Timed Traffic Lights
            false, // 8: debug routing
            false, // 9: debug vehicle to segment end linking
            false, // 10: prevent routing recalculation on global configuration reload
            false, // 11: debug junction restrictions
            false, // 12: debug pedestrian pathfinding
            false, // 13: priority rules debug
            false, // 14: disable GUI overlay of citizens having a valid path
            false, // 15: disable checking of other vehicles for trams
            false, // 16: debug TramBaseAI.SimulationStep (2)
            false, // 17: debug alternative lane selection
            false, // 18: transport line path-find debugging
            false, // 19: enable obligation to drive on the right hand side of the road
            false, // 20: debug realistic public transport
            false, // 21: debug "CalculateSegmentPosition"
            false, // 22: parking ai debug log (vehicles)
            false, // 23: debug lane connections
            false, // 24: debug resource loading
            false, // 25: debug turn-on-red
            false  // 26: debug speed limits (also lists NetInfos skipped due to m_netAI in SpeedLimitsManager.cs)
        };

        private int nodeId_ = 0;

        public static int NodeId => GlobalConfig.Instance.Debug.nodeId_;

        private int segmentId_ = 0;

        public static int SegmentId => GlobalConfig.Instance.Debug.segmentId_;

        private int startSegmentId_ = 0;

        public static int StartSegmentId => GlobalConfig.Instance.Debug.startSegmentId_;

        private int endSegmentId_ = 0;

        public static int EndSegmentId => GlobalConfig.Instance.Debug.endSegmentId_;

        private int vehicleId_ = 0;

        public static int VehicleId => GlobalConfig.Instance.Debug.vehicleId_;

        private int citizenInstanceId_ = 0;

        public static int CitizenInstanceId => GlobalConfig.Instance.Debug.citizenInstanceId_;

        private uint citizenId_ = 0;

        public static uint CitizenId => GlobalConfig.Instance.Debug.citizenId_;

        private uint sourceBuildingId_ = 0;

        public static uint SourceBuildingId => GlobalConfig.Instance.Debug.sourceBuildingId_;

        private uint targetBuildingId_ = 0;

        public static uint TargetBuildingId => GlobalConfig.Instance.Debug.targetBuildingId_;

        [Obsolete]
        public ExtVehicleType ExtVehicleType = ExtVehicleType.None;

        /// <summary>
        /// This adds access to the new moved type from the compatible old field
        /// </summary>
        // Property will not be serialized, permit use of obsolete symbol
#pragma warning disable 612
        public API.Traffic.Enums.ExtVehicleType ApiExtVehicleType
            => LegacyExtVehicleType.ToNew(ExtVehicleType);
#pragma warning restore 612

        public ExtPathMode ExtPathMode = ExtPathMode.None;
    }

    /// <summary>
    /// Indexes into Debug.Switches
    /// </summary>
    public enum DebugSwitch {
        PathFindingLog = 0,
        RoutingBasicLog = 1,
        BasicParkingAILog = 2,
        [UsedImplicitly]
        NoRepairStuckVehiclesCims = 3,
        ExtendedParkingAILog = 4,
        GeometryDebug = 5,
        ParkingAIDistanceIssue = 6,
        TimedTrafficLights = 7,
        Routing = 8,
        VehicleLinkingToSegmentEnd = 9,
        NoRoutingRecalculationOnConfigReload = 10,
        JunctionRestrictions = 11,
        PedestrianPathfinding = 12,
        PriorityRules = 13,
        NoValidPathCitizensOverlay = 14,
        [UsedImplicitly]
        TramsNoOtherVehiclesChecking = 15,
        TramBaseAISimulationStep = 16,
        AlternativeLaneSelection = 17,
        TransportLinePathfind = 18,
        [UsedImplicitly]
        ObligationToRHD = 19,
        RealisticPublicTransport = 20,
        CalculateSegmentPosition = 21,
        VehicleParkingAILog = 22,
        LaneConnections = 23,
        ResourceLoading = 24,
        TurnOnRed = 25,
        SpeedLimits = 26
    }

    static class DebugSwitchExtensions {
        public static bool Get(this DebugSwitch sw) {
            return GlobalConfig.Instance.Debug.Switches[(int)sw];
        }
    }
#endif
}