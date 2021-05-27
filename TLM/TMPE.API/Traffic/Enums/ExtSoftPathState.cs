namespace TrafficManager.API.Traffic.Enums {
    public enum ExtSoftPathState {
        /// <summary>
        /// No path
        /// </summary>
        None = 0,

        /// <summary>
        /// Path is currently being calculated
        /// </summary>
        Calculating = 1,

        /// <summary>
        /// Path-finding has succeeded and must be handled appropriately
        /// </summary>
        Ready = 2,

        /// <summary>
        /// Path-finding has failed and must be handled appropriately
        /// </summary>
        FailedHard = 3,

        /// <summary>
        /// Path-finding must be retried (soft path-find failure)
        /// </summary>
        FailedSoft = 4,

        /// <summary>
        /// Path-finding result must not be handled by the citizen because the path will be transferred to a vehicle
        /// </summary>
        Ignore = 5,
    }
}