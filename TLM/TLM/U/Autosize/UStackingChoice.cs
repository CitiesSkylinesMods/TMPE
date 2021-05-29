namespace TrafficManager.U.Autosize {
    /// <summary>
    /// Used in <see cref="UResizerConfig"/> to command <see cref="UResizer"/> to automatically
    /// stack/position the control, or trust the user-provided resize function to do it.
    /// </summary>
    public enum UStackingChoice {
        /// <summary>Do not apply stacking, trust the user code to reset the size in resize function.</summary>
        ResizeFunction,

        /// <summary>Apply stacking from the <see cref="UResizerConfig.Stacking"/>.</summary>
        Predefined,
    }
}