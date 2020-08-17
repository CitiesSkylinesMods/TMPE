namespace TrafficManager.UI.Textures {
    using TrafficManager.Util;
    using UnityEngine;
    using static TrafficManager.UI.Textures.TextureResources;

    /// <summary>Textures for main menu button and main menu panel.</summary>
    public static class MainMenu {
        // public static readonly Texture2D MainMenuButton;
        // public static readonly Texture2D MainMenuButtons;
        // public static readonly Texture2D NoImage;
        // public static readonly Texture2D RemoveButton;
        public static readonly Texture2D WindowBackground;

        static MainMenu() {
            WindowBackground = LoadDllResource("MainMenu.WindowBackground.png", new IntVector2(16, 60));
        }
    }
}