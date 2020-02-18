namespace TrafficManager.UI.MainMenu {
    using System.Collections.Generic;
    using TrafficManager.State;
    using TrafficManager.State.Keybinds;
    using TrafficManager.U.Button;

    public class PrioritySignsButton : BaseMenuToolModeButton {
        protected override ToolMode ToolMode => ToolMode.AddPrioritySigns;

        public override void SetupButtonSkin(List<string> atlasKeys) {
        }

        protected override ButtonFunction Function => new ButtonFunction("PrioritySigns");

        public override string GetTooltip() =>
            Translation.Menu.Get("Tooltip:Add priority signs") + "\n" +
            Translation.Menu.Get("Tooltip.Keybinds:Add priority signs");

        public override bool IsVisible() => Options.prioritySignsEnabled;

        public override KeybindSetting ShortcutKey => KeybindSettingsBase.PrioritySignsTool;
    }
}
