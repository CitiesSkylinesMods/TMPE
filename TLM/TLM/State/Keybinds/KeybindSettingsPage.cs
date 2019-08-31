namespace TrafficManager.State.Keybinds {
    using UI;

    public class KeybindSettingsPage : KeybindSettingsBase {
        private void Awake() {
            TryCreateConfig();

            keybindUi_.BeginForm(component);

            // Section: Global
            keybindUi_.AddGroup(Translation.Get("Keybind_category_Global"),
                                CreateUI_Global);

            // Section: Lane Connector Tool
            keybindUi_.AddGroup(Translation.Get("Keybind_category_LaneConnector"),
                                CreateUI_LaneConnector);
        }

        /// <summary>
        /// Fill Global keybinds section
        /// </summary>
        private void CreateUI_Global() {
            AddReadOnlyKeybind(Translation.Get("Keybind_Exit_subtool"),
                               ToolCancelViewOnly);

            AddKeybindRowUI(Translation.Get("Keybind_toggle_TMPE_main_menu"),
                            ToggleMainMenu);
            ToggleMainMenu.OnKeyChanged(() => {
                if (LoadingExtension.BaseUI != null &&
                    LoadingExtension.BaseUI.MainMenuButton != null) {
                    LoadingExtension.BaseUI.MainMenuButton.UpdateTooltip();
                }
            });

            AddKeybindRowUI(Translation.Get("Keybind_toggle_traffic_lights_tool"),
                            ToggleTrafficLightTool);
            AddKeybindRowUI(Translation.Get("Keybind_use_lane_arrow_tool"),
                            LaneArrowTool);
            AddKeybindRowUI(Translation.Get("Keybind_use_lane_connections_tool"),
                            LaneConnectionsTool);
            AddKeybindRowUI(Translation.Get("Keybind_use_priority_signs_tool"),
                            PrioritySignsTool);
            AddKeybindRowUI(Translation.Get("Keybind_use_junction_restrictions_tool"),
                            JunctionRestrictionsTool);
            AddKeybindRowUI(Translation.Get("Keybind_use_speed_limits_tool"),
                            SpeedLimitsTool);
        }

        /// <summary>
        /// Fill Lane Connector keybinds section
        /// </summary>
        private void CreateUI_LaneConnector() {
            AddKeybindRowUI(Translation.Get("Keybind_lane_connector_stay_in_lane"),
                            LaneConnectorStayInLane);

            // First key binding is readonly (editable1=false)
            AddAlternateKeybindUI(Translation.Get("Keybind_lane_connector_delete"),
                                  LaneConnectorDelete, false, true);
        }
    }
}