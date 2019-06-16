using TrafficManager.State;

namespace TrafficManager.UI.MainMenu {
	public class ManualTrafficLightsButton : MenuToolModeButton {
		public override ToolMode ToolMode => ToolMode.ManualSwitch;
                public override ButtonFunction Function => ButtonFunction.ManualTrafficLights;
                public override string Tooltip => "Manual_traffic_lights";
                public override bool Visible => Options.timedLightsEnabled;
        }
}
