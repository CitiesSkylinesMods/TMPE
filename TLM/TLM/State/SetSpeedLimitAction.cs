namespace TrafficManager.State {
    using JetBrains.Annotations;
    using TrafficManager.API.Traffic.Data;
    using TrafficManager.UI;
    using TrafficManager.Util;

    /// <summary>
    /// Used to give clear command to SetSpeedLimit.
    /// Does not define where the speed limit goes (to override per segment, to override per lane,
    /// to default per road type etc) for that <see cref="SetSpeedLimitTarget"/> is used.
    /// </summary>
    public readonly struct SetSpeedLimitAction {
        public static readonly SetSpeedLimitAction NO_ACTION = new(ActionType.NoAction, null);

        /// <summary>Defines the action on set speedlimit call.</summary>
        public enum ActionType {
            /// <summary>Dummy action which does nothing.</summary>
            NoAction,

            /// <summary>The Value contains the speed to set.</summary>
            SetOverride,

            /// <summary>Speed is set to unlimited (1000 km/h or 621 MPH).</summary>
            Unlimited,

            /// <summary>Speed override is reset.</summary>
            ResetToDefault,
        }

        /// <summary>Defines the action type.</summary>
        public readonly ActionType Type;

        /// <summary>If Type is GameSpeedUnits, contains the value.</summary>
        public readonly SpeedValue? Override;

        public override string ToString() {
            switch (this.Type) {
                case ActionType.SetOverride:
                    return this.Override != null
                               ? this.Override.Value.FormatStr(GlobalConfig.Instance.Main.DisplaySpeedLimitsMph)
                               : string.Empty;
                case ActionType.Unlimited: return Translation.SpeedLimits.Get("Unlimited");
                case ActionType.ResetToDefault: return Translation.SpeedLimits.Get("Default");
                default: return string.Empty;
            }
        }

        private SetSpeedLimitAction(ActionType t, SpeedValue? v) {
            this.Type = t;
            this.Override = v;
        }

        public static SetSpeedLimitAction ResetToDefault() {
            return new(ActionType.ResetToDefault, null);
        }

        public static SetSpeedLimitAction Unlimited() {
            return new(
                ActionType.Unlimited,
                SpeedValue.UNLIMITED_SPEEDVALUE);
        }

        public static SetSpeedLimitAction SetOverride(SpeedValue v) {
            return new(ActionType.SetOverride, v);
        }

        /// <summary>Reapply value from GetSpeedLimitResult.</summary>
        /// <param name="res">Value to apply.</param>
        /// <returns>New action to pass to SetSpeedLimit.</returns>
        public static SetSpeedLimitAction FromNullableFloat(float? res) {
            return res.HasValue
                       ? (res.Value > 0f
                              ? SetOverride(new SpeedValue(res.Value))
                              : Unlimited())
                       : ResetToDefault();
        }

        public bool NearlyEqual(in SetSpeedLimitAction other) {
            if (this.Type != other.Type) {
                return false;
            }

            if (!this.Override.HasValue) {
                // if this has no value and other has no value, they are equal
                // if this has no value and other doesn't, they are not equal
                return !other.Override.HasValue;
            }

            if (!other.Override.HasValue) {
                // if this has value, but other doesn't - not equal
                return false;
            }

            return FloatUtil.NearlyEqual(
                a: this.Override.Value.GameUnits,
                b: other.Override.Value.GameUnits);
        }
    }
}