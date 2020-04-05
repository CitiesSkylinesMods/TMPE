namespace TrafficManager.UI.MainMenu {
    using System.Collections.Generic;
    using TrafficManager.RedirectionFramework;
    using TrafficManager.State;
    using TrafficManager.State.Keybinds;
    using TrafficManager.U.Button;

    public class SpeedLimitsButton : BaseMenuToolModeButton {
        protected override ToolMode ToolMode => ToolMode.SpeedLimits;

        public override void SetupButtonSkin(HashSet<string> atlasKeys) {
            // Button backround (from BackgroundPrefix) is provided by MainMenuPanel.Start
            this.Skin = new U.Button.ButtonSkin() {
                                                      Prefix = "SpeedLimits",
                                                      BackgroundPrefix = "RoundButton",
                                                      BackgroundHovered = true,
                                                      BackgroundActive = true,
                                                      ForegroundActive = true,
                                                  };
            atlasKeys.AddRange(this.Skin.CreateAtlasKeyset());
        }

        protected override ButtonFunction Function => new ButtonFunction("SpeedLimits");

        public override string GetTooltip() => Translation.Menu.Get("Tooltip:Speed limits");

        public override KeybindSetting ShortcutKey => KeybindSettingsBase.SpeedLimitsTool;

        public override bool IsVisible() => IsButtonEnabled();

        public static bool IsButtonEnabled() => Options.customSpeedLimitsEnabled;
    }
}
