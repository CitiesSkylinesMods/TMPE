namespace TrafficManager.UI.MainMenu {
    using System.Collections.Generic;
    using TrafficManager.RedirectionFramework;
    using TrafficManager.State;
    using TrafficManager.U.Button;

    public class TimedTrafficLightsButton : BaseMenuToolModeButton {
        protected override ToolMode ToolMode => ToolMode.TimedLightsSelectNode;

        public override void SetupButtonSkin(HashSet<string> atlasKeys) {
            // Button backround (from BackgroundPrefix) is provided by MainMenuPanel.Start
            this.Skin = new U.Button.ButtonSkin() {
                                                      Prefix = "TimedTL",
                                                      BackgroundPrefix = "RoundButton",
                                                      BackgroundHovered = true,
                                                      BackgroundActive = true,
                                                      ForegroundActive = true,
                                                  };
            atlasKeys.AddRange(this.Skin.CreateAtlasKeyset());
        }

        protected override ButtonFunction Function => new ButtonFunction("TimedTrafficLights");

        public override string GetTooltip() =>
            Translation.Menu.Get("Tooltip:Timed traffic lights") + "\n" +
            Translation.Menu.Get("Tooltip.Keybinds:Auto TL");

        public override bool IsVisible() => IsButtonEnabled();

        public static bool IsButtonEnabled() => Options.timedLightsEnabled;
    }
}
