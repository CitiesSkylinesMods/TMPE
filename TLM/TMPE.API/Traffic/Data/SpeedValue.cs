namespace TrafficManager.API.Traffic.Data {
    using System;
    using JetBrains.Annotations;
    using UnityEngine;

    /// <summary>
    /// Represents a speed value expressed in game units where 1f = 50 km/h or 32 MPH.
    /// </summary>
    [Serializable]
    public readonly struct SpeedValue : IEquatable<SpeedValue> {
        /// <summary>Speed value in game units corresponding to Unlimited speed on that lane.</summary>
        public const float UNLIMITED = 20.0f;

        public static readonly SpeedValue UNLIMITED_SPEEDVALUE = new(UNLIMITED);

        /// <summary>Special speed value for no override.</summary>
        public static readonly SpeedValue NO_OVERRIDE = new(-1f);

        /// <summary>
        /// Initializes a new instance of the <see cref="SpeedValue"/> struct from game units float.
        /// </summary>
        /// <param name="gameUnits">The value in game speed units</param>
        public SpeedValue(float gameUnits) {
            GameUnits = gameUnits;
        }

        /// <summary>
        /// Sets or returns stored value in game units (no conversion)
        /// </summary>
        public float GameUnits { get; }

        /// <summary>
        /// Converts stored value in game units into velocity magnitude (8x the game units)
        /// </summary>
        /// <returns>Velocity unit used by the game for moving objects.</returns>
        [UsedImplicitly]
        public float ToVelocity() => GameUnits * ApiConstants.SPEED_TO_VELOCITY;

        /// <summary>
        /// Constructs a SpeedValue from velocity magnitude (8x the game units)
        /// </summary>
        /// <param name="vel">The velocity value originating from a vehicle velocity</param>
        /// <returns>A new SpeedValue downscaled to game units</returns>
        public static SpeedValue FromVelocity(float vel)
            => new(vel / ApiConstants.SPEED_TO_VELOCITY);

        /// <summary>
        /// Constructs a SpeedValue from km/hour given as an integer
        /// </summary>
        /// <param name="kmph">A speed in kilometres/hour</param>
        /// <returns>A new speedvalue converted to the game units</returns>
        public static SpeedValue FromKmph(float kmph)
            => new(kmph / ApiConstants.SPEED_TO_KMPH);

        /// <summary>
        /// Constructs a speed value from km/hour given as a KmphValue
        /// </summary>
        /// <param name="kmph">Km/hour typed value</param>
        /// <returns>A new SpeedValue scaled to the game units</returns>
        public static SpeedValue FromKmph(KmphValue kmph)
            => new(kmph.Kmph / ApiConstants.SPEED_TO_KMPH);

        /// <summary>
        /// Constructs a SpeedValue from miles/hour given as an integer
        /// </summary>
        /// <param name="mph">A speed in miles/hour</param>
        /// <returns>A new speedvalue converted to the game units</returns>
        public static SpeedValue FromMph(float mph)
            => new(mph / ApiConstants.SPEED_TO_MPH);

        /// <summary>
        /// Constructs a speed value from miles/hour given as a MphValue
        /// </summary>
        /// <param name="mph">Miles/hour typed value</param>
        /// <returns>A new SpeedValue scaled to the game units</returns>
        public static SpeedValue FromMph(MphValue mph)
            => new(mph.Mph / ApiConstants.SPEED_TO_MPH);

        /// <summary>
        /// Subtracts two speed values
        /// </summary>
        /// <param name="x">First value</param>
        /// <param name="y">Second value</param>
        /// <returns>A new SpeedValue which is a difference between x and y</returns>
        public static SpeedValue operator -(SpeedValue x, SpeedValue y)
            => new(x.GameUnits - y.GameUnits);

        /// <summary>
        /// Convert float game speed to mph and round to nearest STEP
        /// </summary>
        /// <param name="step">Rounds to the nearest step units</param>
        /// <returns>Speed in MPH rounded to nearest step (5 MPH)</returns>
        public MphValue ToMphRounded(float step) {
            float mph = GameUnits * ApiConstants.SPEED_TO_MPH;
            return new MphValue((ushort)(Mathf.Round(mph / step) * step));
        }

        /// <summary>
        /// Convert float game speed to km/h and round to nearest STEP
        /// </summary>
        /// <param name="speed">Speed, scale: 1f=50km/h</param>
        /// <returns>Speed in km/h rounded to nearest 10 km/h</returns>
        public KmphValue ToKmphRounded(float step) {
            float kmph = GameUnits * ApiConstants.SPEED_TO_KMPH;
            return new KmphValue((ushort)(Mathf.Round(kmph / step) * step));
        }

        /// <summary>
        /// Converts this SpeedValue into miles/hour
        /// </summary>
        /// <returns>A typed mph value with integer miles/hour</returns>
        public MphValue ToMphPrecise()
            => new((ushort)Mathf.Round(GameUnits * ApiConstants.SPEED_TO_MPH));

        /// <summary>
        /// Converts this SpeedValue into km/hour
        /// </summary>
        /// <returns>A typed km/h value with integer km/hour</returns>
        public KmphValue ToKmphPrecise()
            => new((ushort)Mathf.Round(GameUnits * ApiConstants.SPEED_TO_KMPH));

        public static SpeedValue operator +(SpeedValue left, SpeedValue right)
            => new(left.GameUnits + right.GameUnits);

        public static bool operator ==(SpeedValue left, SpeedValue right) {
            return left.GameUnits.Equals(right.GameUnits);
        }

        public static bool operator !=(SpeedValue left, SpeedValue right) {
            return !left.GameUnits.Equals(right.GameUnits);
        }

        /// <summary>Scales the value by the multiplier.</summary>
        /// <param name="n">Scale.</param>
        public SpeedValue Scale(float n) {
            return new SpeedValue(this.GameUnits * n);
        }

        public float GetKmph() => this.GameUnits * ApiConstants.SPEED_TO_KMPH;

        public float GetMph() => this.GameUnits * ApiConstants.SPEED_TO_MPH;

        public string FormatStr(bool displayMph) {
            return displayMph
                       ? (int)this.GetMph() + " MPH"
                       : (int)this.GetKmph() + " km/h";
        }

        public bool Equals(SpeedValue other) {
            return GameUnits.Equals(other.GameUnits);
        }

        public override bool Equals(object obj) {
            return obj is SpeedValue other && Equals(other);
        }

        public override int GetHashCode() {
            return GameUnits.GetHashCode();
        }

        public override string ToString() {
            return this.FormatStr(false);
        }
    }
}