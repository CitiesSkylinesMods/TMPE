namespace TrafficManager.API.Traffic.Enums {
    public enum PriorityType {
        None = 0,

        /// <summary>
        /// Priority road
        /// </summary>
        Main = 1,

        /// <summary>
        /// Stop sign
        /// </summary>
        Stop = 2,

        /// <summary>
        /// Yield sign
        /// </summary>
        Yield = 3,
    }
}