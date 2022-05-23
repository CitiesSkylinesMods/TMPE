namespace TrafficManager.API.UI {

    /// <summary>
    /// gets the texture for overlay sprite for each traffic rule according to the current theme.
    /// </summary>
    public interface IUIFactory {
        ITheme ActiveTheme { get; }
    }
}
