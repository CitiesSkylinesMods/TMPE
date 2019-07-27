﻿namespace TrafficManager.UI.MainMenu {
    using State;
    using State.Keybinds;

    public class JunctionRestrictionsButton : MenuToolModeButton {
        protected override ToolMode ToolMode => ToolMode.JunctionRestrictions;

        protected override ButtonFunction Function => ButtonFunction.JunctionRestrictions;

        public override string Tooltip => "Junction_restrictions";

        public override bool Visible => Options.junctionRestrictionsEnabled;

        public override KeybindSetting ShortcutKey => KeybindSettingsBase.JunctionRestrictionsTool;
    }
}