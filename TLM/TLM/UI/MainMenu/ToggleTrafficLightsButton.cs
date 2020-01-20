﻿namespace TrafficManager.UI.MainMenu {
    using TrafficManager.State.Keybinds;

    public class ToggleTrafficLightsButton : MenuToolModeButton {
        protected override ToolMode ToolMode => ToolMode.SwitchTrafficLight;

        protected override ButtonFunction Function => ButtonFunction.ToggleTrafficLights;

        public override string Tooltip => Translation.Menu.Get("Tooltip:Switch traffic lights");

        public override bool Visible => true;

        public override KeybindSetting ShortcutKey => KeybindSettingsBase.ToggleTrafficLightTool;
    }
}
