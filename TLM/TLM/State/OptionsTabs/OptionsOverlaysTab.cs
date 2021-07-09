namespace TrafficManager.State {
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using JetBrains.Annotations;
    using TrafficManager.UI;
    using TrafficManager.UI.Helpers;

    public static class OptionsOverlaysTab {
        private static UICheckBox prioritySignsOverlayToggle_;
        private static UICheckBox timedLightsOverlayToggle_;
        private static UICheckBox speedLimitsOverlayToggle_;
        private static UICheckBox vehicleRestrictionsOverlayToggle_;
        private static UICheckBox parkingRestrictionsOverlayToggle_;
        private static UICheckBox junctionRestrictionsOverlayToggle_;
        private static UICheckBox connectedLanesOverlayToggle_;
        private static UICheckBox nodesOverlayToggle_;
        private static UICheckBox vehicleOverlayToggle_;
        private static UICheckBox showLanesToggle_;
#if DEBUG
        [UsedImplicitly]
        private static UICheckBox citizenOverlayToggle_;

        [UsedImplicitly]
        private static UICheckBox buildingOverlayToggle_;
#endif

        internal static void MakeSettings_Overlays(ExtUITabstrip tabStrip) {
            UIHelper panelHelper = tabStrip.AddTabPage(Translation.Options.Get("Tab:Overlays"));

            prioritySignsOverlayToggle_ = panelHelper.AddCheckbox(
                                              Translation.Options.Get("Checkbox:Priority signs"),
                                              Options.prioritySignsOverlay,
                                              OnPrioritySignsOverlayChanged) as UICheckBox;
            timedLightsOverlayToggle_ = panelHelper.AddCheckbox(
                                            Translation.Options.Get(
                                                "Checkbox:Timed traffic lights"),
                                            Options.timedLightsOverlay,
                                            OnTimedLightsOverlayChanged) as UICheckBox;
            speedLimitsOverlayToggle_ = panelHelper.AddCheckbox(
                                            Translation.Options.Get("Checkbox:Speed limits"),
                                            Options.speedLimitsOverlay,
                                            OnSpeedLimitsOverlayChanged) as UICheckBox;
            vehicleRestrictionsOverlayToggle_
                = panelHelper.AddCheckbox(
                      Translation.Options.Get("Checkbox:Vehicle restrictions"),
                      Options.vehicleRestrictionsOverlay,
                      OnVehicleRestrictionsOverlayChanged) as UICheckBox;
            parkingRestrictionsOverlayToggle_
                = panelHelper.AddCheckbox(
                      Translation.Options.Get("Checkbox:Parking restrictions"),
                      Options.parkingRestrictionsOverlay,
                      OnParkingRestrictionsOverlayChanged) as UICheckBox;
            junctionRestrictionsOverlayToggle_
                = panelHelper.AddCheckbox(
                      Translation.Options.Get("Checkbox:Junction restrictions"),
                      Options.junctionRestrictionsOverlay,
                      OnJunctionRestrictionsOverlayChanged) as UICheckBox;
            connectedLanesOverlayToggle_
                = panelHelper.AddCheckbox(
                      Translation.Options.Get("Overlay.Checkbox:Connected lanes"),
                      Options.connectedLanesOverlay,
                      OnConnectedLanesOverlayChanged) as UICheckBox;
            nodesOverlayToggle_ = panelHelper.AddCheckbox(
                                      Translation.Options.Get(
                                          "Overlay.Checkbox:Nodes and segments"),
                                      Options.nodesOverlay,
                                      OnNodesOverlayChanged) as UICheckBox;
            showLanesToggle_ = panelHelper.AddCheckbox(
                                   Translation.Options.Get("Overlay.Checkbox:Lanes"),
                                   Options.showLanes,
                                   OnShowLanesChanged) as UICheckBox;
#if DEBUG
            vehicleOverlayToggle_ = panelHelper.AddCheckbox(
                                        Translation.Options.Get("Overlay.Checkbox:Vehicles"),
                                        Options.vehicleOverlay,
                                        OnVehicleOverlayChanged) as UICheckBox;
            citizenOverlayToggle_ = panelHelper.AddCheckbox(
                                        Translation.Options.Get("Overlay.Checkbox:Citizens"),
                                        Options.citizenOverlay,
                                        OnCitizenOverlayChanged) as UICheckBox;
            buildingOverlayToggle_ = panelHelper.AddCheckbox(
                                         Translation.Options.Get("Overlay.Checkbox:Buildings"),
                                         Options.buildingOverlay,
                                         OnBuildingOverlayChanged) as UICheckBox;
#endif
        }

        public static void SetPrioritySignsOverlay(bool newPrioritySignsOverlay) {
            Options.prioritySignsOverlay = newPrioritySignsOverlay;

            if (prioritySignsOverlayToggle_ != null) {
                prioritySignsOverlayToggle_.isChecked = newPrioritySignsOverlay;
            }

            ModUI.GetTrafficManagerTool(false)?.InitializeSubTools();
        }

        public static void SetTimedLightsOverlay(bool newTimedLightsOverlay) {
            Options.timedLightsOverlay = newTimedLightsOverlay;

            if (timedLightsOverlayToggle_ != null) {
                timedLightsOverlayToggle_.isChecked = newTimedLightsOverlay;
            }

            ModUI.GetTrafficManagerTool(false)?.InitializeSubTools();
        }

        public static void SetSpeedLimitsOverlay(bool newSpeedLimitsOverlay) {
            Options.speedLimitsOverlay = newSpeedLimitsOverlay;

            if (speedLimitsOverlayToggle_ != null) {
                speedLimitsOverlayToggle_.isChecked = newSpeedLimitsOverlay;
            }

            ModUI.GetTrafficManagerTool(false)?.InitializeSubTools();
        }

        public static void SetVehicleRestrictionsOverlay(bool newVehicleRestrictionsOverlay) {
            Options.vehicleRestrictionsOverlay = newVehicleRestrictionsOverlay;

            if (vehicleRestrictionsOverlayToggle_ != null) {
                vehicleRestrictionsOverlayToggle_.isChecked = newVehicleRestrictionsOverlay;
            }

            ModUI.GetTrafficManagerTool(false)?.InitializeSubTools();
        }

        public static void SetParkingRestrictionsOverlay(bool newParkingRestrictionsOverlay) {
            Options.parkingRestrictionsOverlay = newParkingRestrictionsOverlay;

            if (parkingRestrictionsOverlayToggle_ != null) {
                parkingRestrictionsOverlayToggle_.isChecked = newParkingRestrictionsOverlay;
            }

            ModUI.GetTrafficManagerTool(false)?.InitializeSubTools();
        }

        public static void SetJunctionRestrictionsOverlay(bool newValue) {
            Options.junctionRestrictionsOverlay = newValue;

            if (junctionRestrictionsOverlayToggle_ != null) {
                junctionRestrictionsOverlayToggle_.isChecked = newValue;
            }

            ModUI.GetTrafficManagerTool(false)?.InitializeSubTools();
        }

        public static void SetConnectedLanesOverlay(bool newValue) {
            Options.connectedLanesOverlay = newValue;

            if (connectedLanesOverlayToggle_ != null) {
                connectedLanesOverlayToggle_.isChecked = newValue;
            }
        }

        public static void SetNodesOverlay(bool newNodesOverlay) {
            Options.nodesOverlay = newNodesOverlay;

            if (nodesOverlayToggle_ != null) {
                nodesOverlayToggle_.isChecked = newNodesOverlay;
            }

            ModUI.GetTrafficManagerTool(false)?.InitializeSubTools();
        }

        public static void SetVehicleOverlay(bool newVal) {
            Options.vehicleOverlay = newVal;

            if (vehicleOverlayToggle_ != null) {
                vehicleOverlayToggle_.isChecked = newVal;
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

        private static void
            OnVehicleRestrictionsOverlayChanged(bool newVehicleRestrictionsOverlay) {
            if (!Options.IsGameLoaded()) {
                return;
            }

            Log._Debug($"vehicleRestrictionsOverlay changed to {newVehicleRestrictionsOverlay}");
            Options.vehicleRestrictionsOverlay = newVehicleRestrictionsOverlay;

            ModUI.GetTrafficManagerTool(false)?.InitializeSubTools();
        }

        private static void
            OnParkingRestrictionsOverlayChanged(bool newParkingRestrictionsOverlay) {
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

        private static void OnNodesOverlayChanged(bool newNodesOverlay) {
            if (!Options.IsGameLoaded()) {
                return;
            }

            Log._Debug($"Nodes overlay changed to {newNodesOverlay}");
            Options.nodesOverlay = newNodesOverlay;
        }

        private static void OnShowLanesChanged(bool newShowLanes) {
            if (!Options.IsGameLoaded()) {
                return;
            }

            Log._Debug($"Show lanes changed to {newShowLanes}");
            Options.showLanes = newShowLanes;
        }

        private static void OnVehicleOverlayChanged(bool newVal) {
            if (!Options.IsGameLoaded()) {
                return;
            }

            Log._Debug($"Vehicle overlay changed to {newVal}");
            Options.vehicleOverlay = newVal;
        }

        private static void OnCitizenOverlayChanged(bool newVal) {
            if (!Options.IsGameLoaded()) {
                return;
            }

            Log._Debug($"Citizen overlay changed to {newVal}");
            Options.citizenOverlay = newVal;
        }

        private static void OnBuildingOverlayChanged(bool newVal) {
            if (!Options.IsGameLoaded()) {
                return;
            }

            Log._Debug($"Building overlay changed to {newVal}");
            Options.buildingOverlay = newVal;
        }

        [UsedImplicitly]
        public static void SetShowLanes(bool newShowLanes) {
            Options.showLanes = newShowLanes;

            if (showLanesToggle_ != null) {
                showLanesToggle_.isChecked = newShowLanes;
            }
        }
    } // end class
}