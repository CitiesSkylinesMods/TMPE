namespace TrafficManager.UI.MainMenu {
    using State;
    using State.Keybinds;

    public class LaneConnectorButton : MenuToolModeButton {
        protected override ToolMode ToolMode => ToolMode.LaneConnector;

        protected override ButtonFunction Function => ButtonFunction.LaneConnector;

        public override string Tooltip => Translation.Menu.Get("Tooltip:Lane connector");

        public override bool Visible => Options.laneConnectorEnabled;

        public override KeybindSetting ShortcutKey => KeybindSettingsBase.LaneConnectionsTool;
    }
}