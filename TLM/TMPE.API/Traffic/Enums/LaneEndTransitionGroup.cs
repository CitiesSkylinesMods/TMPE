namespace TrafficManager.API.Traffic.Enums {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    [Flags]
    public enum LaneEndTransitionGroup {
        None = 0,
        Car = 1,
        Track = 2,
        All = Car | Track,
    }
}
