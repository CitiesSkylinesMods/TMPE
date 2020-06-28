namespace TrafficManager.UI.MainMenu {
    using System.Collections.Generic;
    using TrafficManager.RedirectionFramework;
    using TrafficManager.State;
    using TrafficManager.State.Keybinds;

    public class SpeedLimitsButton : BaseMenuToolModeButton {
        protected override ToolMode ToolMode => ToolMode.SpeedLimits;

        public override void SetupButtonSkin(HashSet<string> atlasKeys) {
            // Button backround (from BackgroundPrefix) is provided by MainMenuPanel.Start
            this.Skin = new U.ButtonSkin() {
                Prefix = "SpeedLimits",
                BackgroundPrefix = "RoundButton",
                BackgroundHovered = true,
                BackgroundActive = true,
                ForegroundActive = true,
            };
            atlasKeys.AddRange(this.Skin.CreateAtlasKeyset());
        }

        protected override string U_OverrideTooltipText() => Translation.Menu.Get("Tooltip:Speed limits");

        public override KeybindSetting U_OverrideTooltipShortcutKey() => KeybindSettingsBase.SpeedLimitsTool;

        protected override bool IsVisible() => IsButtonEnabled();

        public static bool IsButtonEnabled() => Options.customSpeedLimitsEnabled;
    }
}
