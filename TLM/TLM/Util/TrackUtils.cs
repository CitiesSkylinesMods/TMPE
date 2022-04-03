namespace TrafficManager.Util {
    using TrafficManager.Util.Extensions;

    internal static class TrackUtils {
        internal const NetInfo.LaneType LANE_TYPES =
            NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle;

        internal const VehicleInfo.VehicleType VEHICLE_TYPES =
            VehicleInfo.VehicleType.Metro |
            VehicleInfo.VehicleType.Train |
            VehicleInfo.VehicleType.Tram |
            VehicleInfo.VehicleType.Monorail;

        internal static bool IsTrackOnly(this NetInfo.Lane laneInfo) {
            return
                laneInfo != null &&
                laneInfo.m_laneType.IsFlagSet(LANE_TYPES) &&
                !laneInfo.m_laneType.IsFlagSet(~LANE_TYPES) &&
                laneInfo.m_vehicleType.IsFlagSet(VEHICLE_TYPES) &&
                !laneInfo.m_vehicleType.IsFlagSet(~VEHICLE_TYPES);
        }

        public static bool IsBidirectional(this NetInfo.Lane laneInfo) {
            // Note that for station tracks we have:
            // AvoidForward = Both + 4
            // AvoidBackward = Both + 8
            return laneInfo.m_direction.CheckFlags(NetInfo.Direction.Both);
        }
    }
}
