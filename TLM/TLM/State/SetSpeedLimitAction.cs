namespace TrafficManager {
    using System;
    using TrafficManager.API.Traffic.Data;

    /// <summary>Used to give clear command to SetSpeedLimit.</summary>
    public readonly struct SetSpeedLimitAction {
        /// <summary>Defines the action on set speedlimit call.</summary>
        public enum ValueType {
            /// <summary>The Value contains the speed to set.</summary>
            SetSpeed,

            /// <summary>The value is ignored. Speed is set to unlimited.</summary>
            Unlimited,

            /// <summary>The value is ignored. Speed override is reset.</summary>
            Default,
        }

        /// <summary>Defines the action type.</summary>
        public readonly ValueType Type;

        /// <summary>If Type is GameSpeedUnits, contains the value.</summary>
        public readonly SpeedValue Value;

        private SetSpeedLimitAction(ValueType t, SpeedValue v) {
            this.Type = t;
            this.Value = v;
        }

        public static SetSpeedLimitAction Default() {
            return new SetSpeedLimitAction(ValueType.Default, default);
        }

        public static SetSpeedLimitAction Unlimited() {
            return new SetSpeedLimitAction(ValueType.Unlimited, default);
        }

        public static SetSpeedLimitAction SetSpeed(SpeedValue v) {
            return new SetSpeedLimitAction(ValueType.SetSpeed, v);
        }

        /// <summary>Reapply value from GetSpeedLimitResult.</summary>
        /// <param name="res">Value to apply.</param>
        /// <returns>New action to pass to SetSpeedLimit.</returns>
        public static SetSpeedLimitAction FromGetResult(GetSpeedLimitResult res) {
            switch (res.Type) {
                case GetSpeedLimitResult.ResultType.NotAvailable:
                    return Default();
                case GetSpeedLimitResult.ResultType.OverrideExists:
                    return res.OverrideValue.HasValue
                        ? SetSpeed(res.OverrideValue.Value)
                        : Default();
            }

            throw new NotImplementedException();
        }
    }
}
