namespace TrafficManager.State {
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using JetBrains.Annotations;
    using TrafficManager.UI.Helpers;
    using TrafficManager.UI;

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

        internal static void MakeSettings_Overlays(ExtUITabstrip tabStrip) {
            UIHelper panelHelper = tabStrip.AddTabPage(Translation.Options.Get("Tab:Overlays"));

            _prioritySignsOverlayToggle = panelHelper.AddCheckbox(
                                              Translation.Options.Get("Checkbox:Priority signs"),
                                              Options.prioritySignsOverlay,
                                              OnPrioritySignsOverlayChanged) as UICheckBox;
            _timedLightsOverlayToggle = panelHelper.AddCheckbox(
                                           Translation.Options.Get("Checkbox:Timed traffic lights"),
                                           Options.timedLightsOverlay,
                                           OnTimedLightsOverlayChanged) as UICheckBox;
            _speedLimitsOverlayToggle = panelHelper.AddCheckbox(
                                            Translation.Options.Get("Checkbox:Speed limits"),
                                            Options.speedLimitsOverlay,
                                            OnSpeedLimitsOverlayChanged) as UICheckBox;
            _vehicleRestrictionsOverlayToggle
                = panelHelper.AddCheckbox(
                      Translation.Options.Get("Checkbox:Vehicle restrictions"),
                      Options.vehicleRestrictionsOverlay,
                      OnVehicleRestrictionsOverlayChanged) as UICheckBox;
            _parkingRestrictionsOverlayToggle
                = panelHelper.AddCheckbox(
                      Translation.Options.Get("Checkbox:Parking restrictions"),
                      Options.parkingRestrictionsOverlay,
                      OnParkingRestrictionsOverlayChanged) as UICheckBox;
            _junctionRestrictionsOverlayToggle
                = panelHelper.AddCheckbox(
                      Translation.Options.Get("Checkbox:Junction restrictions"),
                      Options.junctionRestrictionsOverlay,
                      OnJunctionRestrictionsOverlayChanged) as UICheckBox;
            _connectedLanesOverlayToggle
                = panelHelper.AddCheckbox(
                      Translation.Options.Get("Overlay.Checkbox:Connected lanes"),
                      Options.connectedLanesOverlay,
                      OnConnectedLanesOverlayChanged) as UICheckBox;
            _nodesOverlayToggle = panelHelper.AddCheckbox(
                                     Translation.Options.Get("Overlay.Checkbox:Nodes and segments"),
                                     Options.nodesOverlay,
                                     onNodesOverlayChanged) as UICheckBox;
            _showLanesToggle = panelHelper.AddCheckbox(
                                  Translation.Options.Get("Overlay.Checkbox:Lanes"),
                                  Options.showLanes,
                                  onShowLanesChanged) as UICheckBox;
#if DEBUG
            _vehicleOverlayToggle = panelHelper.AddCheckbox(
                                       Translation.Options.Get("Overlay.Checkbox:Vehicles"),
                                       Options.vehicleOverlay,
                                       onVehicleOverlayChanged) as UICheckBox;
            _citizenOverlayToggle = panelHelper.AddCheckbox(
                                        Translation.Options.Get("Overlay.Checkbox:Citizens"),
                                        Options.citizenOverlay,
                                        onCitizenOverlayChanged) as UICheckBox;
            _buildingOverlayToggle = panelHelper.AddCheckbox(
                                         Translation.Options.Get("Overlay.Checkbox:Buildings"),
                                         Options.buildingOverlay,
                                         onBuildingOverlayChanged) as UICheckBox;
#endif
        }

        public static void SetPrioritySignsOverlay(bool newPrioritySignsOverlay) {
            Options.prioritySignsOverlay = newPrioritySignsOverlay;

            if (_prioritySignsOverlayToggle != null) {
                _prioritySignsOverlayToggle.isChecked = newPrioritySignsOverlay;
            }

            ModUI.GetTrafficManagerTool(false)?.InitializeSubTools();
        }

        public static void SetTimedLightsOverlay(bool newTimedLightsOverlay) {
            Options.timedLightsOverlay = newTimedLightsOverlay;

            if (_timedLightsOverlayToggle != null) {
                _timedLightsOverlayToggle.isChecked = newTimedLightsOverlay;
            }

            ModUI.GetTrafficManagerTool(false)?.InitializeSubTools();
        }

