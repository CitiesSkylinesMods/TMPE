namespace TrafficManager.State {
    using ICities;
    using System;
    using TrafficManager.Manager.Impl;
    using TrafficManager.UI;
    using TrafficManager.UI.Helpers;
    using TrafficManager.Util;

    public static class OverlaysTab_OverlaysGroup {

        public static CheckboxOption PrioritySignsOverlay =
            new (nameof(SavedGameOptions.prioritySignsOverlay), Scope.Savegame) {
                Label = "Checkbox:Priority signs",
                Handler = OnOverlayChanged,
            };
        public static CheckboxOption TimedLightsOverlay =
            new (nameof(SavedGameOptions.timedLightsOverlay), Scope.Savegame) {
                Label = "Checkbox:Timed traffic lights",
                Handler = OnOverlayChanged,
            };
        public static CheckboxOption SpeedLimitsOverlay =
            new (nameof(SavedGameOptions.speedLimitsOverlay), Scope.Savegame) {
                Label = "Checkbox:Speed limits",
                Handler = OnOverlayChanged,
            };
        public static CheckboxOption ShowDefaultSpeedSubIcon =
            new (nameof(SavedGameOptions.showDefaultSpeedSubIcon), Scope.Savegame) {
                Label = "Overlays.Checkbox:Show default speed with customised speeds",
                Indent = true,
            };
        public static CheckboxOption VehicleRestrictionsOverlay =
            new (nameof(SavedGameOptions.vehicleRestrictionsOverlay), Scope.Savegame) {
                Label = "Checkbox:Vehicle restrictions",
                Handler = OnOverlayChanged,
            };
        public static CheckboxOption ParkingRestrictionsOverlay =
            new (nameof(SavedGameOptions.parkingRestrictionsOverlay), Scope.Savegame) {
                Label = "Checkbox:Parking restrictions",
                Handler = OnOverlayChanged,
            };
        public static CheckboxOption JunctionRestrictionsOverlay =
            new (nameof(SavedGameOptions.junctionRestrictionsOverlay), Scope.Savegame) {
                Label = "Checkbox:Junction restrictions",
                Handler = OnOverlayChanged,
            };
        public static CheckboxOption ConnectedLanesOverlay =
            new (nameof(SavedGameOptions.connectedLanesOverlay), Scope.Savegame) {
                Label = "Overlay.Checkbox:Connected lanes",
                Handler = OnOverlayChanged,
            };
        public static CheckboxOption NodesOverlay =
            new (nameof(SavedGameOptions.nodesOverlay), Scope.Savegame) {
                Label = "Overlay.Checkbox:Nodes and segments",
            };
        public static CheckboxOption ShowLanes =
            new (nameof(SavedGameOptions.showLanes), Scope.Savegame) {
                Label = "Overlay.Checkbox:Lanes",
            };
        public static CheckboxOption VehicleOverlay =
            new (nameof(SavedGameOptions.vehicleOverlay), Scope.Savegame) {
                Label = "Overlay.Checkbox:Vehicles",
                Validator = DebugOnlyValidator,
            };
        public static CheckboxOption CitizenOverlay =
            new (nameof(SavedGameOptions.citizenOverlay), Scope.Savegame) {
                Label = "Overlay.Checkbox:Citizens",
                Validator = DebugOnlyValidator,
            };
        public static CheckboxOption BuildingOverlay =
            new (nameof(SavedGameOptions.buildingOverlay), Scope.Savegame) {
                Label = "Overlay.Checkbox:Buildings",
                Validator = DebugOnlyValidator,
            };
        public static CheckboxOption ShowPathFindStats =
            new(nameof(SavedGameOptions.showPathFindStats), Scope.Savegame) {
                Label = "Maintenance.Checkbox:Show path-find stats",
                Validator = QueuedStatsOnlyValidator,
                Handler = OnShowPathFindStatsChanged,
            };

        static OverlaysTab_OverlaysGroup() {
            try {
                PrioritySignsOverlay
                    .PropagateTrueTo(MaintenanceTab_FeaturesGroup.PrioritySignsEnabled);
                TimedLightsOverlay
                    .PropagateTrueTo(MaintenanceTab_FeaturesGroup.TimedLightsEnabled);
                SpeedLimitsOverlay
                    .PropagateTrueTo(MaintenanceTab_FeaturesGroup.CustomSpeedLimitsEnabled);
                VehicleRestrictionsOverlay
                    .PropagateTrueTo(MaintenanceTab_FeaturesGroup.VehicleRestrictionsEnabled);
                ParkingRestrictionsOverlay
                    .PropagateTrueTo(MaintenanceTab_FeaturesGroup.ParkingRestrictionsEnabled);
                JunctionRestrictionsOverlay
                    .PropagateTrueTo(MaintenanceTab_FeaturesGroup.JunctionRestrictionsEnabled);
                ConnectedLanesOverlay
                    .PropagateTrueTo(MaintenanceTab_FeaturesGroup.LaneConnectorEnabled);
            }
            catch (Exception ex) {
                ex.LogException();
            }
        }

        internal static void AddUI(UIHelperBase tab) {

            var group = tab.AddGroup(T("Tab:Overlays"));

            PrioritySignsOverlay.AddUI(group);
            TimedLightsOverlay.AddUI(group);
            SpeedLimitsOverlay.AddUI(group);
            ShowDefaultSpeedSubIcon.AddUI(group);
            VehicleRestrictionsOverlay.AddUI(group);
            ParkingRestrictionsOverlay.AddUI(group);
            JunctionRestrictionsOverlay.AddUI(group);
            ConnectedLanesOverlay.AddUI(group);

            NodesOverlay.AddUI(group);
            ShowLanes.AddUI(group);
#if DEBUG
            VehicleOverlay.AddUI(group);
            CitizenOverlay.AddUI(group);
            BuildingOverlay.AddUI(group);
#endif
#if QUEUEDSTATS
            ShowPathFindStats.AddUI(group);
#endif
        }

        private static string T(string key) => Translation.Options.Get(key);

        private static bool DebugOnlyValidator(bool desired, out bool result) {
            result = VersionUtil.IS_DEBUG && desired;
            return true;
        }

        private static bool QueuedStatsOnlyValidator(bool desired, out bool result) {
#if QUEUEDSTATS
            result = desired;
#else
            result = false;
#endif
            return true;
        }

        private static void OnOverlayChanged(bool _)
            => OptionsManager.ReinitialiseSubTools();

        private static void OnShowPathFindStatsChanged(bool _)
            => OptionsManager.RebuildMenu();
    }
}