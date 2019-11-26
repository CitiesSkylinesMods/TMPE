namespace TrafficManager.UI.MainMenu {
    using State;

    public class TimedTrafficLightsButton : MenuToolModeButton {
        protected override ToolMode ToolMode => ToolMode.TimedLightsSelectNode;

        protected override ButtonFunction Function => ButtonFunction.TimedTrafficLights;

        public override string Tooltip => Translation.Menu.Get("Tooltip:Timed traffic lights");

        public override bool Visible => Options.timedLightsEnabled;
    }
}
