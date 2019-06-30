using TrafficManager.UI;

namespace TrafficManager.State.Keybinds {
    public class KeybindSettingsPage : KeybindSettingsBase {
        private void Awake() {
            TryCreateConfig();

            BeginForm();
            ReadOnlyKeybindUI(Translation.GetString("Keybind_Exit_subtool"),
                              ToolCancelViewOnly);

            AddKeybindRowUI(Translation.GetString("Keybind_toggle_TMPE_main_menu"),
                            ToggleMainMenu);

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

            // New section: Lane Connector Tool
            AddKeybindRowUI(Translation.GetString("Keybind_lane_connector_stay_in_lane"),
                            LaneConnectorStayInLane);

            ReadOnlyKeybindUI(Translation.GetString("Keybind_lane_connector_delete"),
                              LaneConnectorDelete);
            AddAlternateUiControl(LaneConnectorDelete);
        }
    }
}