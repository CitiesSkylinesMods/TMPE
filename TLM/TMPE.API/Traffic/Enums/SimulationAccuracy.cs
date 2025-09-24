namespace TrafficManager.API.Traffic.Enums {
    using JetBrains.Annotations;

    /// <summary>
    /// Represents accuracy of simulation
    /// </summary>
    public enum SimulationAccuracy {
        /// <summary>
        /// Very low accuracy
        /// </summary>
        [LocaleKey("General.Dropdown.Option:Very low")]
        VeryLow = 0,

        /// <summary>
        /// Low accuracy
        /// </summary>
        [LocaleKey("General.Dropdown.Option:Low")]
        Low = 1,

        /// <summary>
        /// Medium accuracy
        /// </summary>
        [LocaleKey("General.Dropdown.Option:Medium")]
        Medium = 2,

        /// <summary>
        /// High accuracy
        /// </summary>
        [LocaleKey("General.Dropdown.Option:High")]
        High = 3,

        /// <summary>
        /// Very high accuracy, will be returned when casting integer value to SimulationAccuracy
        /// </summary>
        [UsedImplicitly]
        [LocaleKey("General.Dropdown.Option:Very high")]
        VeryHigh = 4,

        /// <summary>
        /// Reference value of maximum allowed Simulation Accuracy
        /// </summary>
        MaxValue = 4,
    }
}