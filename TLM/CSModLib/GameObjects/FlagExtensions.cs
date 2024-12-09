using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CSModLib.GameObjects {
    public static class FlagExtensions {

        public static bool IsFlagSet(this NetInfo.LaneType laneType, NetInfo.LaneType flag)
            => (laneType & flag) != 0;

        public static bool IsFlagSet(this VehicleInfo.VehicleType vehicleType, VehicleInfo.VehicleType flag)
            => (vehicleType & flag) != 0;
    }
}
