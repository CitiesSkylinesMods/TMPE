namespace TrafficManager.U.Autosize {
    /// <summary>
    /// Defines UI control interface for a control which owns a <see cref="UResizerConfig"/> and can be
    /// resized as necessary when UI scale or screen size changes.
    /// The callback will receive a <see cref="UResizer"/>.
    /// </summary>
    public interface ISmartSizableControl {
        UResizerConfig GetResizerConfig();

        /// <summary>
        /// Implement in child controls for extra actions while being updated from <see cref="UResizer"/>.
        /// Labels use this for UI scaling of their font.
        /// </summary>
        void OnBeforeResizerUpdate();

        void OnAfterResizerUpdate();
    }
}