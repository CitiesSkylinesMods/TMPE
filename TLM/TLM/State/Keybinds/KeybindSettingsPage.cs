namespace TrafficManager.State.Keybinds {
    using TrafficManager.UI;

    public class KeybindSettingsPage : KeybindSettingsBase {
        private void Awake() {
            TryCreateConfig();

            keybindUi_.BeginForm(component);

            // Section: Global
            keybindUi_.AddGroup(Translation.Options.Get("KeybindCategory:Global"),
                                CreateUI_Global);

            // Section: Lane Connector Tool
            keybindUi_.AddGroup(Translation.Options.Get("KeybindCategory:LaneConnector"),
                                CreateUI_LaneConnector);
        }

        /// <summary>
        /// Fill Global keybinds section
        /// </summary>
        private void CreateUI_Global() {
            AddReadOnlyKeybind(Translation.Options.Get("Keybind:Generic exit subtool key"),
                               Esc);

            AddKeybindRowUI(Translation.Options.Get("Keybind:Toggle Main Menu"),
                            ToggleMainMenu);
            ToggleMainMenu.OnKeyChanged(() => {
                if (ModUI.Instance != null &&
                    ModUI.Instance.MainMenuButton != null) {
                    ModUI.Instance.MainMenuButton.UpdateButtonImageAndTooltip();
                }
            });

            AddKeybindRowUI(Translation.Options.Get("Keybind.Main:Toggle traffic lights tool"),
                            ToggleTrafficLightTool);
            AddKeybindRowUI(Translation.Options.Get("Keybind.Main:Lane arrow tool"),
                            LaneArrowTool);
            AddKeybindRowUI(Translation.Options.Get("Keybind.Main:Lane connections tool"),
                            LaneConnectionsTool);
            AddKeybindRowUI(Translation.Options.Get("Keybind.Main:Priority signs tool"),
                            PrioritySignsTool);
            AddKeybindRowUI(Translation.Options.Get("Keybind.Main:Junction restrictions tool"),
                            JunctionRestrictionsTool);
            AddKeybindRowUI(Translation.Options.Get("Keybind.Main:Speed limits tool"),
                            SpeedLimitsTool);
        }

        /// <summary>
        /// Fill Lane Connector keybinds section
        /// </summary>
        private void CreateUI_LaneConnector() {
            AddKeybindRowUI(Translation.Options.Get("Keybind.LaneConnector:Stay in lane"),
                            LaneConnectorStayInLane);

            // First key binding is readonly (editable1=false)
            AddAlternateKeybindUI(
                Translation.Options.Get("Keybind.LaneConnector:Delete"),
                RestoreDefaultsKey,
                false,
                true);
        }
    }
}
