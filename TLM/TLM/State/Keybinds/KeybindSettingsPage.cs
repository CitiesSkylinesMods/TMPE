using TrafficManager.UI;

namespace TrafficManager.State.Keybinds {
    public class KeybindSettingsPage : KeybindSettingsBase {
        private void Awake() {
            TryCreateConfig();

            AddReadOnlyUi(Translation.GetString("Keybind_Exit_subtool"),
                          ToolCancelViewOnly);

            AddUiControl(Translation.GetString("Keybind_toggle_TMPE_main_menu"),
                         ToggleMainMenu);

            AddUiControl(Translation.GetString("Keybind_toggle_traffic_lights_tool"),
                         ToggleTrafficLightTool);
            AddUiControl(Translation.GetString("Keybind_use_lane_arrow_tool"),
                         LaneArrowTool);
            AddUiControl(Translation.GetString("Keybind_use_lane_connections_tool"),
                         LaneConnectionsTool);
            AddUiControl(Translation.GetString("Keybind_use_priority_signs_tool"),
                         PrioritySignsTool);
            AddUiControl(Translation.GetString("Keybind_use_junction_restrictions_tool"),
                         JunctionRestrictionsTool);
            AddUiControl(Translation.GetString("Keybind_use_speed_limits_tool"),
                         SpeedLimitsTool);

            // New section: Lane Connector Tool
            AddUiControl(Translation.GetString("Keybind_lane_connector_stay_in_lane"),
                         LaneConnectorStayInLane);

            AddUiControl(Translation.GetString("Keybind_lane_connector_delete"),
                         LaneConnectorDelete);
        }
    }
}