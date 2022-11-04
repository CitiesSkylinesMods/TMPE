namespace TrafficManager.State.ConfigData {
    using ExtVehicleType = TrafficManager.Traffic.ExtVehicleType;
    using JetBrains.Annotations;
    using System;
    using System.Xml;
    using System.Xml.Schema;
    using System.Xml.Serialization;
    using CSUtil.Commons;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.Traffic;
    using System.Diagnostics;

#if DEBUG
    /// <summary>
    /// DebugSettings is a part of GlobalConfig, enabled only in Debug mode
    /// </summary>
    public class DebugSettings {
        /// <summary>
        /// Do not use directly.
        /// Use DebugSwitch.$EnumName$.Get() to access the switch values.
        /// </summary>
        [XmlArrayItem("Value")]
        public DebugSwitchValue[] DebugSwitchValues = {
            new (false, "0: Path-finding debug log"),
            new (false, "1: Routing basic debug log"),
            new (false, "2: Parking ai debug log (basic)"),
            new (false, "3: Do not actually repair stuck vehicles/cims, just report"),
            new (false, "4: Parking ai debug log (extended)"),
            new (false, "5: Geometry debug log"),
            new (false, "6: Debug parking AI distance issue"),
            new (false, "7: Debug Timed Traffic Lights"),
            new (false, "8: Debug routing"),
            new (false, "9: Debug vehicle to segment end linking"),
            new (false, "10: Prevent routing recalculation on global configuration reload"),
            new (false, "11: Debug junction restrictions"),
            new (false, "12: Debug pedestrian pathfinding"),
            new (false, "13: Priority rules debug"),
            new (false, "14: Disable GUI overlay of citizens having a valid path"),
            new (false, "15: Disable checking of other vehicles for trams"),
            new (false, "16: Debug TramBaseAI.SimulationStep (2)"),
            new (false, "17: Debug alternative lane selection"),
            new (false, "18: Transport line path-find debugging"),
            new (false, "19: Enable obligation to drive on the right hand side of the road"),
            new (false, "20: Debug realistic public transport"),
            new (false, "21: Debug 'CalculateSegmentPosition'"),
            new (false, "22: Parking ai debug log (vehicles)"),
            new (false, "23: Debug lane connections"),
            new (false, "24: Debug resource loading"),
            new (false, "25: Debug turn-on-red"),
            new (false, "26: Debug speed limits (also lists NetInfos skipped due to m_netAI in SpeedLimitsManager.cs)"),
            new (false, "27: Allow U library UI and event debugging"),
            // Added a new flag? Bump LATEST_VERSION in GlobalConfig!
        };

        internal static DebugSwitch DebugSwitch => MaintenanceTab_ConfigGroup.DebugSwitch.Value;

        private static InstanceID SelectedInstance => InstanceManager.instance.GetSelectedInstance();
        private static DebugSettings Instance => GlobalConfig.Instance.Debug;
        private static ushort Get(ushort val1, ushort val2) => val1 != 0 ? val1 : val2;
        private static uint Get(uint val1, uint val2) => val1 != 0 ? val1 : val2;

        public static ushort NodeId => SelectedInstance.NetNode;

        public static ushort SegmentId => SelectedInstance.NetSegment;

        private ushort startSegmentId_ = 0;

        public static ushort StartSegmentId => Get(Instance.startSegmentId_, SegmentId);

        private ushort endSegmentId_ = 0;

        public static int EndSegmentId => Get(Instance.endSegmentId_, SegmentId);

        public static int VehicleId => SelectedInstance.Vehicle;

        public static int CitizenInstanceId => SelectedInstance.CitizenInstance;

        public static uint CitizenId => SelectedInstance.Citizen;

        private uint sourceBuildingId_ = 0;

        public static uint SourceBuildingId => Get(Instance.sourceBuildingId_, SelectedInstance.Building);

        private uint targetBuildingId_ = 0;
        public static uint TargetBuildingId => Get(Instance.targetBuildingId_, SelectedInstance.Building);

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
        None = -1,
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
        SpeedLimits = 26,
        ULibraryEvents = 27,
        // Added a new flag? Bump LATEST_VERSION in GlobalConfig!
    }

    public class DebugSwitchValue : IXmlSerializable {
        public bool Value = false;
        public string Description = string.Empty;

        [UsedImplicitly] //required for XmlSerializer
        public DebugSwitchValue() { }

        public DebugSwitchValue(bool value, string description) {
            Value = value;
            Description = description;
        }
        public XmlSchema GetSchema() {
            return null;
        }

        public void ReadXml(XmlReader reader) {
            while (reader.Read()) {
                switch (reader.NodeType) {
                    case XmlNodeType.Text:
                        bool.TryParse(reader.Value, out Value);
                        break;
                    case XmlNodeType.Comment:
                        Description = reader.Value;
                        break;
                    case XmlNodeType.EndElement:
                        return;
                }
            }
        }

        public void WriteXml(XmlWriter writer) {
            writer.WriteValue(Value);
            writer.WriteComment(Description);
        }
    }

    internal static class DebugSwitchExtensions {
        public static bool Get(this DebugSwitch sw) {
            return DebugSettings.DebugSwitch == sw || GlobalConfig.Instance.Debug.DebugSwitchValues[(int)sw].Value;
        }
    }
#endif
}