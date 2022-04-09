namespace TrafficManager.UI.MainMenu {
    using TrafficManager.State;
    using TrafficManager.State.Keybinds;
    using TrafficManager.U;
    using TrafficManager.Util;

    public class PrioritySignsButton : BaseMenuToolModeButton {
        protected override ToolMode ToolMode => ToolMode.AddPrioritySigns;

        public override void SetupButtonSkin(AtlasBuilder futureAtlas) {
            // Button backround (from BackgroundPrefix) is provided by MainMenuPanel.Start
            this.Skin = ButtonSkin.CreateSimple(
                                      foregroundPrefix: "PrioritySigns",
                                      backgroundPrefix: UConst.MAINMENU_ROUND_BUTTON_BG)
                                  .CanHover(foreground: false)
                                  .CanActivate(foreground: false);
            this.Skin.UpdateAtlasBuilder(
                atlasBuilder: futureAtlas,
                spriteSize: new IntVector2(128));
        }

        protected override string U_OverrideTooltipText() =>
            Translation.Menu.Get("Tooltip:Add priority signs");

        public override KeybindSetting U_OverrideTooltipShortcutKey() =>
            KeybindSettingsBase.PrioritySignsTool;

        protected override bool IsVisible() => IsButtonEnabled();

        public static bool IsButtonEnabled() => Options.prioritySignsEnabled;
    }
}