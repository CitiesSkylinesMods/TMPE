namespace TrafficManager.API.Traffic.Enums {
    using System;

    // I'm just dumping it here for now so I don't forget to implement it later
    // In particular the ability to filter persistent view to specific categories
    // would be usful - eg. in InfoMode.FireSafety we only want to know about
    // restrictions for emergency vehicles.
    [Flags]
    public enum RestrictedVehicles : short {
        None = 0,

        Transport = 1 << 0, // eg. buses, passenger train/plane, etc.
        Cargo = 1 << 1, // eg. trucks, cargo train/plane, etc.
        Emergency = 1 << 2,
        Cars = 1 << 3,
        Services = 1 << 4,
        Taxis = 1 << 5,

        Bicycle = 1 << 6, // future?
        Motorbike = 1 << 7, // future?
        Pedestrian = 1 << 8, // future?

        Small = 1 << 10,
        Medium = 1 << 11,
        Large = 1 << 12, // could also be used for large vehicle flag (eg. lorries)?

        Intercity = 1 << 13, // future?
        Local = 1 << 14, // future?

        AllTypes =
            Transport | Cargo | Emergency |
            Cars | Services | Taxis,

        AllSizes = Small | Medium | Large,

        AllPlaces = Intercity | Local,

        All = AllTypes | AllSizes | AllPlaces,
    }
}
