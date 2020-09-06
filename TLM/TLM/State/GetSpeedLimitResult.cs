namespace TrafficManager {
    using System.Runtime.CompilerServices;
    using TrafficManager.API.Traffic.Data;

    public struct GetSpeedLimitResult {
        public enum ResultType {
            /// <summary>The speed limit had an override.</summary>
            OverrideExists,

            /// <summary>There was no override possible.</summary>
            NotAvailable,
        }

        public ResultType Type;

        /// <summary>Valid only if Type=='Value'.</summary>
        public SpeedValue? OverrideValue;

        public SpeedValue DefaultValue;

        /// <summary>
        /// Initializes a new instance of the <see cref="GetSpeedLimitResult"/> struct
        /// from maybe an override value (nullable SpeedValue) and a
        /// default speedlimit value for that lane type.
        /// </summary>
        /// <param name="value">Nullable override value.</param>
        /// <param name="defaultValue">Default speed value.</param>
        public GetSpeedLimitResult(SpeedValue? value, SpeedValue defaultValue) {
            Type = ResultType.OverrideExists;
            OverrideValue = value;
            DefaultValue = defaultValue;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="GetSpeedLimitResult"/> struct
        /// as error state (no override possible).
        /// </summary>
        public GetSpeedLimitResult(ResultType _ignored) {
            Type = ResultType.NotAvailable;
            OverrideValue = null;
            DefaultValue = new SpeedValue(0f);
        }
    }
}