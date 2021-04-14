namespace TrafficManager.API.Traffic.Enums {
    /// <summary>
    /// Indicates if a private car [may]/[shall]/[must not] be used
    /// </summary>
    public enum CarUsagePolicy {
        /// <summary>
        /// Citizens may use their own car
        /// </summary>
        Allowed,

        /// <summary>
        /// Citizens are forced to use their parked car
        /// </summary>
        ForcedParked,

        /// <summary>
        /// Citizens are forced to use a pocket car
        /// </summary>
        ForcedPocket,

        /// <summary>
        /// Citizens are forbidden to use their car
        /// </summary>
        Forbidden,
    }
}