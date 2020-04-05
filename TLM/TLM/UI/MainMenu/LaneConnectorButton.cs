namespace TrafficManager.UI.MainMenu {
    using System.Collections.Generic;
    using TrafficManager.RedirectionFramework;
    using TrafficManager.State;
    using TrafficManager.State.Keybinds;
    using TrafficManager.U.Button;

    public class LaneConnectorButton : BaseMenuToolModeButton {
        protected override ToolMode ToolMode => ToolMode.LaneConnector;

        public override void SetupButtonSkin(HashSet<string> atlasKeys) {
            // Button backround (from BackgroundPrefix) is provided by MainMenuPanel.Start
            this.Skin = new U.Button.ButtonSkin() {
                                                      Prefix = "LaneConnector",
                                                      BackgroundPrefix = "RoundButton",
                                                      BackgroundHovered = true,
                                                      BackgroundActive = true,
                                                      ForegroundActive = true,
                                                  };
            atlasKeys.AddRange(this.Skin.CreateAtlasKeyset());
        }

        protected override ButtonFunction Function => new ButtonFunction("LaneConnector");

        public override string GetTooltip() => Translation.Menu.Get("Tooltip:Lane connector");

        public override KeybindSetting ShortcutKey => KeybindSettingsBase.LaneConnectionsTool;

        public override bool IsVisible() => IsButtonEnabled();

        public static bool IsButtonEnabled() => Options.laneConnectorEnabled;
    }
}
