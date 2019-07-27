namespace TrafficManager.UI.MainMenu {
    using State.Keybinds;

    public class ToggleTrafficLightsButton : MenuToolModeButton {
        protected override ToolMode ToolMode => ToolMode.SwitchTrafficLight;

        protected override ButtonFunction Function => ButtonFunction.ToggleTrafficLights;

        public override string Tooltip => "Switch_traffic_lights";

        public override bool Visible => true;

        public override KeybindSetting ShortcutKey => KeybindSettingsBase.ToggleTrafficLightTool;
    }
}