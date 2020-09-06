namespace TrafficManager.UI.MainMenu {
    using System.Collections.Generic;
    using TrafficManager.RedirectionFramework;
    using TrafficManager.State;
    using TrafficManager.U;
    using TrafficManager.Util;

    public class TimedTrafficLightsButton : BaseMenuToolModeButton {
        protected override ToolMode ToolMode => ToolMode.TimedTrafficLights;

        public override void SetupButtonSkin(AtlasBuilder futureAtlas) {
            // Button backround (from BackgroundPrefix) is provided by MainMenuPanel.Start
            this.Skin = new U.ButtonSkin() {
                Prefix = "TimedTL",
                BackgroundPrefix = "RoundButton",
                BackgroundHovered = true,
                BackgroundActive = true,
                ForegroundActive = true,
            };
            this.Skin.UpdateAtlasBuilder(
                atlasBuilder: futureAtlas,
                spriteSize: new IntVector2(50));
        }

        protected override string U_OverrideTooltipText() => Translation.Menu.Get("Tooltip:Timed traffic lights");

        protected override bool IsVisible() => IsButtonEnabled();

        public static bool IsButtonEnabled() => Options.timedLightsEnabled;
    }
}
