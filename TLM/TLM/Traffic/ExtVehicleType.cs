namespace TrafficManager.Traffic {
    using System;

    /// <summary>
    /// This Enum is kept for save compatibility.
    /// DO NOT USE.
    /// Please use TMPE.API.Traffic.Enums.ExtVehicleType
    /// </summary>
    [Flags]
    [Obsolete]
    public enum ExtVehicleType {
        None = 0,
        PassengerCar = 1,
        Bus = 1 << 1,
        Taxi = 1 << 2,
        CargoTruck = 1 << 3,
        Service = 1 << 4,
        Emergency = 1 << 5,
        PassengerTrain = 1 << 6,
        CargoTrain = 1 << 7,
        Tram = 1 << 8,
        Bicycle = 1 << 9,
        Pedestrian = 1 << 10,
        PassengerShip = 1 << 11,
        CargoShip = 1 << 12,
        PassengerPlane = 1 << 13,
        Helicopter = 1 << 14,
        CableCar = 1 << 15,
        PassengerFerry = 1 << 16,
        PassengerBlimp = 1 << 17,
        CargoPlane = 1 << 18,
        Plane = PassengerPlane | CargoPlane,
        Ship = PassengerShip | CargoShip,
        CargoVehicle = CargoTruck | CargoTrain | CargoShip | CargoPlane,
        PublicTransport = Bus | Taxi | Tram | PassengerTrain,
        RoadPublicTransport = Bus | Taxi,
        RoadVehicle = PassengerCar | Bus | Taxi | CargoTruck | Service | Emergency,
        RailVehicle = PassengerTrain | CargoTrain,
        NonTransportRoadVehicle = RoadVehicle & ~PublicTransport,
        Ferry = PassengerFerry,
        Blimp = PassengerBlimp
    }

    public static class LegacyExtVehicleType {
        public static API.Traffic.Enums.ExtVehicleType ToNew(ExtVehicleType old) {
            return (API.Traffic.Enums.ExtVehicleType)(int)old;
        }

        public static ExtVehicleType ToOld(API.Traffic.Enums.ExtVehicleType new_) {
            return (ExtVehicleType)(int)new_;
        }
    }
}