        public static void SetSpeedLimitsOverlay(bool newSpeedLimitsOverlay) {
            Options.speedLimitsOverlay = newSpeedLimitsOverlay;

            if (_speedLimitsOverlayToggle != null) {
                _speedLimitsOverlayToggle.isChecked = newSpeedLimitsOverlay;
            }

            ModUI.GetTrafficManagerTool(false)?.InitializeSubTools();
        }

        public static void SetVehicleRestrictionsOverlay(bool newVehicleRestrictionsOverlay) {
            Options.vehicleRestrictionsOverlay = newVehicleRestrictionsOverlay;

            if (_vehicleRestrictionsOverlayToggle != null) {
                _vehicleRestrictionsOverlayToggle.isChecked = newVehicleRestrictionsOverlay;
            }

            ModUI.GetTrafficManagerTool(false)?.InitializeSubTools();
        }

        public static void SetParkingRestrictionsOverlay(bool newParkingRestrictionsOverlay) {
            Options.parkingRestrictionsOverlay = newParkingRestrictionsOverlay;

            if (_parkingRestrictionsOverlayToggle != null) {
                _parkingRestrictionsOverlayToggle.isChecked = newParkingRestrictionsOverlay;
            }

            ModUI.GetTrafficManagerTool(false)?.InitializeSubTools();
        }

        public static void SetJunctionRestrictionsOverlay(bool newValue) {
            Options.junctionRestrictionsOverlay = newValue;

            if (_junctionRestrictionsOverlayToggle != null) {
                _junctionRestrictionsOverlayToggle.isChecked = newValue;
            }

            ModUI.GetTrafficManagerTool(false)?.InitializeSubTools();
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

            ModUI.GetTrafficManagerTool(false)?.InitializeSubTools();
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

            ModUI.GetTrafficManagerTool(false)?.InitializeSubTools();
        }

        private static void OnTimedLightsOverlayChanged(bool newTimedLightsOverlay) {
            if (!Options.IsGameLoaded()) {
                return;
            }

            Log._Debug($"timedLightsOverlay changed to {newTimedLightsOverlay}");
            Options.timedLightsOverlay = newTimedLightsOverlay;

            ModUI.GetTrafficManagerTool(false)?.InitializeSubTools();
        }

        private static void OnSpeedLimitsOverlayChanged(bool newSpeedLimitsOverlay) {
            if (!Options.IsGameLoaded()) {
                return;
            }

            Log._Debug($"speedLimitsOverlay changed to {newSpeedLimitsOverlay}");
            Options.speedLimitsOverlay = newSpeedLimitsOverlay;

            ModUI.GetTrafficManagerTool(false)?.InitializeSubTools();
        }

        private static void OnVehicleRestrictionsOverlayChanged(bool newVehicleRestrictionsOverlay) {
            if (!Options.IsGameLoaded()) {
                return;
            }

            Log._Debug($"vehicleRestrictionsOverlay changed to {newVehicleRestrictionsOverlay}");
            Options.vehicleRestrictionsOverlay = newVehicleRestrictionsOverlay;

            ModUI.GetTrafficManagerTool(false)?.InitializeSubTools();
        }

        private static void OnParkingRestrictionsOverlayChanged(bool newParkingRestrictionsOverlay) {
            if (!Options.IsGameLoaded()) {
                return;
            }

            Log._Debug($"parkingRestrictionsOverlay changed to {newParkingRestrictionsOverlay}");
            Options.parkingRestrictionsOverlay = newParkingRestrictionsOverlay;

            ModUI.GetTrafficManagerTool(false)?.InitializeSubTools();
        }

        private static void OnJunctionRestrictionsOverlayChanged(bool newValue) {
            if (!Options.IsGameLoaded()) {
                return;
            }

            Log._Debug($"junctionRestrictionsOverlay changed to {newValue}");
            Options.junctionRestrictionsOverlay = newValue;

            ModUI.GetTrafficManagerTool(false)?.InitializeSubTools();
        }

        private static void OnConnectedLanesOverlayChanged(bool newValue) {
            if (!Options.IsGameLoaded()) {
                return;
            }

            Log._Debug($"connectedLanesOverlay changed to {newValue}");
            Options.connectedLanesOverlay = newValue;

            ModUI.GetTrafficManagerTool(false)?.InitializeSubTools();
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
