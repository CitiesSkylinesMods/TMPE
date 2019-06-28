using ColossalFramework;
using TrafficManager.State;
using TrafficManager.State.Keybinds;

namespace TrafficManager.UI.MainMenu {
    public class PrioritySignsButton : MenuToolModeButton {
        public override ToolMode ToolMode => ToolMode.AddPrioritySigns;
        public override ButtonFunction Function => ButtonFunction.PrioritySigns;
        public override string Tooltip => "Add_priority_signs";
        public override bool Visible => Options.prioritySignsEnabled;
        public override SavedInputKey ShortcutKey => KeymappingSettings.KeyPrioritySignsTool;
    }
}