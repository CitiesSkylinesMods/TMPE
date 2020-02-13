namespace TrafficManager.Compatibility {

    /// <summary>
    /// The severity of a mod conflict.
    /// </summary>
    public enum Severity {
        /// <summary>
        /// Minor annoyance
        /// </summary>
        Minor,

        /// <summary>
        /// Loss of functionality.
        /// </summary>
        Major,

        /// <summary>
        /// Game-breaking.
        /// </summary>
        Critical,
    }
}
