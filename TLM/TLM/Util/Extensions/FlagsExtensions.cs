namespace TrafficManager.Util.Extensions {
    internal static class FlagsExtensions {
        internal static bool IsFlagSet(this NetInfo.LaneType value, NetInfo.LaneType flag) => (value & flag) != 0;

        internal static bool IsFlagSet(this VehicleInfo.VehicleType value, VehicleInfo.VehicleType flag) => (value & flag) != 0;

        internal static bool IsFlagSet(this Vehicle.Flags value, Vehicle.Flags flag) => (value & flag) != 0;

        internal static bool IsFlagSet(this NetNode.Flags value, NetNode.Flags flag) => (value & flag) != 0;

        internal static bool IsFlagSet(this NetSegment.Flags value, NetSegment.Flags flag) => (value & flag) != 0;

        internal static bool IsFlagSet(this NetLane.Flags value, NetLane.Flags flag) => (value & flag) != 0;

        internal static bool CheckFlags(this NetInfo.LaneType value, NetInfo.LaneType required, NetInfo.LaneType forbidden = 0) =>
            (value & (required | forbidden)) == required;

        internal static bool CheckFlags(this VehicleInfo.VehicleType value, VehicleInfo.VehicleType required, VehicleInfo.VehicleType forbidden = 0) =>
            (value & (required | forbidden)) == required;

        internal static bool CheckFlags(this Vehicle.Flags value, Vehicle.Flags required, Vehicle.Flags forbidden = 0) =>
            (value & (required | forbidden)) == required;

        internal static bool CheckFlags(this NetNode.Flags value, NetNode.Flags required, NetNode.Flags forbidden = 0) =>
            (value & (required | forbidden)) == required;

        internal static bool CheckFlags(this NetSegment.Flags value, NetSegment.Flags required, NetSegment.Flags forbidden = 0) =>
            (value & (required | forbidden)) == required;

        internal static bool CheckFlags(this NetLane.Flags value, NetLane.Flags required, NetLane.Flags forbidden = 0) =>
            (value & (required | forbidden)) == required;
    }
}
