namespace TrafficManager.UI.MainMenu {
    using TrafficManager.U;
    using TrafficManager.Util;

    public class RoutingDetectorButton : BaseMenuToolModeButton {
        protected override ToolMode ToolMode => ToolMode.RoutingDetector;

        public override void SetupButtonSkin(AtlasBuilder atlasBuilder) {
            this.Skin = ButtonSkin.CreateSimple(
                                      foregroundPrefix: "RoutingDetector",
                                      backgroundPrefix: UConst.MAINMENU_ROUND_BUTTON_BG)
                                  .CanHover(foreground: false)
                                  .CanActivate();
            this.Skin.UpdateAtlasBuilder(
                atlasBuilder: atlasBuilder,
                spriteSize: new IntVector2(50));
        }

        protected override string U_OverrideTooltipText() => "Routing detector";

        protected override bool IsVisible() => true;
    }
}