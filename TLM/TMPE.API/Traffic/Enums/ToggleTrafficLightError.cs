namespace TrafficManager.API.Traffic.Enums {
    public enum ToggleTrafficLightError {
        None,
        NoJunction,
        HasTimedLight,
        IsLevelCrossing,
        InsufficientSegments,
    }
}