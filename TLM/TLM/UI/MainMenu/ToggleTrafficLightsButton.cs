namespace TrafficManager.UI.MainMenu {
    using TrafficManager.State.Keybinds;
    using TrafficManager.U;
    using TrafficManager.Util;

    public class ToggleTrafficLightsButton : BaseMenuToolModeButton {
        protected override ToolMode ToolMode => ToolMode.ToggleTrafficLight;

        public override void SetupButtonSkin(AtlasBuilder futureAtlas) {
            // Button background (from BackgroundPrefix) is provided by MainMenuPanel.Start
            this.Skin = ButtonSkin.CreateSimple(
                                      foregroundPrefix: "ToggleTL",
                                      backgroundPrefix: UConst.MAINMENU_ROUND_BUTTON_BG)
                                  .CanHover(foreground: false)
                                  .CanActivate();
            this.Skin.UpdateAtlasBuilder(
                atlasBuilder: futureAtlas,
                spriteSize: new IntVector2(50));
        }

        protected override string U_OverrideTooltipText() =>
            Translation.Menu.Get("Tooltip:Switch traffic lights");

        protected override bool IsVisible() => true;

        public override KeybindSetting U_OverrideTooltipShortcutKey() =>
            KeybindSettingsBase.ToggleTrafficLightTool;
    }
}