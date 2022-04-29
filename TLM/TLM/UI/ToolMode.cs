namespace TrafficManager.UI {
    /// <summary>Current mode of operation for TMPE.</summary>
    public enum ToolMode {
        /// <summary>No tool is active.</summary>
        None,

        /// <summary>Traffic light on/off tool.</summary>
        ToggleTrafficLight,

        /// <summary>Priority/yield tool.</summary>
        AddPrioritySigns,

        /// <summary>Traffic light manual control tool.</summary>
        ManualSwitch,

        /// <summary>Timed traffic lights.</summary>
        TimedTrafficLights,

        /// <summary>Lane Arrows tool is active.</summary>
        LaneArrows,

        /// <summary>Speed limits tool.</summary>
        SpeedLimits,

        /// <summary>Traffic type restrictions tool.</summary>
        VehicleRestrictions,

        /// <summary>Lane connecting curves tool.</summary>
        LaneConnector,

        /// <summary>Junction settings (allow/ban behaviours on junctions) tool.</summary>
        JunctionRestrictions,

        /// <summary>No parking tool.</summary>
        ParkingRestrictions,

        /// <summary>
        /// This key is not an actual tool mode, it is used by MainMenu to key the UI button.
        /// </summary>
        DespawnButton,

        /// <summary>
        /// This key is not an actual tool mode, it is used by MainMenu to key the UI button.
        /// </summary>
        ClearTrafficButton,

        RoutingDetector,
    }
}