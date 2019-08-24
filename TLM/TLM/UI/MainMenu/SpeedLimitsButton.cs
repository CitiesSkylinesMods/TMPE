namespace TrafficManager.UI.MainMenu {
    using State;
    using State.Keybinds;
    using UXLibrary.Keyboard;

    public class SpeedLimitsButton : MenuToolModeButton {
        public override string Tooltip => "Speed_limits";

        public override bool Visible => Options.customSpeedLimitsEnabled;

        public override KeybindSetting ShortcutKey => KeybindSettingsBase.SpeedLimitsTool;

        protected override ToolMode ToolMode => ToolMode.SpeedLimits;

        protected override ButtonFunction Function => ButtonFunction.SpeedLimits;
    }
}