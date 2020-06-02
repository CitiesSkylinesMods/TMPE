namespace TrafficManager.API.Traffic.Enums {
    using JetBrains.Annotations;

    /// <summary>
    /// Represents accuracy of simulation
    /// </summary>
    public enum SimulationAccuracy {
        /// <summary>
        /// Very low accuracy
        /// </summary>
        VeryLow = 0,

        /// <summary>
        /// Low accuracy
        /// </summary>
        Low = 1,

        /// <summary>
        /// Medium accuracy
        /// </summary>
        Medium = 2,

        /// <summary>
        /// High accuracy
        /// </summary>
        High = 3,

        /// <summary>
        /// Very high accuracy, will be returned when casting integer value to SimulationAccuracy
        /// </summary>
        [UsedImplicitly]
        VeryHigh = 4,

        /// <summary>
        /// Reference value of maximum allowed Simulation Accuracy
        /// </summary>
        MaxValue = 4,
    }
}