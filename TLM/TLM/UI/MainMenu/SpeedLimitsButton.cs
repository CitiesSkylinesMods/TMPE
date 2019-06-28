using ColossalFramework;
using TrafficManager.State;

namespace TrafficManager.UI.MainMenu {
    public class SpeedLimitsButton : MenuToolModeButton {
        public override ToolMode ToolMode => ToolMode.SpeedLimits;
        public override ButtonFunction Function => ButtonFunction.SpeedLimits;
        public override string Tooltip => "Speed_limits";
        public override bool Visible => Options.customSpeedLimitsEnabled;
        public override SavedInputKey ShortcutKey => OptionsKeymapping.KeySpeedLimitsTool;
    }
}