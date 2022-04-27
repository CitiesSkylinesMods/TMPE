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

            // Section: Speed Limits tool
            keybindUi_.AddGroup(Translation.Options.Get("KeybindCategory:SpeedLimits"),
                                CreateUI_SpeedLimits);
        }

        /// <summary>
        /// Fill Global keybinds section
        /// </summary>
        private void CreateUI_Global() {
            AddReadOnlyKeybind(Translation.Options.Get("Keybind:Generic exit subtool key"),
                               Esc);
            AddReadOnlyKeybind(Translation.Options.Get("Keybind:Overground view"),
                               ElevationUp,
                               autoUpdateText: true);
            AddReadOnlyKeybind(Translation.Options.Get("Keybind:Underground view"),
                               ElevationDown,
                               autoUpdateText: true);

            AddKeybindRowUI(Translation.Options.Get("Keybind:Toggle Main Menu"),
                            ToggleMainMenu);
            ToggleMainMenu.OnKeyChanged(() => {
                if (ModUI.Instance != null &&
                    ModUI.Instance.MainMenuButton != null) {
                    ModUI.Instance.MainMenuButton.UpdateButtonSkinAndTooltip();
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

        /// <summary>Fill Lane Connector keybinds section.</summary>
        private void CreateUI_LaneConnector() {
            AddKeybindRowUI(label: Translation.Options.Get("Keybind.LaneConnector:Stay in lane"),
                            keybind: LaneConnectorStayInLane);

            // First key binding is readonly (editable1=false)
            AddAlternateKeybindUI(
                title: Translation.Options.Get("Keybind.LaneConnector:Delete"),
                keybind: RestoreDefaultsKey,
                editable1: false,
                editable2: true);
        }

        /// <summary>Fill Lane Connector keybinds section.</summary>
        private void CreateUI_SpeedLimits() {
            AddAlternateKeybindUI(
                title: Translation.Options.Get("Keybind.SpeedLimits:Decrease selected speed"),
                keybind: SpeedLimitsLess,
                editable1: true,
                editable2: true);
            AddAlternateKeybindUI(
                title: Translation.Options.Get("Keybind.SpeedLimits:Increase selected speed"),
                keybind: SpeedLimitsMore,
                editable1: true,
                editable2: true);
        }
    }
}
