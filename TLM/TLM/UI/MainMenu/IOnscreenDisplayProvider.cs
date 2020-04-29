namespace TrafficManager.UI.MainMenu {
    /// <summary>
    /// Implement this if your class should be called from the main tool or main menu and can
    /// provide onscreen keyboard and click hints for the current operation mode.
    /// </summary>
    public interface IOnscreenDisplayProvider {
        /// <summary>
        /// Called from the <see cref="TrafficManagerTool"/> when update for the Keybinds panel
        /// in MainMenu is requested. Or when we need to change state.
        /// Never call this directly, only as: MainTool.RequestOnscreenDisplayUpdate();
        /// What should do: Clear the OnscreenDisplay panel or clear and populate with keybinds.
        /// </summary>
        void UpdateOnscreenDisplayPanel();
    }
}