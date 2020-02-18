namespace TrafficManager.UI.MainMenu {
    using System.Collections.Generic;
    using TrafficManager.State;
    using TrafficManager.State.Keybinds;
    using TrafficManager.U.Button;

    public class SpeedLimitsButton : BaseMenuToolModeButton {
        protected override ToolMode ToolMode => ToolMode.SpeedLimits;

        public override void SetupButtonSkin(List<string> atlasKeys) {
        }

        protected override ButtonFunction Function => new ButtonFunction("SpeedLimits");

        public override string GetTooltip() => Translation.Menu.Get("Tooltip:Speed limits");

        public override bool IsVisible() => Options.customSpeedLimitsEnabled;

        public override KeybindSetting ShortcutKey => KeybindSettingsBase.SpeedLimitsTool;
    }
}
