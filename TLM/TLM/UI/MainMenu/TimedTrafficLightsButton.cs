namespace TrafficManager.UI.MainMenu {
    using System.Collections.Generic;
    using TrafficManager.RedirectionFramework;
    using TrafficManager.State;
    using TrafficManager.U.Button;

    public class TimedTrafficLightsButton : BaseMenuToolModeButton {
        protected override ToolMode ToolMode => ToolMode.TimedTrafficLights;

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

        protected override string GetTooltip() => Translation.Menu.Get("Tooltip:Timed traffic lights");

        protected override bool IsVisible() => IsButtonEnabled();

        public static bool IsButtonEnabled() => Options.timedLightsEnabled;
    }
}
