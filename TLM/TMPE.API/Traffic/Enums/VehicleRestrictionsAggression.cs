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
        [LocaleKey("VR.Dropdown.Option:Low Aggression")]
        Low = 0,

        /// <summary>
        /// Medium aggression
        /// </summary>
        [LocaleKey("VR.Dropdown.Option:Medium Aggression")]
        Medium = 1,

        /// <summary>
        /// High aggression
        /// </summary>
        [UsedImplicitly]
        [LocaleKey("VR.Dropdown.Option:High Aggression")]
        High = 2,

        /// <summary>
        /// Strict aggression
        /// </summary>
        [LocaleKey("VR.Dropdown.Option:Strict")]
        Strict = 3,
    }
}