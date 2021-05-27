namespace TrafficManager.Traffic {
    using System;

    [Obsolete("For save compatibility. Instead use Traffic.Enums.ExtVehicleType.")]
    public static class LegacyExtVehicleType {
        public static API.Traffic.Enums.ExtVehicleType ToNew(ExtVehicleType old) {
            return (API.Traffic.Enums.ExtVehicleType)(int)old;
        }

        public static ExtVehicleType ToOld(API.Traffic.Enums.ExtVehicleType new_) {
            return (ExtVehicleType)(int)new_;
        }
    }
}
