namespace TrafficManager.UI.MainMenu {
    using System.Collections.Generic;
    using TrafficManager.RedirectionFramework;
    using TrafficManager.State;
    using TrafficManager.U.Button;

    public class ManualTrafficLightsButton : BaseMenuToolModeButton {
        protected override ToolMode ToolMode => ToolMode.ManualSwitch;

        public override void SetupButtonSkin(HashSet<string> atlasKeys) {
            // Button backround (from BackgroundPrefix) is provided by MainMenuPanel.Start
            this.Skin = new U.Button.ButtonSkin() {
                                                      Prefix = "ManualTL",
                                                      BackgroundPrefix = "RoundButton",
                                                      BackgroundHovered = true,
                                                      BackgroundActive = true,
                                                      ForegroundActive = true,
                                                  };
            atlasKeys.AddRange(this.Skin.CreateAtlasKeyset());
        }

        protected override ButtonFunction Function => new ButtonFunction("ManualTrafficLights");

        public override string GetTooltip() =>
            Translation.Menu.Get("Tooltip:Manual traffic lights");

        public override bool IsVisible() => IsButtonEnabled();

        public static bool IsButtonEnabled() => Options.timedLightsEnabled;
    }
}
