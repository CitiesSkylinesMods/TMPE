namespace TrafficManager.API.Traffic.Enums {
    public enum LaneEndTransitionType {
        /// <summary>
        /// No connection
        /// </summary>
        Invalid,

        /// <summary>
        /// Lane arrow or regular lane connection
        /// </summary>
        Default,

        /// <summary>
        /// Custom lane connection
        /// </summary>
        LaneConnection,

        /// <summary>
        /// Relaxed connection for road vehicles [!] that do not have to follow lane arrows
        /// </summary>
        Relaxed,
    }
}