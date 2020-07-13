namespace TrafficManager.UI.MainMenu {
    using System.Collections.Generic;
    using TrafficManager.RedirectionFramework;
    using TrafficManager.State;
    using TrafficManager.State.Keybinds;
    using TrafficManager.Util;

    public class PrioritySignsButton : BaseMenuToolModeButton {
        protected override ToolMode ToolMode => ToolMode.AddPrioritySigns;

        public override void SetupButtonSkin(HashSet<U.AtlasSpriteDef> spriteDefs) {
            // Button backround (from BackgroundPrefix) is provided by MainMenuPanel.Start
            this.Skin = new U.ButtonSkin() {
                Prefix = "PrioritySigns",
                BackgroundPrefix = "RoundButton",
                BackgroundHovered = true,
                BackgroundActive = true,
                ForegroundActive = true,
            };
            spriteDefs.AddRange(this.Skin.CreateAtlasSpriteSet(new IntVector2(50)));
        }

        protected override string U_OverrideTooltipText() => Translation.Menu.Get("Tooltip:Add priority signs");

        public override KeybindSetting U_OverrideTooltipShortcutKey() => KeybindSettingsBase.PrioritySignsTool;

        protected override bool IsVisible() => IsButtonEnabled();

        public static bool IsButtonEnabled() => Options.prioritySignsEnabled;
    }
}
