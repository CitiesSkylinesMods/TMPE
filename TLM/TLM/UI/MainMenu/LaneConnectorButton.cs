using ColossalFramework;
using TrafficManager.State;

namespace TrafficManager.UI.MainMenu {
    public class LaneConnectorButton : MenuToolModeButton {
        public override ToolMode ToolMode => ToolMode.LaneConnector;
        public override ButtonFunction Function => ButtonFunction.LaneConnector;
        public override string Tooltip => "Lane_connector";
        public override bool Visible => Options.laneConnectorEnabled;
        public override SavedInputKey ShortcutKey => OptionsKeymapping.KeyLaneConnectionsTool;
    }
}