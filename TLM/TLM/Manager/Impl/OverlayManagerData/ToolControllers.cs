namespace TrafficManager.Manager.Impl.OverlayManagerData {
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using TrafficManager.API.Traffic.Enums;

    public class ToolControllers {

        static ToolControllers() {
            Lookup = new(Settings.Count);

            foreach (var setting in Settings)
                Lookup.Add(setting.Tool, setting);
        }

        internal static Dictionary<Type, OverlayConfig> Lookup;

        /// <summary>
        /// Seettings for automatic <see cref="ToolsModifierControl.toolController.CurrentTool"/> overlays.
        /// </summary>
        private static readonly ReadOnlyCollection<OverlayConfig> Settings = new(new List<OverlayConfig>() {

            new OverlayConfig {
                Context =
                    OverlayContext.Tool,
                Tool =
                    typeof(NetTool),
                Culling =
                    OverlayCulling.Mouse,
                Interactive =
                    Overlays.None,
                Persistent =
                    Overlays.GroupAwareness,
                Filter =
                    RestrictedVehicles.All,
            },

            new OverlayConfig {
                Context =
                    OverlayContext.Tool,
                Tool =
                    typeof(BulldozeTool),
                Culling =
                    OverlayCulling.Mouse,
                Interactive =
                    Overlays.None,
                Persistent =
                    Overlays.GroupAwareness,
                Filter =
                    RestrictedVehicles.All,
            },

            new OverlayConfig {
                Context =
                    OverlayContext.Tool,
                Tool =
                    typeof(BuildingTool),
                Culling =
                    OverlayCulling.Mouse,
                Interactive =
                    Overlays.None,
                Persistent =
                    Overlays.VehicleRestrictions |
                    Overlays.ParkingRestrictions,
                Filter =
                    RestrictedVehicles.All,
            },

            new OverlayConfig {
                Context =
                    OverlayContext.Tool,
                Tool =
                    typeof(TransportTool),
                Culling =
                    OverlayCulling.Mouse,
                Interactive =
                    Overlays.None,
                Persistent =
                    Overlays.GroupTransport,
                Filter =
                    RestrictedVehicles.Transport |
                    RestrictedVehicles.Taxis |
                    RestrictedVehicles.Cars,
            },
        });
    }
}
