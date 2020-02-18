namespace TrafficManager.UI.MainMenu {
    using System.Collections.Generic;
    using TrafficManager.State;
    using TrafficManager.U.Button;

    public class TimedTrafficLightsButton : BaseMenuToolModeButton {
        protected override ToolMode ToolMode => ToolMode.TimedLightsSelectNode;

        public override void SetupButtonSkin(List<string> atlasKeys) {
        }

        protected override ButtonFunction Function => new ButtonFunction("TimedTrafficLights");

        public override string GetTooltip() =>
            Translation.Menu.Get("Tooltip:Timed traffic lights") + "\n" +
            Translation.Menu.Get("Tooltip.Keybinds:Auto TL");

        public override bool IsVisible() => Options.timedLightsEnabled;
    }
}
