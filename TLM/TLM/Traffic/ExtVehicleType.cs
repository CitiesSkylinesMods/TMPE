namespace TrafficManager.Traffic {
    using System;
    using JetBrains.Annotations;

    /// <summary>
    /// This Enum is kept for save compatibility.
    /// DO NOT USE.
    /// Please use TMPE.API.Traffic.Enums.ExtVehicleType.
    /// </summary>
    [Flags]
    [Obsolete("For save compatibility. Instead use Traffic.Enums.ExtVehicleType.")]
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

        [UsedImplicitly]
        Bicycle = 1 << 9,

        [UsedImplicitly]
        Pedestrian = 1 << 10,
        PassengerShip = 1 << 11,
        CargoShip = 1 << 12,
        PassengerPlane = 1 << 13,

        [UsedImplicitly]
        Helicopter = 1 << 14,

        [UsedImplicitly]
        CableCar = 1 << 15,
        PassengerFerry = 1 << 16,
        PassengerBlimp = 1 << 17,
        CargoPlane = 1 << 18,
        TrolleyBus = 1 << 19,

        [UsedImplicitly]
        Plane = PassengerPlane | CargoPlane,

        [UsedImplicitly]
        Ship = PassengerShip | CargoShip,

        [UsedImplicitly]
        CargoVehicle = CargoTruck | CargoTrain | CargoShip | CargoPlane,
        PublicTransport = Bus | Taxi | Tram | PassengerTrain | TrolleyBus,

        [UsedImplicitly]
        RoadPublicTransport = Bus | Taxi,
        RoadVehicle = PassengerCar | Bus | Taxi | CargoTruck | Service | Emergency,

        [UsedImplicitly]
        RailVehicle = PassengerTrain | CargoTrain,

        [UsedImplicitly]
        NonTransportRoadVehicle = RoadVehicle & ~PublicTransport,

        [UsedImplicitly]
        Ferry = PassengerFerry,

        [UsedImplicitly]
        Blimp = PassengerBlimp,
    }
}