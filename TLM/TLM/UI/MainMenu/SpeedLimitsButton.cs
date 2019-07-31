namespace TrafficManager.UI.MainMenu {
    using State;
    using State.Keybinds;

    public class SpeedLimitsButton : MenuToolModeButton {
        protected override ToolMode ToolMode => ToolMode.SpeedLimits;

        protected override ButtonFunction Function => ButtonFunction.SpeedLimits;

        public override string Tooltip => "Speed_limits";

        public override bool Visible => Options.customSpeedLimitsEnabled;

        public override KeybindSetting ShortcutKey => KeybindSettingsBase.SpeedLimitsTool;
    }
}