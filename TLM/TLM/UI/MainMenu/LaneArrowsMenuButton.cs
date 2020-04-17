namespace TrafficManager.UI.MainMenu {
    using System.Collections.Generic;
    using TrafficManager.RedirectionFramework;
    using TrafficManager.State.Keybinds;
    using TrafficManager.U.Button;

    public class LaneArrowsMenuButton : BaseMenuToolModeButton {
        protected override ToolMode ToolMode => ToolMode.LaneArrows;

        public override void SetupButtonSkin(HashSet<string> atlasKeys) {
            // Button backround (from BackgroundPrefix) is provided by MainMenuPanel.Start
            this.Skin = new U.Button.ButtonSkin() {
                                                      Prefix = "LaneArrows",
                                                      BackgroundPrefix = "RoundButton",
                                                      BackgroundHovered = true,
                                                      BackgroundActive = true,
                                                      ForegroundActive = true,
                                                  };
            atlasKeys.AddRange(this.Skin.CreateAtlasKeyset());
        }

        protected override string GetTooltip() => Translation.Menu.Get("Tooltip:Change lane arrows");

        protected override bool IsVisible() => true;

        public override KeybindSetting GetShortcutKey() => KeybindSettingsBase.LaneArrowTool;
    }
}
