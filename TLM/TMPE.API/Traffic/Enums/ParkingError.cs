namespace TrafficManager.API.Traffic.Enums {
    /// <summary>
    /// Represents the reason why a parked car could not be spawned
    /// </summary>
    public enum ParkingError {
        /// <summary>
        /// Parked car could be spawned
        /// </summary>
        None,

        /// <summary>
        /// No free parking space was found
        /// </summary>
        NoSpaceFound,

        /// <summary>
        /// The maximum allowed number of parked vehicles has been reached
        /// </summary>
        LimitHit,
    }
}