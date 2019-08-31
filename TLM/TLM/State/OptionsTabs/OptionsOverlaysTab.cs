namespace TrafficManager.State {
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using JetBrains.Annotations;
    using UI;

    public static class OptionsOverlaysTab {
        private static UICheckBox _prioritySignsOverlayToggle;
        private static UICheckBox _timedLightsOverlayToggle;
        private static UICheckBox _speedLimitsOverlayToggle;
        private static UICheckBox _vehicleRestrictionsOverlayToggle;
        private static UICheckBox _parkingRestrictionsOverlayToggle;
        private static UICheckBox _junctionRestrictionsOverlayToggle;
        private static UICheckBox _connectedLanesOverlayToggle;
        private static UICheckBox _nodesOverlayToggle;
        private static UICheckBox _vehicleOverlayToggle;
        private static UICheckBox _showLanesToggle;
#if DEBUG
        [UsedImplicitly]
        private static UICheckBox _citizenOverlayToggle;

        [UsedImplicitly]
        private static UICheckBox _buildingOverlayToggle;
#endif

        private static string T(string s) {
            return Translation.Get(s);
        }

        internal static void MakeSettings_Overlays(UITabstrip tabStrip, int tabIndex) {
            Options.AddOptionTab(tabStrip, T("Overlays"));
            tabStrip.selectedIndex = tabIndex;

            var currentPanel = tabStrip.tabContainer.components[tabIndex] as UIPanel;
            currentPanel.autoLayout = true;
            currentPanel.autoLayoutDirection = LayoutDirection.Vertical;
            currentPanel.autoLayoutPadding.top = 5;
            currentPanel.autoLayoutPadding.left = 10;
            currentPanel.autoLayoutPadding.right = 10;

            var panelHelper = new UIHelper(currentPanel);

            _prioritySignsOverlayToggle = panelHelper.AddCheckbox(
                                             T("Priority_signs"),
                                             Options.prioritySignsOverlay,
                                             OnPrioritySignsOverlayChanged) as UICheckBox;
            _timedLightsOverlayToggle = panelHelper.AddCheckbox(
                                           T("Timed_traffic_lights"),
                                           Options.timedLightsOverlay,
                                           OnTimedLightsOverlayChanged) as UICheckBox;
            _speedLimitsOverlayToggle = panelHelper.AddCheckbox(
                                           T("Speed_limits"),
                                           Options.speedLimitsOverlay,
                                           OnSpeedLimitsOverlayChanged) as UICheckBox;
            _vehicleRestrictionsOverlayToggle
                = panelHelper.AddCheckbox(
                      T("Vehicle_restrictions"),
                      Options.vehicleRestrictionsOverlay,
                      OnVehicleRestrictionsOverlayChanged) as UICheckBox;
            _parkingRestrictionsOverlayToggle
                = panelHelper.AddCheckbox(
                      T("Parking_restrictions"),
                      Options.parkingRestrictionsOverlay,
                      OnParkingRestrictionsOverlayChanged) as UICheckBox;
            _junctionRestrictionsOverlayToggle
                = panelHelper.AddCheckbox(
                      T("Junction_restrictions"),
                      Options.junctionRestrictionsOverlay,
                      OnJunctionRestrictionsOverlayChanged) as UICheckBox;
            _connectedLanesOverlayToggle = panelHelper.AddCheckbox(
                                              T("Connected_lanes"),
                                              Options.connectedLanesOverlay,
                                              OnConnectedLanesOverlayChanged) as UICheckBox;
            _nodesOverlayToggle = panelHelper.AddCheckbox(
                                     T("Nodes_and_segments"),
                                     Options.nodesOverlay,
                                     onNodesOverlayChanged) as UICheckBox;
            _showLanesToggle = panelHelper.AddCheckbox(
                                  T("Lanes"),
                                  Options.showLanes,
                                  onShowLanesChanged) as UICheckBox;
#if DEBUG
            _vehicleOverlayToggle = panelHelper.AddCheckbox(
                                       T("Vehicles"),
                                       Options.vehicleOverlay,
                                       onVehicleOverlayChanged) as UICheckBox;
            _citizenOverlayToggle = panelHelper.AddCheckbox(
                                       T("Citizens"),
                                       Options.citizenOverlay,
                                       onCitizenOverlayChanged) as UICheckBox;
            _buildingOverlayToggle = panelHelper.AddCheckbox(
                                        T("Buildings"),
                                        Options.buildingOverlay,
                                        onBuildingOverlayChanged) as UICheckBox;
#endif
        }

        public static void SetPrioritySignsOverlay(bool newPrioritySignsOverlay) {
            Options.prioritySignsOverlay = newPrioritySignsOverlay;

            if (_prioritySignsOverlayToggle != null) {
                _prioritySignsOverlayToggle.isChecked = newPrioritySignsOverlay;
            }

            UIBase.GetTrafficManagerTool(false)?.InitializeSubTools();
        }

                public static void SetTimedLightsOverlay(bool newTimedLightsOverlay) {
            Options.timedLightsOverlay = newTimedLightsOverlay;

            if (_timedLightsOverlayToggle != null) {
                _timedLightsOverlayToggle.isChecked = newTimedLightsOverlay;
            }

            UIBase.GetTrafficManagerTool(false)?.InitializeSubTools();
        }

        public static void SetSpeedLimitsOverlay(bool newSpeedLimitsOverlay) {
            Options.speedLimitsOverlay = newSpeedLimitsOverlay;

            if (_speedLimitsOverlayToggle != null) {
                _speedLimitsOverlayToggle.isChecked = newSpeedLimitsOverlay;
            }

            UIBase.GetTrafficManagerTool(false)?.InitializeSubTools();
        }

        public static void SetVehicleRestrictionsOverlay(bool newVehicleRestrictionsOverlay) {
            Options.vehicleRestrictionsOverlay = newVehicleRestrictionsOverlay;

            if (_vehicleRestrictionsOverlayToggle != null) {
                _vehicleRestrictionsOverlayToggle.isChecked = newVehicleRestrictionsOverlay;
            }

            UIBase.GetTrafficManagerTool(false)?.InitializeSubTools();
        }

        public static void SetParkingRestrictionsOverlay(bool newParkingRestrictionsOverlay) {
            Options.parkingRestrictionsOverlay = newParkingRestrictionsOverlay;

            if (_parkingRestrictionsOverlayToggle != null) {
                _parkingRestrictionsOverlayToggle.isChecked = newParkingRestrictionsOverlay;
            }

            UIBase.GetTrafficManagerTool(false)?.InitializeSubTools();
        }

        public static void SetJunctionRestrictionsOverlay(bool newValue) {
            Options.junctionRestrictionsOverlay = newValue;

            if (_junctionRestrictionsOverlayToggle != null) {
                _junctionRestrictionsOverlayToggle.isChecked = newValue;
            }

            UIBase.GetTrafficManagerTool(false)?.InitializeSubTools();
        }

        public static void SetConnectedLanesOverlay(bool newValue) {
            Options.connectedLanesOverlay = newValue;

            if (_connectedLanesOverlayToggle != null) {
                _connectedLanesOverlayToggle.isChecked = newValue;
            }
        }

        public static void SetNodesOverlay(bool newNodesOverlay) {
            Options.nodesOverlay = newNodesOverlay;

            if (_nodesOverlayToggle != null) {
                _nodesOverlayToggle.isChecked = newNodesOverlay;
            }

            UIBase.GetTrafficManagerTool(false)?.InitializeSubTools();
        }

        public static void SetVehicleOverlay(bool newVal) {
            Options.vehicleOverlay = newVal;

            if (_vehicleOverlayToggle != null) {
                _vehicleOverlayToggle.isChecked = newVal;
            }
        }

        private static void OnPrioritySignsOverlayChanged(bool newPrioritySignsOverlay) {
            if (!Options.IsGameLoaded()) {
                return;
            }

            Log._Debug($"prioritySignsOverlay changed to {newPrioritySignsOverlay}");
            Options.prioritySignsOverlay = newPrioritySignsOverlay;

            UIBase.GetTrafficManagerTool(false)?.InitializeSubTools();
        }

        private static void OnTimedLightsOverlayChanged(bool newTimedLightsOverlay) {
            if (!Options.IsGameLoaded()) {
                return;
            }

            Log._Debug($"timedLightsOverlay changed to {newTimedLightsOverlay}");
            Options.timedLightsOverlay = newTimedLightsOverlay;

            UIBase.GetTrafficManagerTool(false)?.InitializeSubTools();
        }

        private static void OnSpeedLimitsOverlayChanged(bool newSpeedLimitsOverlay) {
            if (!Options.IsGameLoaded()) {
                return;
            }

            Log._Debug($"speedLimitsOverlay changed to {newSpeedLimitsOverlay}");
            Options.speedLimitsOverlay = newSpeedLimitsOverlay;

            UIBase.GetTrafficManagerTool(false)?.InitializeSubTools();
        }

        private static void OnVehicleRestrictionsOverlayChanged(bool newVehicleRestrictionsOverlay) {
            if (!Options.IsGameLoaded()) {
                return;
            }

            Log._Debug($"vehicleRestrictionsOverlay changed to {newVehicleRestrictionsOverlay}");
            Options.vehicleRestrictionsOverlay = newVehicleRestrictionsOverlay;

            UIBase.GetTrafficManagerTool(false)?.InitializeSubTools();
        }

        private static void OnParkingRestrictionsOverlayChanged(bool newParkingRestrictionsOverlay) {
            if (!Options.IsGameLoaded()) {
                return;
            }

            Log._Debug($"parkingRestrictionsOverlay changed to {newParkingRestrictionsOverlay}");
            Options.parkingRestrictionsOverlay = newParkingRestrictionsOverlay;

            UIBase.GetTrafficManagerTool(false)?.InitializeSubTools();
        }

        private static void OnJunctionRestrictionsOverlayChanged(bool newValue) {
            if (!Options.IsGameLoaded()) {
                return;
            }

            Log._Debug($"junctionRestrictionsOverlay changed to {newValue}");
            Options.junctionRestrictionsOverlay = newValue;

            UIBase.GetTrafficManagerTool(false)?.InitializeSubTools();
        }

        private static void OnConnectedLanesOverlayChanged(bool newValue) {
            if (!Options.IsGameLoaded()) {
                return;
            }

            Log._Debug($"connectedLanesOverlay changed to {newValue}");
            Options.connectedLanesOverlay = newValue;

            UIBase.GetTrafficManagerTool(false)?.InitializeSubTools();
        }

        private static void onNodesOverlayChanged(bool newNodesOverlay) {
            if (!Options.IsGameLoaded()) {
                return;
            }

            Log._Debug($"Nodes overlay changed to {newNodesOverlay}");
            Options.nodesOverlay = newNodesOverlay;
        }

        private static void onShowLanesChanged(bool newShowLanes) {
            if (!Options.IsGameLoaded()) {
                return;
            }

            Log._Debug($"Show lanes changed to {newShowLanes}");
            Options.showLanes = newShowLanes;
        }

        private static void onVehicleOverlayChanged(bool newVal) {
            if (!Options.IsGameLoaded()) {
                return;
            }

            Log._Debug($"Vehicle overlay changed to {newVal}");
            Options.vehicleOverlay = newVal;
        }

        private static void onCitizenOverlayChanged(bool newVal) {
            if (!Options.IsGameLoaded()) {
                return;
            }

            Log._Debug($"Citizen overlay changed to {newVal}");
            Options.citizenOverlay = newVal;
        }

        private static void onBuildingOverlayChanged(bool newVal) {
            if (!Options.IsGameLoaded()) {
                return;
            }

            Log._Debug($"Building overlay changed to {newVal}");
            Options.buildingOverlay = newVal;
        }

        [UsedImplicitly]
        public static void SetShowLanes(bool newShowLanes) {
            Options.showLanes = newShowLanes;

            if (_showLanesToggle != null) {
                _showLanesToggle.isChecked = newShowLanes;
            }
        }

    } // end class
}