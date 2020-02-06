namespace TrafficManager.UI.MainMenu {
    using TrafficManager.State;
    using TrafficManager.State.Keybinds;

    public class PrioritySignsButton : BaseMenuToolModeButton {
        protected override ToolMode ToolMode => ToolMode.AddPrioritySigns;

        protected override ButtonFunction Function => ButtonFunction.PrioritySigns;

        public override string Tooltip =>
            Translation.Menu.Get("Tooltip:Add priority signs") + "\n" +
            Translation.Menu.Get("Tooltip.Keybinds:Add priority signs");

        public override bool Visible => Options.prioritySignsEnabled;

        public override KeybindSetting ShortcutKey => KeybindSettingsBase.PrioritySignsTool;
    }
}
