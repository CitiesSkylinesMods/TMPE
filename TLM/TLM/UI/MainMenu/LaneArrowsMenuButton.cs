namespace TrafficManager.UI.MainMenu {
    using TrafficManager.State.Keybinds;
    using TrafficManager.U;
    using TrafficManager.Util;

    public class LaneArrowsMenuButton : BaseMenuToolModeButton {
        protected override ToolMode ToolMode => ToolMode.LaneArrows;

        public override void SetupButtonSkin(AtlasBuilder futureAtlas) {
            // Button backround (from BackgroundPrefix) is provided by MainMenuPanel.Start
            this.Skin = new U.ButtonSkin() {
                Prefix = "LaneArrows",
                BackgroundPrefix = "RoundButton",
                BackgroundHovered = true,
                BackgroundActive = true,
                ForegroundActive = true,
            };
            this.Skin.UpdateAtlasBuilder(
                atlasBuilder: futureAtlas,
                spriteSize: new IntVector2(50));
        }

        protected override string U_OverrideTooltipText() => Translation.Menu.Get("Tooltip:Change lane arrows");

        protected override bool IsVisible() => true;

        public override KeybindSetting U_OverrideTooltipShortcutKey() => KeybindSettingsBase.LaneArrowTool;
    }
}
