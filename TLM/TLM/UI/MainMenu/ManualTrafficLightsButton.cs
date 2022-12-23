namespace TrafficManager.UI.MainMenu {
    using TrafficManager.State;
    using TrafficManager.U;
    using TrafficManager.Util;

    public class ManualTrafficLightsButton : BaseMenuToolModeButton {
        protected override ToolMode ToolMode => ToolMode.ManualSwitch;

        public override void SetupButtonSkin(AtlasBuilder futureAtlas) {
            // Button background (from BackgroundPrefix) is provided by MainMenuPanel.Start
            this.Skin = ButtonSkin.CreateSimple(
                                      foregroundPrefix: "ManualTL",
                                      backgroundPrefix: UConst.MAINMENU_ROUND_BUTTON_BG)
                                  .CanHover(foreground: false)
                                  .CanActivate();
            this.Skin.UpdateAtlasBuilder(
                atlasBuilder: futureAtlas,
                spriteSize: new IntVector2(50));
        }

        protected override string U_OverrideTooltipText() =>
            Translation.Menu.Get("Tooltip:Manual traffic lights");

        protected override bool IsVisible() => IsButtonEnabled();

        public static bool IsButtonEnabled() => SavedGameOptions.Instance.timedLightsEnabled;
    }
}