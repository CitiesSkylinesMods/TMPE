namespace TrafficManager.State {
    using System;
    using TrafficManager.API.Traffic.Data;
    using TrafficManager.UI;
    using TrafficManager.UI.SubTools.SpeedLimits;
    using TrafficManager.Util;

    /// <summary>
    /// Used to give clear command to SetSpeedLimit.
    /// Does not define where the speed limit goes (to override per segment, to override per lane,
    /// to default per road type etc) for that <see cref="SetSpeedLimitTarget"/> is used.
    /// </summary>
    public readonly struct SetSpeedLimitAction {
        /// <summary>Defines the action on set speedlimit call, constants are used as indexes in
        /// <see cref="SetSpeedLimitAction.Variant3"/>.</summary>
        public enum ActionType {
            /// <summary>The Value contains the speed to set.</summary>
            SetOverride,

            /// <summary>Speed is set to unlimited (1000 km/h or 621 MPH).</summary>
            Unlimited,

            /// <summary>Speed override is reset.</summary>
            ResetToDefault,
        }

        /// <summary>
        /// Guards the contained override value, and allows access to it only if valuetype is
        /// SetOverride or Unlimited.
        /// </summary>
        public readonly struct Variant3 {
            /// <summary>Determines which ActionType this is.</summary>
            public readonly ActionType Which;

            private readonly SpeedValue overrideValue_;

            /// <summary>Only accessible if the value type is SetOverride or Unlimited.</summary>
            /// <exception cref="Exception">The value type is not SetOverride.</exception>
            public SpeedValue Override =>
                this.Which is ActionType.SetOverride or ActionType.Unlimited
                    ? this.overrideValue_
                    : throw new Exception(
                          $"Variant3's override value is not accessible, current value type is {this.Which}");

            public Variant3(SpeedValue v, ActionType valueType) {
                this.overrideValue_ = v;
                this.Which = valueType;
            }
        }

        /// <summary>
        /// A variant struct which contains a SpeedValue, but only will let you access it if its type field
        /// (Variant3.Which) is set to either SetOverride or Unlimited.
        /// </summary>
        public readonly Variant3 GuardedValue;

        /// <summary>Returns integer for which value is stored. Use <see cref="ActionType"/> constants.</summary>
        public ActionType Type => this.GuardedValue.Which;

        public override string ToString() {
            switch (this.GuardedValue.Which) {
                case ActionType.SetOverride:
                    return this.GuardedValue.Override.FormatStr(
                        GlobalConfig.Instance.Main.DisplaySpeedLimitsMph);
                case ActionType.Unlimited: return Translation.SpeedLimits.Get("Palette.Text:Unlimited");
                case ActionType.ResetToDefault: return Translation.SpeedLimits.Get("Palette.Text:Default");
                default: return string.Empty;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SetSpeedLimitAction"/> struct.
        /// This becomes a SetOverride variant.
        /// </summary>
        private SetSpeedLimitAction(SpeedValue v) {
            this.GuardedValue = new(v, ActionType.SetOverride);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SetSpeedLimitAction"/> struct.
        /// This becomes either Unlimited or ResetToDefault variant.</summary>
        private SetSpeedLimitAction(ActionType which) {
            switch (which) {
                case ActionType.Unlimited:
                    this.GuardedValue = new(SpeedValue.UNLIMITED_SPEEDVALUE, which);
                    break;
                case ActionType.ResetToDefault:
                    this.GuardedValue = new(SpeedValue.NO_OVERRIDE, which);
                    break;
                default:
                    throw new Exception(
                        "SpeedLimitAction/1 constructor does not accept this variant type");
            }
        }

        public static SetSpeedLimitAction ResetToDefault() {
            return new SetSpeedLimitAction(ActionType.ResetToDefault);
        }

        public static SetSpeedLimitAction Unlimited() {
            return new SetSpeedLimitAction(ActionType.Unlimited);
        }

        public static SetSpeedLimitAction SetOverride(SpeedValue v) {
            return new SetSpeedLimitAction(v);
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
            if (this.GuardedValue.Which != other.GuardedValue.Which) {
                return false;
            }

            if (this.GuardedValue.Which == ActionType.SetOverride) {
                return FloatUtil.NearlyEqual(
                    a: this.GuardedValue.Override.GameUnits,
                    b: other.GuardedValue.Override.GameUnits);
            }

            return true; // otherwise equal
        }
    }
}