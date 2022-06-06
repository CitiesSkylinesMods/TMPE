namespace TrafficManager.API.Traffic.Enums {
    using System;

    [Flags]
    public enum ExtVehicleSize {
        None,
        // Tiny = 1, // integration with external mod
        Small = 1 << 1,
        Medium = 1 << 2,
        Large = 1 << 3,
        All = /*Tiny |*/ Small | Medium | Large,
    }
}