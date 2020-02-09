namespace TrafficManager.UI.MainMenu {
    using TrafficManager.State;
    using TrafficManager.State.Keybinds;

    public class SpeedLimitsButton : BaseMenuToolModeButton {
        protected override ToolMode ToolMode => ToolMode.SpeedLimits;

        protected override ButtonFunction Function => ButtonFunction.SpeedLimits;

        public override string GetTooltip() => Translation.Menu.Get("Tooltip:Speed limits");

        public override bool IsVisible() => Options.customSpeedLimitsEnabled;

        public override KeybindSetting ShortcutKey => KeybindSettingsBase.SpeedLimitsTool;
    }
}
