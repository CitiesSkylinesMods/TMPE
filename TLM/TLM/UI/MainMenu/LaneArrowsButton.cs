namespace TrafficManager.UI.MainMenu {
    using System.Collections.Generic;
    using TrafficManager.RedirectionFramework;
    using TrafficManager.State.Keybinds;
    using TrafficManager.U.Button;

    public class LaneArrowsButton : BaseMenuToolModeButton {
        protected override ToolMode ToolMode => ToolMode.LaneChange;

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

        protected override ButtonFunction Function => new ButtonFunction("LaneArrows");

        public override string GetTooltip() =>
            Translation.Menu.Get("Tooltip:Change lane arrows") + "\n" +
            Translation.Menu.Get("Tooltip.Keybinds:Change lane arrows");

        public override bool IsVisible() => true;

        public override KeybindSetting ShortcutKey => KeybindSettingsBase.LaneArrowTool;
    }
}
