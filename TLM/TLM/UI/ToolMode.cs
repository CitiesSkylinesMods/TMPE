namespace TrafficManager.UI {
    /// <summary>Current mode of operation for TMPE.</summary>
    public enum ToolMode {
        /// <summary>No tool is active.</summary>
        None = 0,

        /// <summary>Traffic light on/off tool.</summary>
        SwitchTrafficLight = 1,

        /// <summary>Priority/yield tool.</summary>
        AddPrioritySigns = 2,

        /// <summary>Traffic light manual control tool.</summary>
        ManualSwitch = 3,

        /// <summary>Timed traffic light submode.</summary>
        TimedLightsSelectNode = 4,

        /// <summary>Timed traffic light submode.</summary>
        TimedLightsShowLights = 5,

        /// <summary>Lane Arrows tool is active.</summary>
        LaneChange = 6,

        /// <summary>Timed traffic light submode.</summary>
        TimedLightsAddNode = 7,

        /// <summary>Timed traffic light submode.</summary>
        TimedLightsRemoveNode = 8,

        /// <summary>Timed traffic light submode.</summary>
        TimedLightsCopyLights = 9,

        /// <summary>Speed limits tool.</summary>
        SpeedLimits = 10,

        /// <summary>Traffic type restrictions tool.</summary>
        VehicleRestrictions = 11,

        /// <summary>Lane connecting curves tool.</summary>
        LaneConnector = 12,

        /// <summary>Junction settings (allow/ban behaviours on junctions) tool.</summary>
        JunctionRestrictions = 13,

        /// <summary>No parking tool.</summary>
        ParkingRestrictions = 14,
    }
}