namespace TrafficManager.API.Traffic.Enums {
    public enum SetPrioritySignUnableReason {
        None,
        NoJunction,
        HasTimedLight,
        InvalidSegment,
        NotIncoming
    }
}