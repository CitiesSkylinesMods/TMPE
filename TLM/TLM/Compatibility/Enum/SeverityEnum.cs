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
        /// An instance of TM:PE which is not otherwise marked as incompatible.
        /// If there is more than one TMPE active, user must choose only one.
        /// </summary>
        TMPE,

        /// <summary>
        /// Minor annoyance or glitch that player can choose to live with if they want.
        /// </summary>
        Minor,

        /// <summary>
        /// Loss of functionality, such as a mod that directly conflicts with TM:PE.
        /// </summary>
        Major,

        /// <summary>
        /// Game-breaking, must be removed even if not using TM:PE.
        /// </summary>
        Critical,
    }
}
