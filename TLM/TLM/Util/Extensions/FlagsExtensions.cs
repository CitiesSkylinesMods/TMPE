namespace TrafficManager.Util.Extensions {
    using System;
    using TrafficManager.API.Traffic.Enums;

    internal static class FlagsExtensions {

        /// <summary>Checks if only a single bit is set in enum or flags.</summary>
        /// <param name="flags">The enum flags to inspect.</param>
        /// <returns>Returns <c>true</c> if exactly 1 bit set, otherwise <c>false</c>.</returns>
        internal static bool IsSingleFlag(this Enum flags) {
            var bits = (int)(object)flags; // TODO: find a way to avoid this
            return bits != 0 && (bits & (bits - 1)) == 0; // https://stackoverflow.com/a/4624295/5240636
        }

        internal static bool IsFlagSet(this Building.Flags value, Building.Flags flag) => (value & flag) != 0;

        internal static bool IsFlagSet(this Citizen.Flags value, Citizen.Flags flag) => (value & flag) != 0;

        internal static bool IsFlagSet(this CitizenInstance.Flags value, CitizenInstance.Flags flag) => (value & flag) != 0;

        internal static bool IsFlagSet(this NetInfo.LaneType value, NetInfo.LaneType flag) => (value & flag) != 0;

        internal static bool IsFlagSet(this VehicleInfo.VehicleType value, VehicleInfo.VehicleType flag) => (value & flag) != 0;

        internal static bool IsFlagSet(this ExtVehicleType value, ExtVehicleType flag) => (value & flag) != 0;

        internal static bool IsFlagSet(this Vehicle.Flags value, Vehicle.Flags flag) => (value & flag) != 0;

        internal static bool IsFlagSet(this NetNode.Flags value, NetNode.Flags flag) => (value & flag) != 0;

        internal static bool IsFlagSet(this NetSegment.Flags value, NetSegment.Flags flag) => (value & flag) != 0;

        internal static bool IsFlagSet(this NetLane.Flags value, NetLane.Flags flag) => (value & flag) != 0;

        internal static bool IsFlagSet(this VehicleParked.Flags value, VehicleParked.Flags flag) => (value & flag) != 0;

        internal static bool IsFlagSet(this NetInfo.Direction value, NetInfo.Direction flag) => (value & flag) != 0;

        internal static bool CheckFlags(this Building.Flags value, Building.Flags required, Building.Flags forbidden = 0) =>
            (value & (required | forbidden)) == required;

        internal static bool CheckFlags(this CitizenInstance.Flags value, CitizenInstance.Flags required, CitizenInstance.Flags forbidden = 0) =>
            (value & (required | forbidden)) == required;

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

        internal static bool CheckFlags(this NetInfo.Direction value, NetInfo.Direction required, NetInfo.Direction forbidden = 0) =>
            (value & (required | forbidden)) == required;
    }
}
