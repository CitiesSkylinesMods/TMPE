namespace TrafficManager.UI.MainMenu {
    using System.Collections.Generic;
    using TrafficManager.State;
    using TrafficManager.State.Keybinds;
    using TrafficManager.U.Button;

    public class JunctionRestrictionsButton : BaseMenuToolModeButton {
        protected override ToolMode ToolMode => ToolMode.JunctionRestrictions;

        public override void SetupButtonSkin(List<string> atlasKeys) {
            // Button backround (from BackgroundPrefix) is provided by MainMenuPanel.Start
            this.Skin = new U.Button.ButtonSkin() {
                                                      Prefix = "JunctionRestrictions",
                                                      BackgroundPrefix = "RoundButton",
                                                      BackgroundHovered = true,
                                                      BackgroundActive = true,
                                                      ForegroundActive = true,
                                                  };
            atlasKeys.AddRange(this.Skin.CreateAtlasKeysList());
        }

        protected override ButtonFunction Function => new ButtonFunction("JunctionRestrictions");

        public override string GetTooltip() =>
            Translation.Menu.Get("Tooltip:Junction restrictions");

        public override bool IsVisible() => Options.junctionRestrictionsEnabled;

        public override KeybindSetting ShortcutKey => KeybindSettingsBase.JunctionRestrictionsTool;
    }
}
