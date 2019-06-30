using TrafficManager.State;
using TrafficManager.State.Keybinds;

namespace TrafficManager.UI.MainMenu {
    public class JunctionRestrictionsButton : MenuToolModeButton {
        public override ToolMode ToolMode => ToolMode.JunctionRestrictions;
        public override ButtonFunction Function => ButtonFunction.JunctionRestrictions;
        public override string Tooltip => "Junction_restrictions";
        public override bool Visible => Options.junctionRestrictionsEnabled;
        public override KeybindSetting ShortcutKey => KeybindSettingsBase.JunctionRestrictionsTool;
    }
}