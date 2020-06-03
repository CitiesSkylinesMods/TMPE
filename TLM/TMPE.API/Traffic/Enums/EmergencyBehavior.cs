namespace TrafficManager.API.Traffic.Enums {
    public enum EmergencyBehavior {
        /// <summary>
        /// No custom behavior
        /// </summary>
        None,

        /// <summary>
        /// (on two-way roads) Vehicles on both sides drive to the outer side to create a rescue lane in the middle of the road
        /// (on one-way roads) Vehicles drive to the outer side to create a rescure lane on the inner side of the road
        /// </summary>
        RescueLane,

        /// <summary>
        /// Clear innermost lane for emergency vehicles
        /// </summary>
        InnerLane,

        /// <summary>
        /// Emergency vehicles drive on the parking lane
        /// </summary>
        DriveOnParkingLane,

        /// <summary>
        /// Regular vehicles evade on the parking lane
        /// </summary>
        EvadeOnParkingLane,
    }
}