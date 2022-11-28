namespace TrafficManager.UI.MainMenu {
    using TrafficManager.State;
    using TrafficManager.State.Keybinds;
    using TrafficManager.U;
    using TrafficManager.Util;

    public class JunctionRestrictionsButton : BaseMenuToolModeButton {
        protected override ToolMode ToolMode => ToolMode.JunctionRestrictions;

        public override void SetupButtonSkin(AtlasBuilder futureAtlas) {
            // Button background (from BackgroundPrefix) is provided by MainMenuPanel.Start
            this.Skin = ButtonSkin.CreateSimple(
                                      foregroundPrefix: "JunctionRestrictions",
                                      backgroundPrefix: UConst.MAINMENU_ROUND_BUTTON_BG)
                                  .CanHover(foreground: false)
                                  .CanActivate();
            this.Skin.UpdateAtlasBuilder(
                atlasBuilder: futureAtlas,
                spriteSize: new IntVector2(50));
        }

        protected override string U_OverrideTooltipText() =>
            Translation.Menu.Get("Tooltip:Junction restrictions");

        public override KeybindSetting U_OverrideTooltipShortcutKey() => KeybindSettingsBase.JunctionRestrictionsTool;

        protected override bool IsVisible() => IsButtonEnabled();

        public static bool IsButtonEnabled() => SavedGameOptions.Instance.junctionRestrictionsEnabled;
    }
}
