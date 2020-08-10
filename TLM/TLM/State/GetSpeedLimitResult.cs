namespace TrafficManager {
    using TrafficManager.API.Traffic.Data;

    public struct GetSpeedLimitResult {
        public enum ResultType {
            /// <summary>The speed limit override was set to unlimited.</summary>
            Unlimited,

            /// <summary>The speed limit had an override.</summary>
            Value,

            /// <summary>There was no override.</summary>
            NotSet,
        }

        public ResultType Type;
        public SpeedValue Value;

        public static GetSpeedLimitResult NotSet() {
            return new GetSpeedLimitResult {
                Type = ResultType.NotSet,
                Value = new SpeedValue(0f),
            };
        }

        public static GetSpeedLimitResult CreateValue(SpeedValue v) {
            return new GetSpeedLimitResult {
                Type = ResultType.Value,
                Value = v,
            };
        }

        public static GetSpeedLimitResult CreateKmph(float km) {
            return new GetSpeedLimitResult {
                Type = ResultType.Value,
                Value = SpeedValue.FromKmph(km),
            };
        }
    }
}