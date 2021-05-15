namespace TrafficManager.API.Traffic.Enums {
    public enum ExtPathState {
        /// <summary>
        /// No path
        /// </summary>
        None = 0,

        /// <summary>
        /// Path is currently being calculated
        /// </summary>
        Calculating = 1,

        /// <summary>
        /// Path-finding has succeeded
        /// </summary>
        Ready = 2,

        /// <summary>
        /// Path-finding has failed
        /// </summary>
        Failed = 3,
    }
}