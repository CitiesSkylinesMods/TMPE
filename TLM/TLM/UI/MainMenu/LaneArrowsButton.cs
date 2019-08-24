namespace TrafficManager.UI.MainMenu {
    using State.Keybinds;
    using UXLibrary.Keyboard;

    public class LaneArrowsButton : MenuToolModeButton {
        public override string Tooltip => "Change_lane_arrows";

        public override bool Visible => true;

        public override KeybindSetting ShortcutKey => KeybindSettingsBase.LaneArrowTool;

        protected override ToolMode ToolMode => ToolMode.LaneArrows;

        protected override ButtonFunction Function => ButtonFunction.LaneArrows;
    }
}