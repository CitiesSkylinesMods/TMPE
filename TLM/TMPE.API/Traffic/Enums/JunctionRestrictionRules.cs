namespace TrafficManager.API.Traffic.Enums {
    public enum JunctionRestrictionRules {
        Uturn = 1 << 0,
        NearTurnOnRed = 1 << 1,
        FarTurnOnRed = 1 << 2,
        ForwardLaneChange = 1 << 3,
        EnterWhenBlocked = 1 << 4,
        AllowPedestrianCrossing = 1 << 5,
    }
}