using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TrafficManager.Manager.Model {
    internal enum JunctionRestrictionFlags {
        AllowUTurn = 1 << 0,
        AllowNearTurnOnRed = 1 << 1,
        AllowFarTurnOnRed = 1 << 2,
        AllowForwardLaneChange = 1 << 3,
        AllowEnterWhenBlocked = 1 << 4,
        AllowPedestrianCrossing = 1 << 5,
    }

}
