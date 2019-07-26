namespace TrafficManager.State {
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using UI;

    public static class OptionsOverlaysTab {
        internal static UICheckBox prioritySignsOverlayToggle;
        private static UICheckBox timedLightsOverlayToggle;
        private static UICheckBox speedLimitsOverlayToggle;
        private static UICheckBox vehicleRestrictionsOverlayToggle;
        private static UICheckBox parkingRestrictionsOverlayToggle;
        private static UICheckBox junctionRestrictionsOverlayToggle;
        private static UICheckBox connectedLanesOverlayToggle;
        private static UICheckBox nodesOverlayToggle;
        private static UICheckBox vehicleOverlayToggle;
        private static UICheckBox showLanesToggle;
#if DEBUG
        private static UICheckBox citizenOverlayToggle;
        private static UICheckBox buildingOverlayToggle;
#endif

        private static string T(string s) {
            return Translation.GetString(s);
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

            prioritySignsOverlayToggle = panelHelper.AddCheckbox(
                                             T("Priority_signs"),
                                             Options.prioritySignsOverlay,
                                             OnPrioritySignsOverlayChanged) as UICheckBox;
            timedLightsOverlayToggle = panelHelper.AddCheckbox(
                                           T("Timed_traffic_lights"),
                                           Options.timedLightsOverlay,
                                           OnTimedLightsOverlayChanged) as UICheckBox;
            speedLimitsOverlayToggle = panelHelper.AddCheckbox(
                                           T("Speed_limits"),
                                           Options.speedLimitsOverlay,
                                           OnSpeedLimitsOverlayChanged) as UICheckBox;
            vehicleRestrictionsOverlayToggle
                = panelHelper.AddCheckbox(
                      T("Vehicle_restrictions"),
                      Options.vehicleRestrictionsOverlay,
                      OnVehicleRestrictionsOverlayChanged) as UICheckBox;
            parkingRestrictionsOverlayToggle
                = panelHelper.AddCheckbox(
                      T("Parking_restrictions"),
                      Options.parkingRestrictionsOverlay,
                      OnParkingRestrictionsOverlayChanged) as UICheckBox;
            junctionRestrictionsOverlayToggle
                = panelHelper.AddCheckbox(
                      T("Junction_restrictions"),
                      Options.junctionRestrictionsOverlay,
                      OnJunctionRestrictionsOverlayChanged) as UICheckBox;
            connectedLanesOverlayToggle = panelHelper.AddCheckbox(
                                              T("Connected_lanes"),
                                              Options.connectedLanesOverlay,
                                              OnConnectedLanesOverlayChanged) as UICheckBox;
            nodesOverlayToggle = panelHelper.AddCheckbox(
                                     T("Nodes_and_segments"),
                                     Options.nodesOverlay,
                                     onNodesOverlayChanged) as UICheckBox;
            showLanesToggle = panelHelper.AddCheckbox(
                                  T("Lanes"),
                                  Options.showLanes,
                                  onShowLanesChanged) as UICheckBox;
#if DEBUG
            vehicleOverlayToggle = panelHelper.AddCheckbox(
                                       T("Vehicles"),
                                       Options.vehicleOverlay,
                                       onVehicleOverlayChanged) as UICheckBox;
            citizenOverlayToggle = panelHelper.AddCheckbox(
                                       T("Citizens"),
                                       Options.citizenOverlay,
                                       onCitizenOverlayChanged) as UICheckBox;
            buildingOverlayToggle = panelHelper.AddCheckbox(
                                        T("Buildings"),
                                        Options.buildingOverlay,
                                        onBuildingOverlayChanged) as UICheckBox;
#endif
        }

        public static void SetPrioritySignsOverlay(bool newPrioritySignsOverlay) {
            Options.prioritySignsOverlay = newPrioritySignsOverlay;

            if (prioritySignsOverlayToggle != null) {
                prioritySignsOverlayToggle.isChecked = newPrioritySignsOverlay;
            }

            UIBase.GetTrafficManagerTool(false)?.InitializeSubTools();
        }

                public static void SetTimedLightsOverlay(bool newTimedLightsOverlay) {
            Options.timedLightsOverlay = newTimedLightsOverlay;

            if (timedLightsOverlayToggle != null) {
                timedLightsOverlayToggle.isChecked = newTimedLightsOverlay;
            }

            UIBase.GetTrafficManagerTool(false)?.InitializeSubTools();
        }

        public static void SetSpeedLimitsOverlay(bool newSpeedLimitsOverlay) {
            Options.speedLimitsOverlay = newSpeedLimitsOverlay;

            if (speedLimitsOverlayToggle != null) {
                speedLimitsOverlayToggle.isChecked = newSpeedLimitsOverlay;
            }

            UIBase.GetTrafficManagerTool(false)?.InitializeSubTools();
        }

        public static void SetVehicleRestrictionsOverlay(bool newVehicleRestrictionsOverlay) {
            Options.vehicleRestrictionsOverlay = newVehicleRestrictionsOverlay;

            if (vehicleRestrictionsOverlayToggle != null) {
                vehicleRestrictionsOverlayToggle.isChecked = newVehicleRestrictionsOverlay;
            }

            UIBase.GetTrafficManagerTool(false)?.InitializeSubTools();
        }

        public static void SetParkingRestrictionsOverlay(bool newParkingRestrictionsOverlay) {
            Options.parkingRestrictionsOverlay = newParkingRestrictionsOverlay;

            if (parkingRestrictionsOverlayToggle != null) {
                parkingRestrictionsOverlayToggle.isChecked = newParkingRestrictionsOverlay;
            }

            UIBase.GetTrafficManagerTool(false)?.InitializeSubTools();
        }

        public static void SetJunctionRestrictionsOverlay(bool newValue) {
            Options.junctionRestrictionsOverlay = newValue;

            if (junctionRestrictionsOverlayToggle != null) {
                junctionRestrictionsOverlayToggle.isChecked = newValue;
            }

            UIBase.GetTrafficManagerTool(false)?.InitializeSubTools();
        }

        public static void SetConnectedLanesOverlay(bool newValue) {
            Options.connectedLanesOverlay = newValue;

            if (connectedLanesOverlayToggle != null) {
                connectedLanesOverlayToggle.isChecked = newValue;
            }
        }

        public static void SetNodesOverlay(bool newNodesOverlay) {
            Options.nodesOverlay = newNodesOverlay;

            if (nodesOverlayToggle != null) {
                nodesOverlayToggle.isChecked = newNodesOverlay;
            }

            UIBase.GetTrafficManagerTool(false)?.InitializeSubTools();
        }

        public static void SetVehicleOverlay(bool newVal) {
            Options.vehicleOverlay = newVal;

            if (vehicleOverlayToggle != null) {
                vehicleOverlayToggle.isChecked = newVal;
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

        public static void SetShowLanes(bool newShowLanes) {
            Options.showLanes = newShowLanes;

            if (showLanesToggle != null) {
                showLanesToggle.isChecked = newShowLanes;
            }
        }

    } // end class
}