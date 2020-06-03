namespace TrafficManager.API.Traffic.Enums {
    public enum SetPrioritySignError {
        None,
        NoJunction,
        HasTimedLight,
        InvalidSegment,
        NotIncoming,
    }
}