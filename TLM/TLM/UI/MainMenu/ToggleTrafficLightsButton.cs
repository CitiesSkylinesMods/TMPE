namespace TrafficManager.UI.MainMenu {
    using System.Collections.Generic;
    using TrafficManager.RedirectionFramework;
    using TrafficManager.State.Keybinds;
    using TrafficManager.U.Button;

    public class ToggleTrafficLightsButton : BaseMenuToolModeButton {
        protected override ToolMode ToolMode => ToolMode.ToggleTrafficLight;

        public override void SetupButtonSkin(HashSet<string> atlasKeys) {
            // Button backround (from BackgroundPrefix) is provided by MainMenuPanel.Start
            this.Skin = new U.Button.ButtonSkin() {
                                                      Prefix = "ToggleTL",
                                                      BackgroundPrefix = "RoundButton",
                                                      BackgroundHovered = true,
                                                      BackgroundActive = true,
                                                      ForegroundActive = true,
                                                  };
            atlasKeys.AddRange(this.Skin.CreateAtlasKeyset());
        }

        protected override string U_OverrideTooltipText() => Translation.Menu.Get("Tooltip:Switch traffic lights");

        protected override bool IsVisible() => true;

        public override KeybindSetting U_OverrideTooltipShortcutKey() => KeybindSettingsBase.ToggleTrafficLightTool;
    }
}
