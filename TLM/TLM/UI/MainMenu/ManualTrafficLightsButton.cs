namespace TrafficManager.UI.MainMenu {
    using System.Collections.Generic;
    using TrafficManager.State;
    using TrafficManager.U.Button;

    public class ManualTrafficLightsButton : BaseMenuToolModeButton {
        protected override ToolMode ToolMode => ToolMode.ManualSwitch;

        public override void SetupButtonSkin(List<string> atlasKeys) {
        }

        protected override ButtonFunction Function => new ButtonFunction("ManualTrafficLights");

        public override string GetTooltip() =>
            Translation.Menu.Get("Tooltip:Manual traffic lights");

        public override bool IsVisible() => Options.timedLightsEnabled;
    }
}
