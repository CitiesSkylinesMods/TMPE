namespace TrafficManager.UI.MainMenu {
    using System.Collections.Generic;
    using TrafficManager.State;
    using TrafficManager.U.Button;

    public class TimedTrafficLightsButton : BaseMenuToolModeButton {
        protected override ToolMode ToolMode => ToolMode.TimedLightsSelectNode;

        public override void SetupButtonSkin(List<string> atlasKeys) {
            // Button backround (from BackgroundPrefix) is provided by MainMenuPanel.Start
            this.Skin = new U.Button.ButtonSkin() {
                                                      Prefix = "TimedTL",
                                                      BackgroundPrefix = "RedButton",
                                                      BackgroundHovered = true,
                                                      BackgroundActive = true,
                                                      ForegroundActive = true,
                                                  };
            atlasKeys.AddRange(this.Skin.CreateAtlasKeysList());
        }

        protected override ButtonFunction Function => new ButtonFunction("TimedTrafficLights");

        public override string GetTooltip() =>
            Translation.Menu.Get("Tooltip:Timed traffic lights") + "\n" +
            Translation.Menu.Get("Tooltip.Keybinds:Auto TL");

        public override bool IsVisible() => Options.timedLightsEnabled;
    }
}
