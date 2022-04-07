namespace TrafficManager.API.Traffic.Enums {
    using System;

    [Flags]
    public enum Overlays : int {
        None = 0,

        /// <summary>
        /// Priority Signs at junctions.
        /// </summary>
        PrioritySigns = 1 << 0,

        /// <summary>
        /// Traffic Lights at junctions.
        /// </summary>
        TrafficLights = 1 << 1,

        /// <summary>
        /// Speed Limits on segments/lanes.
        /// </summary>
        SpeedLimits = 1 << 2,

        /// <summary>
        /// Vehicle restrictions on segment lanes.
        /// </summary>
        VehicleRestrictions = 1 << 3,

        /// <summary>
        /// Parking Restrictions on segment lanes.
        /// </summary>
        ParkingRestrictions = 1 << 4,

        /// <summary>
        /// Parking Space props in buildings.
        /// </summary>
        ParkingSpaces = 1 << 5, // future

        /// <summary>
        /// Junction Restrictions on segment ends.
        /// </summary>
        JunctionRestrictions = 1 << 6,

        /// <summary>
        /// Lane Connectors on nodes
        /// </summary>
        LaneConnectors = 1 << 7,

        /// <summary>
        /// Lane Arrows
        /// </summary>
        LaneArrows = 1 << 8,

        /// <summary>
        /// When used as persistent overlay, tunnels will be rendered
        /// in overground mode, allowing display and interaction of
        /// underground overlays.
        /// </summary>
        Tunnels = 1 << 9,

        // Developer/niche overlays
        Networks = 1 << 24,
        Lanes = 1 << 26,
        Vehicles = 1 << 27,
        PathUnits = 1 << 28, // future
        Citizens = 1 << 29,
        Buildings = 1 << 30,

        // TM:PE use only - special flag that denotes user choices in Overlays tab
        TMPE = 1 << 31,

        /// <summary>
        /// Useful for external mods, so user can see what
        /// their actions might affect.
        /// </summary>
        GroupAwareness =
            PrioritySigns | TrafficLights | SpeedLimits |
            VehicleRestrictions | ParkingRestrictions |
            JunctionRestrictions | LaneConnectors,

        /// <summary>
        /// Useful for bulk edit tools / mass applicators.
        /// </summary>
        GroupBulk =
            PrioritySigns | JunctionRestrictions |
            SpeedLimits | LaneConnectors,

        /// <summary>
        /// Overlays that affect services,
        /// including emergency responders.
        /// </summary>
        GroupService =
            VehicleRestrictions |
            LaneConnectors,

        /// <summary>
        /// Overlays that affect public transport usage.
        /// </summary>
        GroupTransport =
            VehicleRestrictions |
            ParkingRestrictions |
            ParkingSpaces |
            LaneConnectors,

        /// <summary>
        /// Overlays for outside connections,
        /// road maintenance, etc.
        /// </summary>
        GroupNetwork =
            VehicleRestrictions |
            LaneConnectors |
            SpeedLimits,

        /// <summary>
        /// Overlays which affect cargo transport.
        /// </summary>
        GroupCargo =
            GroupService,
    }
}
