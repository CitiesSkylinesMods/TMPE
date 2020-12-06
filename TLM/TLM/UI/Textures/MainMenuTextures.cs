namespace TrafficManager.UI.Textures {
    using TrafficManager.Util;
    using UnityEngine;
    using static TrafficManager.UI.Textures.TextureResources;

    /// <summary>Textures for main menu button and main menu panel.</summary>
    public class MainMenuTextures {
        public readonly Texture2D WindowBackground;

        public MainMenuTextures() {
            WindowBackground = LoadDllResource("MainMenu.WindowBackground.png", new IntVector2(16, 60));
        }
    }
}