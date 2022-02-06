namespace TrafficManager.API.Traffic {
    public static class ApiConstants {
        /// <summary>
        /// Conversion rate from game speed units to km/h
        /// </summary>
        public const float SPEED_TO_KMPH = 50.0f; // 1.0f game speed equals 50 km/h

        /// <summary>
        /// Conversion rate from game speed units to MPH
        /// </summary>
        public const float SPEED_TO_MPH = 31.06f; // 50 km/h converted to mph

        /// <summary>
        /// Multiplier used to convert between game speedlimits and velocities (directed speeds)
        /// </summary>
        public const float SPEED_TO_VELOCITY = 8f;
    }
}