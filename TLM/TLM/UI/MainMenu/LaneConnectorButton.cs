namespace TrafficManager.UI.MainMenu {
    using System.Collections.Generic;
    using TrafficManager.State;
    using TrafficManager.State.Keybinds;
    using TrafficManager.U.Button;

    public class LaneConnectorButton : BaseMenuToolModeButton {
        protected override ToolMode ToolMode => ToolMode.LaneConnector;

        public override void SetupButtonSkin(List<string> atlasKeys) {
        }

        protected override ButtonFunction Function => new ButtonFunction("LaneConnector");

        public override string GetTooltip() => Translation.Menu.Get("Tooltip:Lane connector");

        public override bool IsVisible() => Options.laneConnectorEnabled;

        public override KeybindSetting ShortcutKey => KeybindSettingsBase.LaneConnectionsTool;
    }
}
