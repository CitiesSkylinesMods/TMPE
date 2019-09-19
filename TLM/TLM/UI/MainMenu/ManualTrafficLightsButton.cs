namespace TrafficManager.UI.MainMenu {
    using State;

    public class ManualTrafficLightsButton : MenuToolModeButton {
        protected override ToolMode ToolMode => ToolMode.ManualSwitch;

        protected override ButtonFunction Function => ButtonFunction.ManualTrafficLights;

        public override string Tooltip => Translation.Menu.Get("Tooltip:Manual traffic lights");

        public override bool Visible => Options.timedLightsEnabled;
    }
}
