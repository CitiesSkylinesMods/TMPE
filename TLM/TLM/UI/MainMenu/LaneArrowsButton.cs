namespace TrafficManager.UI.MainMenu {
    using TrafficManager.State.Keybinds;

    public class LaneArrowsButton : MenuToolModeButton {
        protected override ToolMode ToolMode => ToolMode.LaneChange;

        protected override ButtonFunction Function => ButtonFunction.LaneArrows;

        public override string Tooltip => Translation.Menu.Get("Tooltip:Change lane arrows") + "\n" + Translation.Menu.Get("Tooltip.Keybinds:Change lane arrows");

        public override bool Visible => true;

        public override KeybindSetting ShortcutKey => KeybindSettingsBase.LaneArrowTool;
    }
}
