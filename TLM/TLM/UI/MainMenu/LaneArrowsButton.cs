namespace TrafficManager.UI.MainMenu {
    using State.Keybinds;

    public class LaneArrowsButton : MenuToolModeButton {
        protected override ToolMode ToolMode => ToolMode.LaneChange;

        protected override ButtonFunction Function => ButtonFunction.LaneArrows;

        public override string Tooltip => "Change_lane_arrows";

        public override bool Visible => true;

        public override KeybindSetting ShortcutKey => KeybindSettingsBase.LaneArrowTool;
    }
}