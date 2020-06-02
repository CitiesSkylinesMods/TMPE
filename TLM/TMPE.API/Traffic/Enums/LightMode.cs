namespace TrafficManager.API.Traffic.Enums {
    public enum LightMode {
        Simple = 1, // ↑
        SingleLeft = 2, // ←, ↑→
        SingleRight = 3, // ←↑, →
        All = 4, // ←, ↑, →
    }
}