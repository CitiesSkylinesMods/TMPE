namespace TrafficManager.State {
    using TrafficManager.API.Traffic.Data;

    public struct GetSpeedLimitResult {
        /// <summary>Valid only if override exists.</summary>
        public SpeedValue? OverrideValue;

        /// <summary>Contains known default speed limit, or null.</summary>
        public SpeedValue? DefaultValue;

        /// <summary>
        /// Initializes a new instance of the <see cref="GetSpeedLimitResult"/> struct
        /// from maybe an override value (nullable SpeedValue) and a
        /// default speedlimit value for that lane type.
        /// </summary>
        /// <param name="overrideValue">Speed limit override value, if exists.</param>
        /// <param name="defaultValue">Default speed value if known.</param>
        public GetSpeedLimitResult(SpeedValue? overrideValue, SpeedValue? defaultValue) {
            OverrideValue = overrideValue;
            DefaultValue = defaultValue;
        }
    }
}