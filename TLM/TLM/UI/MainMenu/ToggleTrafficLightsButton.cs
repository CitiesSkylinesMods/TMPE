namespace TrafficManager.UI.MainMenu {
    using TrafficManager.State.Keybinds;

    public class ToggleTrafficLightsButton : BaseMenuToolModeButton {
        protected override ToolMode ToolMode => ToolMode.SwitchTrafficLight;

        protected override ButtonFunction Function => ButtonFunction.ToggleTrafficLights;

        public override string GetTooltip() => Translation.Menu.Get("Tooltip:Switch traffic lights");

        public override bool IsVisible() => true;

        public override KeybindSetting ShortcutKey => KeybindSettingsBase.ToggleTrafficLightTool;
    }
}
