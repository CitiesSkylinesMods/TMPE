namespace TrafficManager.UI {
    /// <summary>Current mode of operation for TMPE.</summary>
    public enum ToolMode {
        /// <summary>No tool is active.</summary>
        None = 0,

        /// <summary>Traffic light on/off tool.</summary>
        ToggleTrafficLight = 1,

        /// <summary>Priority/yield tool.</summary>
        AddPrioritySigns = 2,

        /// <summary>Traffic light manual control tool.</summary>
        ManualSwitch = 3,

        /// <summary>
        /// This key is not an actual tool mode, it is used by MainMenu to key the UI button for
        /// timed traffic lights.
        /// </summary>
        TimedLightsButton,

        /// <summary>Timed traffic light submode.</summary>
        TimedLightsSelectNode = 4,

        /// <summary>Timed traffic light submode.</summary>
        TimedLightsShowLights = 5,

        /// <summary>Lane Arrows tool is active.</summary>
        LaneArrows = 6,

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

        /// <summary>
        /// This key is not an actual tool mode, it is used by MainMenu to key the UI button.
        /// </summary>
        DespawnButton,

        /// <summary>
        /// This key is not an actual tool mode, it is used by MainMenu to key the UI button.
        /// </summary>
        ClearTrafficButton,
    }
}