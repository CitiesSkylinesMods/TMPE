namespace TrafficManager.Compatibility.Enum {

    /// <summary>
    /// The severity of a mod conflict.
    /// </summary>
    public enum Severity {
        /// <summary>
        /// No known issues.
        /// </summary>
        None,

        /// <summary>
        /// Non-obsolete TM:PE mod candidate; if more than one user has to choose which one to use.
        /// </summary>
        Candidate,

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
