using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.Util.Extensions;

namespace TrafficManager.ExtPrefabs {
    internal static class NetInfoExtensions {

        public static bool IsRoadLane(this NetInfo.Lane lane) =>
            lane.m_laneType.IsFlagSet(NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)
            && lane.m_vehicleType.IsFlagSet(VehicleInfo.VehicleType.Car | VehicleInfo.VehicleType.Trolleybus);
    }
}
