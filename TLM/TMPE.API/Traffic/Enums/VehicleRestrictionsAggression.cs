namespace TrafficManager.API.Traffic.Enums {
    using JetBrains.Annotations;

    /// <summary>
    /// Represents vehicle restrictions effect strength
    /// </summary>
    public enum VehicleRestrictionsAggression {
        /// <summary>
        /// Low aggression
        /// </summary>
        [UsedImplicitly]
        Low = 0,

        /// <summary>
        /// Medium aggression
        /// </summary>
        Medium = 1,

        /// <summary>
        /// High aggression
        /// </summary>
        [UsedImplicitly]
        High = 2,

        /// <summary>
        /// Strict aggression
        /// </summary>
        Strict = 3,
    }
}