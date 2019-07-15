namespace TrafficManager.API.Traffic.Enums {
    public enum ToggleTrafficLightUnableReason {
        None,
        NoJunction,
        HasTimedLight,
        IsLevelCrossing,
        InsufficientSegments
    }
}