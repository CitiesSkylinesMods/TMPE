namespace TrafficManager.UI.MainMenu {
    using System.Collections.Generic;
    using TrafficManager.State;
    using TrafficManager.State.Keybinds;
    using TrafficManager.U.Button;

    public class JunctionRestrictionsButton : BaseMenuToolModeButton {
        protected override ToolMode ToolMode => ToolMode.JunctionRestrictions;

        public override void SetupButtonSkin(List<string> atlasKeys) {
        }

        protected override ButtonFunction Function => new ButtonFunction("JunctionRestrictions");

        public override string GetTooltip() =>
            Translation.Menu.Get("Tooltip:Junction restrictions");

        public override bool IsVisible() => Options.junctionRestrictionsEnabled;

        public override KeybindSetting ShortcutKey => KeybindSettingsBase.JunctionRestrictionsTool;
    }
}
