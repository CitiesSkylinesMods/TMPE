using ColossalFramework;
using TrafficManager.State;
using TrafficManager.State.Keybinds;

namespace TrafficManager.UI.MainMenu {
    public class LaneArrowsButton : MenuToolModeButton {
        public override ToolMode ToolMode => ToolMode.LaneChange;
        public override ButtonFunction Function => ButtonFunction.LaneArrows;
        public override string Tooltip => "Change_lane_arrows";
        public override bool Visible => true;
        public override KeybindSetting ShortcutKey => KeybindSettingsBase.LaneArrowTool;
    }
}