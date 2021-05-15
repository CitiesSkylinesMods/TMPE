namespace TrafficManager.U.Autosize {
    /// <summary>
    /// Used in <see cref="UResizerConfig"/> to command <see cref="UResizer"/> to automatically
    /// fix size of the control, or trust the user-provided resize function to do it.
    /// </summary>
    public enum USizeChoice {
        /// <summary>
        /// Do not apply fixed size, trust the user code to reset the size in resize function.
        /// </summary>
        ResizeFunction,

        /// <summary>Apply fixed size from the <see cref="UResizerConfig.FixedSize"/>.</summary>
        Predefined,
    }
}