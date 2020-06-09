namespace TrafficManager.API.Traffic.Enums {
    public enum VehicleJunctionTransitState {
        /// <summary>
        /// Represents an unknown/ignored state
        /// </summary>
        None,

        /// <summary>
        /// Vehicle is apparoaching at a junction
        /// </summary>
        Approach,

        /// <summary>
        /// Vehicle must stop at a junction
        /// </summary>
        Stop,

        /// <summary>
        /// Vehicle is leaving the junction
        /// </summary>
        Leave,

        /// <summary>
        /// Vehicle may leave but is blocked due to traffic ahead
        /// </summary>
        Blocked,
    }
}