namespace TrafficManager.API.Traffic.Enums {
    using System;

    // TODO why do we need this?
    [Flags]
    public enum ExtVehicleFlags {
        None = 0,
        Created = 1,
        Spawned = 1 << 1,

        // aircraft related flags
        GoingToOutside = 1 << 2,
        GoingFromOutside = 1 << 3,
    }
}