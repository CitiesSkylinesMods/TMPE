namespace TrafficManager.UI.MainMenu {
    using State;
    using State.Keybinds;
    using UXLibrary.Keyboard;

    public class PrioritySignsButton : MenuToolModeButton {
        public override string Tooltip => "Add_priority_signs";

        public override bool Visible => Options.prioritySignsEnabled;

        public override KeybindSetting ShortcutKey => KeybindSettingsBase.PrioritySignsTool;

        protected override ToolMode ToolMode => ToolMode.AddPrioritySigns;

        protected override ButtonFunction Function => ButtonFunction.PrioritySigns;
    }
}