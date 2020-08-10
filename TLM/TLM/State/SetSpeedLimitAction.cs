namespace TrafficManager {
    using System;

    /// <summary>Used to give clear command to SetSpeedLimit.</summary>
    public readonly struct SetSpeedLimitAction {
        /// <summary>Defines the action on set speedlimit call.</summary>
        public enum ValueType {
            /// <summary>The Value is game speed units.</summary>
            GameSpeedUnits,

            /// <summary>The value is ignored. Speed is set to unlimited.</summary>
            Unlimited,

            /// <summary>The value is ignored. Speed override is reset.</summary>
            Default,
        }

        /// <summary>Defines the action type.</summary>
        public readonly ValueType Type;

        /// <summary>If Type is GameSpeedUnits, contains the value.</summary>
        public readonly float Value;

        private SetSpeedLimitAction(ValueType t, float v) {
            this.Type = t;
            this.Value = v;
        }

        public static SetSpeedLimitAction Default() {
            return new SetSpeedLimitAction(ValueType.Default, 0f);
        }

        public static SetSpeedLimitAction Unlimited() {
            return new SetSpeedLimitAction(ValueType.Unlimited, 0f);
        }

        public static SetSpeedLimitAction GameSpeedUnits(float v) {
            return new SetSpeedLimitAction(ValueType.GameSpeedUnits, v);
        }

        /// <summary>Reapply value from GetSpeedLimitResult.</summary>
        /// <param name="res">Value to apply.</param>
        /// <returns>New action to pass to SetSpeedLimit.</returns>
        public static SetSpeedLimitAction FromGetResult(GetSpeedLimitResult res) {
            switch (res.Type) {
                case GetSpeedLimitResult.ResultType.Unlimited:
                    return Unlimited();
                case GetSpeedLimitResult.ResultType.Value:
                    return GameSpeedUnits(res.Value.GameUnits);
                case GetSpeedLimitResult.ResultType.NotSet:
                    return Default();
            }

            throw new NotImplementedException();
        }
    }
}
