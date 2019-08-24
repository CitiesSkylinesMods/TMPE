namespace TrafficManager.UI.MainMenu {
    using State;
    using State.Keybinds;
    using UXLibrary.Keyboard;

    public class LaneConnectorButton : MenuToolModeButton {
        public override string Tooltip => "Lane_connector";

        public override bool Visible => Options.laneConnectorEnabled;

        public override KeybindSetting ShortcutKey => KeybindSettingsBase.LaneConnectionsTool;

        protected override ToolMode ToolMode => ToolMode.LaneConnector;

        protected override ButtonFunction Function => ButtonFunction.LaneConnector;
    }
}