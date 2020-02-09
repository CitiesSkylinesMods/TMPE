namespace TrafficManager.UI.MainMenu {
    using TrafficManager.State.Keybinds;

    public class LaneArrowsButton : BaseMenuToolModeButton {
        protected override ToolMode ToolMode => ToolMode.LaneChange;

        protected override ButtonFunction Function => ButtonFunction.LaneArrows;

        public override string GetTooltip() =>
            Translation.Menu.Get("Tooltip:Change lane arrows") + "\n" +
            Translation.Menu.Get("Tooltip.Keybinds:Change lane arrows");

        public override bool IsVisible() => true;

        public override KeybindSetting ShortcutKey => KeybindSettingsBase.LaneArrowTool;
    }
}
