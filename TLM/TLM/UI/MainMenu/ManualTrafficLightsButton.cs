namespace TrafficManager.UI.MainMenu {
    using State;

    public class ManualTrafficLightsButton : MenuToolModeButton {
        protected override ToolMode ToolMode => ToolMode.ManualSwitch;

        protected override ButtonFunction Function => ButtonFunction.ManualTrafficLights;

        public override string Tooltip => "Manual_traffic_lights";

        public override bool Visible => Options.timedLightsEnabled;
    }
}