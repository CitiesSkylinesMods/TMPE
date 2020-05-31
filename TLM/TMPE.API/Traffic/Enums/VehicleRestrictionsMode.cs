namespace TrafficManager.API.Traffic.Enums {
    public enum VehicleRestrictionsMode {
        /// <summary>
        /// Interpret bus lanes as "free for all"
        /// </summary>
        Unrestricted,

        /// <summary>
        /// Interpret bus lanes according to the configuration
        /// </summary>
        Configured,

        /// <summary>
        /// Interpret bus lanes as restricted
        /// </summary>
        Restricted,
    }
}