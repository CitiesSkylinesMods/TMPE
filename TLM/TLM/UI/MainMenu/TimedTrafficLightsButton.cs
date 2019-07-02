using TrafficManager.State;

namespace TrafficManager.UI.MainMenu {
    public class TimedTrafficLightsButton : MenuToolModeButton {
        public override ToolMode ToolMode => ToolMode.TimedLightsSelectNode;
        public override ButtonFunction Function => ButtonFunction.TimedTrafficLights;
        public override string Tooltip => "Timed_traffic_lights";
        public override bool Visible => Options.timedLightsEnabled;
    }
}