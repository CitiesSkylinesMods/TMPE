using System;

namespace TrafficManager.API.Traffic.Enums {

    [Flags]
    public enum JunctionRestrictionFlags {
        AllowUTurn = 1 << 0,
        AllowNearTurnOnRed = 1 << 1,
        AllowFarTurnOnRed = 1 << 2,
        AllowForwardLaneChange = 1 << 3,
        AllowEnterWhenBlocked = 1 << 4,
        AllowPedestrianCrossing = 1 << 5,

        All = (1 << 6) - 1,
    }
}
