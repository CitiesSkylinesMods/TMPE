namespace TrafficManager.UI.MainMenu {
    using TrafficManager.State;
    using TrafficManager.State.Keybinds;

    public class SpeedLimitsButton : MenuToolModeButton {
        protected override ToolMode ToolMode => ToolMode.SpeedLimits;

        protected override ButtonFunction Function => ButtonFunction.SpeedLimits;

        public override string Tooltip => Translation.Menu.Get("Tooltip:Speed limits");

        public override bool Visible => Options.customSpeedLimitsEnabled;

        public override KeybindSetting ShortcutKey => KeybindSettingsBase.SpeedLimitsTool;
    }
}
