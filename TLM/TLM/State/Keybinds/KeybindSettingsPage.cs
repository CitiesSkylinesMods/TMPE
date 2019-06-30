using TrafficManager.UI;

namespace TrafficManager.State.Keybinds {
    public class KeybindSettingsPage : KeybindSettingsBase {
        private void Awake() {
            TryCreateConfig();

            keybindUi_.BeginForm(component);
            keybindUi_.AddGroup(Translation.GetString("Keybind_category_Global"),
                                CreateUI_Global);
            // New section: Lane Connector Tool
            keybindUi_.AddGroup(Translation.GetString("Keybind_category_LaneConnector"),
                                CreateUI_LaneConnector);
        }

        /// <summary>
        /// Fill Global keybinds section
        /// </summary>
        private void CreateUI_Global() {
            AddReadOnlyKeybind(Translation.GetString("Keybind_Exit_subtool"),
                               ToolCancelViewOnly);

            AddKeybindRowUI(Translation.GetString("Keybind_toggle_TMPE_main_menu"),
                            ToggleMainMenu);
            ToggleMainMenu.OnKeyChanged(() => {
                if (LoadingExtension.BaseUI != null &&
                    LoadingExtension.BaseUI.MainMenuButton != null) {
                    LoadingExtension.BaseUI.MainMenuButton.UpdateTooltip();
                }
            });

            AddKeybindRowUI(Translation.GetString("Keybind_toggle_traffic_lights_tool"),
                            ToggleTrafficLightTool);
            AddKeybindRowUI(Translation.GetString("Keybind_use_lane_arrow_tool"),
                            LaneArrowTool);
            AddKeybindRowUI(Translation.GetString("Keybind_use_lane_connections_tool"),
                            LaneConnectionsTool);
            AddKeybindRowUI(Translation.GetString("Keybind_use_priority_signs_tool"),
                            PrioritySignsTool);
            AddKeybindRowUI(Translation.GetString("Keybind_use_junction_restrictions_tool"),
                            JunctionRestrictionsTool);
            AddKeybindRowUI(Translation.GetString("Keybind_use_speed_limits_tool"),
                            SpeedLimitsTool);
        }

        /// <summary>
        /// Fill Lane Connector keybinds section
        /// </summary>
        private void CreateUI_LaneConnector() {
            AddKeybindRowUI(Translation.GetString("Keybind_lane_connector_stay_in_lane"),
                            LaneConnectorStayInLane);

            AddAlternateKeybindUI(Translation.GetString("Keybind_lane_connector_delete"),
                                  LaneConnectorDelete);
        }
    }
}