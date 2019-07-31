namespace TrafficManager.API.Traffic {
    public static class ApiConstants {
        /// <summary>
        /// Conversion rate from km/h to game speed (also exists in TrafficManager.Constants)
        /// </summary>
        public const float SPEED_TO_KMPH = 50.0f; // 1.0f equals 50 km/h

        /// <summary>
        /// Conversion rate from MPH to game speed (also exists in TrafficManager.Constants)
        /// </summary>
        private const float SPEED_TO_MPH = 32.06f; // 50 km/h converted to mph
    }
}