namespace TrafficManager.State.ConfigData {
    using System;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.Traffic;
    using ExtVehicleType = TrafficManager.Traffic.ExtVehicleType;

#if DEBUG
    /// <summary>
    /// DebugSettings is a part of GlobalConfig, enabled only in Debug mode
    /// </summary>
    public class DebugSettings {
        public static DebugSwitch DebugSwitch;

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
        [Obsolete]
        public API.Traffic.Enums.ExtVehicleType ApiExtVehicleType
            => LegacyExtVehicleType.ToNew(ExtVehicleType);

        public ExtPathMode ExtPathMode = ExtPathMode.None;
    }

    [Flags]
    public enum DebugSwitch {
        None = 0,
        RoutingBasicLog = 1 << 0,
        Routing = 1 << 1,
        NoRoutingRecalculationOnConfigReload = 1 << 2, // Prevent routing recalculation on global configuration reload
        PathFindingLog = 1 << 3,
        PedestrianPathfinding = 1 << 4,
        TransportLinePathfind = 1 << 5,
        BasicParkingAILog = 1 << 6,
        ExtendedParkingAILog = 1 << 7,
        ParkingAIDistanceIssue = 1 << 8,
        VehicleParkingAILog = 1 << 9,
        NoRepairStuckVehiclesCims = 1 << 10, // Do not actually repair stuck vehicles/cims, just report
        GeometryDebug = 1 << 11,
        TimedTrafficLights = 1 << 12,
        VehicleLinkingToSegmentEnd = 1 << 13,
        JunctionRestrictions = 1 << 14,
        PriorityRules = 1 << 15,
        NoValidPathCitizensOverlay = 1 << 16, // Disable GUI overlay of citizens having a valid path
        TramsNoOtherVehiclesChecking = 1 << 17, // Disable checking of other vehicles for trams
        TramBaseAISimulationStep = 1 << 18,
        AlternativeLaneSelection = 1 << 19,
        ObligationToRHD = 1 << 19, // obligation to drive on the right hand side of the road
        RealisticPublicTransport = 1 << 20,
        CalculateSegmentPosition = 1 << 21,
        LaneConnections = 1 << 23,
        ResourceLoading = 1 << 24,
        TurnOnRed = 1 << 25,
        SpeedLimits = 1 << 26, // also lists NetInfos skipped due to m_netAI in SpeedLimitsManager.cs
        ULibraryEvents = 1 << 27, // U library UI and event
    }

    internal static class DebugSwitchExtensions {
        public static bool Get(this DebugSwitch sw) {
            return (DebugSettings.DebugSwitch & sw) != 0;
        }
    }
#endif
}