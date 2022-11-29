namespace TrafficManager.State.ConfigData {
    using CSUtil.Commons;
    using System;
    using System.Xml.Serialization;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.Traffic;
    using ExtVehicleType = TrafficManager.Traffic.ExtVehicleType;

#if DEBUG
    /// <summary>
    /// DebugSettings is a part of GlobalConfig, enabled only in Debug mode
    /// </summary>
    public class DebugSettings {
        private static DebugSwitch _debugSwitches;
        public static DebugSwitch DebugSwitches {
            get => _debugSwitches;
            set {
                _debugSwitches = value;
                Log._Debug("DebugSettings.DebugSwitches set to " + DebugSwitches);
            }
        }
        private static InstanceID SelectedInstance => InstanceManager.instance.GetSelectedInstance();
        private static DebugSettings Instance => GlobalConfig.Instance.Debug;
        private static ushort Get(ushort val1, ushort val2) => val1 != 0 ? val1 : val2;
        private static uint Get(uint val1, uint val2) => val1 != 0 ? val1 : val2;

        public static ushort NodeId => SelectedInstance.NetNode;

        public static ushort SegmentId => SelectedInstance.NetSegment;

        [XmlElement("StartSegmentId")]
        public ushort SavedStartSegmentId = 0;
        public static ushort StartSegmentId => Get(Instance.SavedStartSegmentId, SegmentId);

        [XmlElement("EndSegmentId")]
        public ushort SavedEndSegmentId = 0;
        public static int EndSegmentId => Get(Instance.SavedEndSegmentId, SegmentId);

        public static int VehicleId => SelectedInstance.Vehicle;

        public static int CitizenInstanceId => SelectedInstance.CitizenInstance;

        public static uint CitizenId => SelectedInstance.Citizen;

        [XmlElement("SourceBuildingId")]
        public uint SavedSourceBuildingId = 0;
        public static uint SourceBuildingId => Get(Instance.SavedSourceBuildingId, SelectedInstance.Building);

        [XmlElement("TargetBuildingId")]
        public uint SavedTargetBuildingId = 0;
        public static uint TargetBuildingId => Get(Instance.SavedTargetBuildingId, SelectedInstance.Building);

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
            return (DebugSettings.DebugSwitches & sw) != 0;
        }
    }
#endif
}