namespace TrafficManager.UI.MainMenu {
    using TrafficManager.State;

    public class ManualTrafficLightsButton : BaseMenuToolModeButton {
        protected override ToolMode ToolMode => ToolMode.ManualSwitch;

        protected override ButtonFunction Function => ButtonFunction.ManualTrafficLights;

        public override string GetTooltip() =>
            Translation.Menu.Get("Tooltip:Manual traffic lights");

        public override bool IsVisible() => Options.timedLightsEnabled;
    }
}
