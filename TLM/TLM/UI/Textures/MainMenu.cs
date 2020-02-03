namespace TrafficManager.UI.Textures {
    using UnityEngine;
    using static TrafficManager.UI.Textures.TextureResources;

    /// <summary>Textures for main menu button and main menu panel.</summary>
    public static class MainMenu {
        public static readonly Texture2D MainMenuButton;
        public static readonly Texture2D MainMenuButtons;
        public static readonly Texture2D NoImage;
        public static readonly Texture2D RemoveButton;
        public static readonly Texture2D WindowBackground;

        static MainMenu() {
            // missing image
            NoImage = LoadDllResource("MainMenu.noimage.png", 64, 64);

            // main menu icon
            MainMenuButton = LoadDllResource("MainMenu.MenuButton.png", 300, 50);
            MainMenuButton.name = "TMPE_MainMenuButtonIcon";

            // Main menu backgrounds, buttons, and highlighted buttons
            MainMenuButtons = LoadDllResource("MainMenu.LegacyButtons.png", 960, 50);
            MainMenuButtons.name = "TMPE_MainMenuButtons";

            RemoveButton = LoadDllResource("MainMenu.remove-btn.png", 150, 30);

            WindowBackground = LoadDllResource("MainMenu.WindowBackground.png", 16, 60);
        }
    }
}