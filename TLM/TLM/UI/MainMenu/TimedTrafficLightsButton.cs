namespace TrafficManager.UI.MainMenu {
    using TrafficManager.State;

    public class TimedTrafficLightsButton : BaseMenuToolModeButton {
        protected override ToolMode ToolMode => ToolMode.TimedLightsSelectNode;

        protected override ButtonFunction Function => ButtonFunction.TimedTrafficLights;

        public override string GetTooltip() => Translation.Menu.Get("Tooltip:Timed traffic lights") + "\n" + Translation.Menu.Get("Tooltip.Keybinds:Auto TL");

        public override bool IsVisible() => Options.timedLightsEnabled;
    }
}
