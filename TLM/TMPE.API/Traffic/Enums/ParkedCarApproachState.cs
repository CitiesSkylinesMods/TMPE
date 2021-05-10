namespace TrafficManager.API.Traffic.Enums {
    /// <summary>
    /// Indicates the current state while approaching a private car
    /// </summary>
    public enum ParkedCarApproachState {
        /// <summary>
        /// Citizen is not approaching their parked car
        /// </summary>
        None,

        /// <summary>
        /// Citizen is currently approaching their parked car
        /// </summary>
        Approaching,

        /// <summary>
        /// Citizen has approaching their parked car
        /// </summary>
        Approached,

        /// <summary>
        /// Citizen failed to approach their parked car
        /// </summary>
        Failure,
    }
}