namespace TrafficManager.UI.MainMenu {
    using TrafficManager.State;
    using TrafficManager.U.Button;

    public class ManualTrafficLightsButton : BaseMenuToolModeButton {
        protected override ToolMode ToolMode => ToolMode.ManualSwitch;

        protected override ButtonFunction Function => new ButtonFunction("ManualTrafficLights");

        public override string GetTooltip() =>
            Translation.Menu.Get("Tooltip:Manual traffic lights");

        public override bool IsVisible() => Options.timedLightsEnabled;
    }
}
