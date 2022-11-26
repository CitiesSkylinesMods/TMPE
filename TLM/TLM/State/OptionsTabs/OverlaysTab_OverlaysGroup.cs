namespace TrafficManager.State {
    using ICities;
    using System;
    using TrafficManager.Manager.Impl;
    using TrafficManager.UI;
    using TrafficManager.UI.Helpers;
    using TrafficManager.Util;

    public static class OverlaysTab_OverlaysGroup {

        public static CheckboxOption PrioritySignsOverlay =
            new (nameof(Options.prioritySignsOverlay), Options.Scope.Savegame) {
                Label = "Checkbox:Priority signs",
                Handler = OnOverlayChanged,
            };
        public static CheckboxOption TimedLightsOverlay =
            new (nameof(Options.timedLightsOverlay), Options.Scope.Savegame) {
                Label = "Checkbox:Timed traffic lights",
                Handler = OnOverlayChanged,
            };
        public static CheckboxOption SpeedLimitsOverlay =
            new (nameof(Options.speedLimitsOverlay), Options.Scope.Savegame) {
                Label = "Checkbox:Speed limits",
                Handler = OnOverlayChanged,
            };
        public static CheckboxOption ShowDefaultSpeedSubIcon =
            new (nameof(Options.showDefaultSpeedSubIcon), Options.Scope.Savegame) {
                Label = "Overlays.Checkbox:Show default speed with customised speeds",
                Indent = true,
            };
        public static CheckboxOption VehicleRestrictionsOverlay =
            new (nameof(Options.vehicleRestrictionsOverlay), Options.Scope.Savegame) {
                Label = "Checkbox:Vehicle restrictions",
                Handler = OnOverlayChanged,
            };
        public static CheckboxOption ParkingRestrictionsOverlay =
            new (nameof(Options.parkingRestrictionsOverlay), Options.Scope.Savegame) {
                Label = "Checkbox:Parking restrictions",
                Handler = OnOverlayChanged,
            };
        public static CheckboxOption JunctionRestrictionsOverlay =
            new (nameof(Options.junctionRestrictionsOverlay), Options.Scope.Savegame) {
                Label = "Checkbox:Junction restrictions",
                Handler = OnOverlayChanged,
            };
        public static CheckboxOption ConnectedLanesOverlay =
            new (nameof(Options.connectedLanesOverlay), Options.Scope.Savegame) {
                Label = "Overlay.Checkbox:Connected lanes",
                Handler = OnOverlayChanged,
            };
        public static CheckboxOption NodesOverlay =
            new (nameof(Options.nodesOverlay), Options.Scope.Savegame) {
                Label = "Overlay.Checkbox:Nodes and segments",
            };
        public static CheckboxOption ShowLanes =
            new (nameof(Options.showLanes), Options.Scope.Savegame) {
                Label = "Overlay.Checkbox:Lanes",
            };
        public static CheckboxOption VehicleOverlay =
            new (nameof(Options.vehicleOverlay), Options.Scope.Savegame) {
                Label = "Overlay.Checkbox:Vehicles",
                Validator = DebugOnlyValidator,
            };
        public static CheckboxOption CitizenOverlay =
            new (nameof(Options.citizenOverlay), Options.Scope.Savegame) {
                Label = "Overlay.Checkbox:Citizens",
                Validator = DebugOnlyValidator,
            };
        public static CheckboxOption BuildingOverlay =
            new (nameof(Options.buildingOverlay), Options.Scope.Savegame) {
                Label = "Overlay.Checkbox:Buildings",
                Validator = DebugOnlyValidator,
            };
        public static CheckboxOption ShowPathFindStats =
            new(nameof(Options.showPathFindStats), Options.Scope.Savegame) {
                DefaultValue = VersionUtil.IS_DEBUG,
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