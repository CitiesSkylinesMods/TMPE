namespace TrafficManager.UI.MainMenu {
    using State.Keybinds;
    using UXLibrary.Keyboard;

    public class ToggleTrafficLightsButton : MenuToolModeButton {
        public override string Tooltip => "Switch_traffic_lights";

        public override bool Visible => true;

        public override KeybindSetting ShortcutKey => KeybindSettingsBase.ToggleTrafficLightTool;

        protected override ToolMode ToolMode => ToolMode.SwitchTrafficLight;

        protected override ButtonFunction Function => ButtonFunction.ToggleTrafficLights;
    }
}