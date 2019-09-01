namespace TrafficManager.UI.MainMenu {
    using State;
    using State.Keybinds;

    public class PrioritySignsButton : MenuToolModeButton {
        protected override ToolMode ToolMode => ToolMode.AddPrioritySigns;

        protected override ButtonFunction Function => ButtonFunction.PrioritySigns;

        public override string Tooltip => Translation.Menu.Get("Add priority signs");

        public override bool Visible => Options.prioritySignsEnabled;

        public override KeybindSetting ShortcutKey => KeybindSettingsBase.PrioritySignsTool;
    }
}