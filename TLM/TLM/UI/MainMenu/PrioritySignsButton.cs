namespace TrafficManager.UI.MainMenu {
    using System.Collections.Generic;
    using TrafficManager.RedirectionFramework;
    using TrafficManager.State;
    using TrafficManager.State.Keybinds;
    using TrafficManager.U.Button;

    public class PrioritySignsButton : BaseMenuToolModeButton {
        protected override ToolMode ToolMode => ToolMode.AddPrioritySigns;

        public override void SetupButtonSkin(HashSet<string> atlasKeys) {
            // Button backround (from BackgroundPrefix) is provided by MainMenuPanel.Start
            this.Skin = new U.Button.ButtonSkin() {
                                                      Prefix = "PrioritySigns",
                                                      BackgroundPrefix = "RoundButton",
                                                      BackgroundHovered = true,
                                                      BackgroundActive = true,
                                                      ForegroundActive = true,
                                                  };
            atlasKeys.AddRange(this.Skin.CreateAtlasKeyset());
        }

        protected override ButtonFunction Function => new ButtonFunction("PrioritySigns");

        public override string GetTooltip() =>
            Translation.Menu.Get("Tooltip:Add priority signs") + "\n" +
            Translation.Menu.Get("Tooltip.Keybinds:Add priority signs");

        public override KeybindSetting ShortcutKey => KeybindSettingsBase.PrioritySignsTool;

        public override bool IsVisible() => IsButtonEnabled();

        public static bool IsButtonEnabled() => Options.prioritySignsEnabled;
    }
}
