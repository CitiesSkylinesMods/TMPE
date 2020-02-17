namespace TrafficManager.UI.MainMenu {
    using TrafficManager.State;
    using TrafficManager.State.Keybinds;
    using TrafficManager.U.Button;

    public class LaneConnectorButton : BaseMenuToolModeButton {
        protected override ToolMode ToolMode => ToolMode.LaneConnector;

        protected override ButtonFunction Function => new ButtonFunction("LaneConnector");

        public override string GetTooltip() => Translation.Menu.Get("Tooltip:Lane connector");

        public override bool IsVisible() => Options.laneConnectorEnabled;

        public override KeybindSetting ShortcutKey => KeybindSettingsBase.LaneConnectionsTool;
    }
}
