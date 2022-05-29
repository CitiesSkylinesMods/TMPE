namespace TrafficManager.API.Traffic.Enums {
    using System;

    [Flags]
    public enum LaneEndTransitionGroup {
        None = 0,
        Road = 1,
        Track = 2,
        Vehicle = Road | Track,
        Bicycle = 4,
        Pedestrian = 8,
    }
}
