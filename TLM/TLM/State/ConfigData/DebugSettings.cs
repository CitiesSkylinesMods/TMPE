namespace TrafficManager.State.ConfigData {
    using System;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.Traffic;
    using ExtVehicleType = TrafficManager.Traffic.ExtVehicleType;

    /// <summary>
    /// DebugSettings is a part of GlobalConfig, enabled only in Debug mode
    /// </summary>
    public class DebugSettings {
#if DEBUG
        //-------------------------------------------------------
        // All fields below are loaded from a section in TMPE_GlobalConfig.xml
        //-------------------------------------------------------
        public readonly bool AlternativeLaneSelection = false;
        public readonly bool ApplyTrafficRules = false; // log calls to apply traffic rules
        public readonly bool BasicParkingAILog = false;
        public readonly bool CalculateSegmentPosition = false;
        public readonly bool CustomCarAI = false;
        public readonly bool ExtendedParkingAILog = false;
        public readonly bool GeometryDebug = false;
        public readonly bool Guide = false;
        public readonly bool JunctionRestrictions = false;
        public readonly bool LaneConnections = false;
        public readonly bool NoRoutingRecalculationOnConfigReload = false;
        public readonly bool NoValidPathCitizensOverlay = false; // disable GUI overlay of citizens having a valid path
        public readonly bool ParkingAIDistanceIssue = false;
        public readonly bool PathFindingLog = false;
        public readonly bool PedestrianPathfinding = false;
        public readonly bool PriorityRules = false;
        public readonly bool RealisticPublicTransport = false;
        public readonly bool Redirection = false; // debug DLL loading/redirection events
        public readonly bool ResourceLoading = false;
        public readonly bool RoadSelection = false; // debug log for road/loop selection routines
        public readonly bool Routing = false;
        public readonly bool RoutingBasicLog = false;
        public readonly bool SpeedLimits = false;
        public readonly bool TimedTrafficLights = false;
        public readonly bool TrafficLights = false;
        public readonly bool TramBaseAISimulationStep = false;
        public readonly bool TransportLinePathfind = false;
        public readonly bool TurnOnRed = false;
        public readonly bool LogUEvents = false; // debug U GUI subsystem
        public readonly bool VehicleLinkingToSegmentEnd = false;
        public readonly bool VehicleParkingAILog = false;
        // sorted A..Z
#else
        //-------------------------------------------------------
        // All properties below cannot be modified, always false
        //-------------------------------------------------------
        public bool AlternativeLaneSelection => false;
        public bool ApplyTrafficRules => false; // log calls to apply traffic rules
        public bool BasicParkingAILog => false;
        public bool CalculateSegmentPosition => false;
        public bool CustomCarAI => false;
        public bool ExtendedParkingAILog => false;
        public bool GeometryDebug => false;
        public bool Guide => false;
        public bool JunctionRestrictions => false;
        public bool LaneConnections => false;
        public bool NoRoutingRecalculationOnConfigReload => false;
        public bool NoValidPathCitizensOverlay => false; // disable GUI overlay of citizens having a valid path
        public bool ParkingAIDistanceIssue => false;
        public bool PathFindingLog => false;
        public bool PedestrianPathfinding => false;
        public bool PriorityRules => false;
        public bool RealisticPublicTransport => false;
        public bool Redirection => false; // debug DLL loading/redirection events
        public bool ResourceLoading => false;
        public bool RoadSelection => false; // debug log for road/loop selection routines
        public bool Routing => false;
        public bool RoutingBasicLog => false;
        public bool SpeedLimits => false;
        public bool TimedTrafficLights => false;
        public bool TrafficLights => false;
        public bool TramBaseAISimulationStep => false;
        public bool TransportLinePathfind => false;
        public bool TurnOnRed => false;
        public bool LogUEvents => false; // debug U GUI subsystem
        public bool VehicleLinkingToSegmentEnd => false;
        public bool VehicleParkingAILog => false;
        // sorted A..Z
#endif // DEBUG

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
}