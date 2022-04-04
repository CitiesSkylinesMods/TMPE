namespace TrafficManager.API.Traffic.Enums {
    using JetBrains.Annotations;

    /// <summary>
    /// Represents accuracy of simulation
    /// </summary>
    public enum SimulationAccuracy {
        /// <summary>
        /// Very low accuracy
        /// </summary>
        [Key("General.Dropdown.Option:Very low")]
        VeryLow = 0,

        /// <summary>
        /// Low accuracy
        /// </summary>
        [Key("General.Dropdown.Option:Low")]
        Low = 1,

        /// <summary>
        /// Medium accuracy
        /// </summary>
        [Key("General.Dropdown.Option:Medium")]
        Medium = 2,

        /// <summary>
        /// High accuracy
        /// </summary>
        [Key("General.Dropdown.Option:High")]
        High = 3,

        /// <summary>
        /// Very high accuracy, will be returned when casting integer value to SimulationAccuracy
        /// </summary>
        [UsedImplicitly]
        [Key("General.Dropdown.Option:Very high")]
        VeryHigh = 4,

        /// <summary>
        /// Reference value of maximum allowed Simulation Accuracy
        /// </summary>
        MaxValue = 4,
    }
}