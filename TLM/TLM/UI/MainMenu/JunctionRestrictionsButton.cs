namespace TrafficManager.UI.MainMenu {
    using TrafficManager.State;
    using TrafficManager.State.Keybinds;

    public class JunctionRestrictionsButton : BaseMenuToolModeButton {
        protected override ToolMode ToolMode => ToolMode.JunctionRestrictions;

        protected override ButtonFunction Function => ButtonFunction.JunctionRestrictions;

        public override string Tooltip => Translation.Menu.Get("Tooltip:Junction restrictions");

        public override bool Visible => Options.junctionRestrictionsEnabled;

        public override KeybindSetting ShortcutKey => KeybindSettingsBase.JunctionRestrictionsTool;
    }
}
