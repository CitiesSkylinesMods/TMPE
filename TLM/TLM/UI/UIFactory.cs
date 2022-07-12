namespace TrafficManager.UI {
    using TrafficManager.API.UI;
    using TrafficManager.UI.Textures;

    public class UIFactory : IUIFactory {
        public static IUIFactory Instance = new UIFactory();

        public ITheme ActiveTheme => RoadSignThemeManager.ActiveTheme;
    }
}
