namespace TrafficManager.UI.MainMenu {
    using TrafficManager.State;

    public class TimedTrafficLightsButton : BaseMenuToolModeButton {
        protected override ToolMode ToolMode => ToolMode.TimedLightsSelectNode;

        protected override ButtonFunction Function => ButtonFunction.TimedTrafficLights;

        public override string Tooltip => Translation.Menu.Get("Tooltip:Timed traffic lights") + "\n" + Translation.Menu.Get("Tooltip.Keybinds:Auto TL");

        public override bool Visible => Options.timedLightsEnabled;
    }
}
