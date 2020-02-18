namespace TrafficManager.UI.MainMenu {
    using System.Collections.Generic;
    using TrafficManager.State.Keybinds;
    using TrafficManager.U.Button;

    public class ToggleTrafficLightsButton : BaseMenuToolModeButton {
        protected override ToolMode ToolMode => ToolMode.SwitchTrafficLight;

        public override void SetupButtonSkin(List<string> atlasKeys) {
            // Button backround (from BackgroundPrefix) is provided by MainMenuPanel.Start
            this.Skin = new U.Button.ButtonSkin() {
                                                      Prefix = "ToggleTL",
                                                      BackgroundPrefix = "RoundButton",
                                                      BackgroundHovered = true,
                                                      BackgroundActive = true,
                                                      ForegroundActive = true,
                                                  };
            atlasKeys.AddRange(this.Skin.CreateAtlasKeysList());
        }

        protected override ButtonFunction Function => new ButtonFunction("ToggleTrafficLights");

        public override string GetTooltip() => Translation.Menu.Get("Tooltip:Switch traffic lights");

        public override bool IsVisible() => true;

        public override KeybindSetting ShortcutKey => KeybindSettingsBase.ToggleTrafficLightTool;
    }
}
