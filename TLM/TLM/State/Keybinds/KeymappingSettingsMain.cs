using TrafficManager.UI;

namespace TrafficManager.State.Keybinds {
    public class KeymappingSettingsMain : KeymappingSettings {
        private void Awake() {
            TryCreateConfig();

            AddReadOnlyKeymapping(Translation.GetString("Keybind_Exit_subtool"),
                                  KeyToolCancel_ViewOnly);

            AddKeymapping(Translation.GetString("Keybind_toggle_TMPE_main_menu"),
                          KeyToggleTMPEMainMenu, "Global");

            AddKeymapping(Translation.GetString("Keybind_toggle_traffic_lights_tool"),
                          KeyToggleTrafficLightTool, "Global");
            AddKeymapping(Translation.GetString("Keybind_use_lane_arrow_tool"),
                          KeyLaneArrowTool, "Global");
            AddKeymapping(Translation.GetString("Keybind_use_lane_connections_tool"),
                          KeyLaneConnectionsTool, "Global");
            AddKeymapping(Translation.GetString("Keybind_use_priority_signs_tool"),
                          KeyPrioritySignsTool, "Global");
            AddKeymapping(Translation.GetString("Keybind_use_junction_restrictions_tool"),
                          KeyJunctionRestrictionsTool, "Global");
            AddKeymapping(Translation.GetString("Keybind_use_speed_limits_tool"),
                          KeySpeedLimitsTool, "Global");

            // New section: Lane Connector Tool
            AddKeymapping(Translation.GetString("Keybind_lane_connector_stay_in_lane"),
                          KeyLaneConnectorStayInLane, "LaneConnector");

            AddKeymapping(Translation.GetString("Keybind_lane_connector_delete"),
                          KeyLaneConnectorDelete, "LaneConnector");
        }
    }
}