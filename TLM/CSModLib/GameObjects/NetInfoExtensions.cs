using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CSModLib.GameObjects {
    public static class NetInfoExtensions {

        public static bool IsRoadLane(this NetInfo.Lane lane) =>
            lane.m_laneType.IsFlagSet(NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)
            && lane.m_vehicleType.IsFlagSet(VehicleInfo.VehicleType.Car | VehicleInfo.VehicleType.Trolleybus);

        public static bool IsCarLane(this NetInfo.Lane lane) =>
            lane.m_laneType.IsFlagSet(NetInfo.LaneType.Vehicle)
            && lane.m_vehicleType.IsFlagSet(VehicleInfo.VehicleType.Car);
    }
}
