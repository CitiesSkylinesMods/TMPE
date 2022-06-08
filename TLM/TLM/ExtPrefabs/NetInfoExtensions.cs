using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TrafficManager.ExtPrefabs {
    internal static class NetInfoExtensions {

        public static bool IsRoadLane(this NetInfo.Lane lane) =>
            lane.m_laneType == NetInfo.LaneType.Vehicle && (lane.m_vehicleType & VehicleInfo.VehicleType.Car) != 0;
    }
}
