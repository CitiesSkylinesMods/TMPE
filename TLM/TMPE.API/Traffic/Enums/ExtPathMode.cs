namespace TrafficManager.API.Traffic.Enums {
    public enum ExtPathMode {
        None = 0,

        /// <summary>
        /// Indicates that the citizen requires a walking path to their parked car
        /// </summary>
        RequiresWalkingPathToParkedCar = 1,

        /// <summary>
        /// Indicates that a walking path to the parked car is being calculated
        /// </summary>
        CalculatingWalkingPathToParkedCar = 2,

        /// <summary>
        /// Indicates that the citizen is walking to their parked car
        /// </summary>
        WalkingToParkedCar = 3,

        /// <summary>
        /// Indicates that the citizen is close to their parked car
        /// </summary>
        ApproachingParkedCar = 4,

        /// <summary>
        /// Indicates that the citizen has reached their parked car and requires a car path now
        /// </summary>
        RequiresCarPath = 5,

        /// <summary>
        /// Indicates that a direct car path to the target is being calculated
        /// </summary>
        CalculatingCarPathToTarget = 6,

        /// <summary>
        /// Indicates that a car path to a known parking spot near the target is being calculated
        /// </summary>
        CalculatingCarPathToKnownParkPos = 7,

        /// <summary>
        /// Indicates that the citizen is currently driving on a direct path to target
        /// </summary>
        DrivingToTarget = 8,

        /// <summary>
        /// Indiciates that the citizen is currently driving to a known parking spot near the target
        /// </summary>
        DrivingToKnownParkPos = 9,

        /// <summary>
        /// Indicates that the vehicle is being parked on an alternative parking position
        /// </summary>
        RequiresWalkingPathToTarget = 10,

        /// <summary>
        /// Indicates that parking has failed
        /// </summary>
        ParkingFailed = 11,

        /// <summary>
        /// Indicates that a path to an alternative parking position is being calculated
        /// </summary>
        CalculatingCarPathToAltParkPos = 12,

        /// <summary>
        /// Indicates that the vehicle is on a path to an alternative parking position
        /// </summary>
        DrivingToAltParkPos = 13,

        /// <summary>
        /// Indicates that a walking path to target is being calculated
        /// </summary>
        CalculatingWalkingPathToTarget = 14,

        /// <summary>
        /// Indicates that the citizen is currently walking to the target
        /// </summary>
        WalkingToTarget = 15,

        /// <summary>
        /// (DEPRECATED) Indicates that the citizen is using public transport (bus/train/tram/subway) to reach the target
        /// </summary>
        __Deprecated__PublicTransportToTarget = 16,

        /// <summary>
        /// Indicates that the citizen is using a taxi to reach the target
        /// </summary>
        TaxiToTarget = 17,

        /// <summary>
        /// Indicates that the driving citizen requires a direct path to target (driving/public transport)
        /// where possible transitions between different modes of transport happen as required (thus no search
        /// for parking spaces is performed beforehand)
        /// </summary>
        RequiresMixedCarPathToTarget = 18,
    }
}