namespace TrafficManager.API.Traffic.Data {
    using System;
    using JetBrains.Annotations;
    using UnityEngine;

    /// <summary>
    /// Represents a speed value expressed in game units where 1f = 50 km/h or 32 MPH.
    /// </summary>
    [Serializable]
    public readonly struct SpeedValue {
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
        /// <returns></returns>
        [UsedImplicitly]
        public float ToVelocity() => GameUnits * ApiConstants.SPEED_TO_VELOCITY;

        /// <summary>
        /// Constructs a SpeedValue from velocity magnitude (8x the game units)
        /// </summary>
        /// <param name="vel">The velocity value originating from a vehicle velocity</param>
        /// <returns>A new SpeedValue downscaled to game units</returns>
        public static SpeedValue FromVelocity(float vel)
            => new SpeedValue(vel / ApiConstants.SPEED_TO_VELOCITY);

        /// <summary>
        /// Constructs a SpeedValue from km/hour given as an integer
        /// </summary>
        /// <param name="kmph">A speed in kilometres/hour</param>
        /// <returns>A new speedvalue converted to the game units</returns>
        public static SpeedValue FromKmph(ushort kmph)
            => new SpeedValue(kmph / ApiConstants.SPEED_TO_KMPH);

        /// <summary>
        /// Constructs a speed value from km/hour given as a KmphValue
        /// </summary>
        /// <param name="kmph">Km/hour typed value</param>
        /// <returns>A new SpeedValue scaled to the game units</returns>
        public static SpeedValue FromKmph(KmphValue kmph)
            => new SpeedValue(kmph.Kmph / ApiConstants.SPEED_TO_KMPH);

        /// <summary>
        /// Constructs a SpeedValue from miles/hour given as an integer
        /// </summary>
        /// <param name="mph">A speed in miles/hour</param>
        /// <returns>A new speedvalue converted to the game units</returns>
        public static SpeedValue FromMph(ushort mph)
            => new SpeedValue(mph / ApiConstants.SPEED_TO_MPH);

        /// <summary>
        /// Constructs a speed value from miles/hour given as a MphValue
        /// </summary>
        /// <param name="mph">Miles/hour typed value</param>
        /// <returns>A new SpeedValue scaled to the game units</returns>
        public static SpeedValue FromMph(MphValue mph)
            => new SpeedValue(mph.Mph / ApiConstants.SPEED_TO_MPH);

        /// <summary>
        /// Subtracts two speed values
        /// </summary>
        /// <param name="x">First value</param>
        /// <param name="y">Second value</param>
        /// <returns>A new SpeedValue which is a difference between x and y</returns>
        public static SpeedValue operator -(SpeedValue x, SpeedValue y)
            => new SpeedValue(x.GameUnits - y.GameUnits);

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
            => new MphValue((ushort)Mathf.Round(GameUnits * ApiConstants.SPEED_TO_MPH));

        /// <summary>
        /// Converts this SpeedValue into km/hour
        /// </summary>
        /// <returns>A typed km/h value with integer km/hour</returns>
        public KmphValue ToKmphPrecise()
            => new KmphValue((ushort)Mathf.Round(GameUnits * ApiConstants.SPEED_TO_KMPH));
    }

    /// <summary>
    /// Represents a speed value expressed in km/hour.
    /// </summary>
    [Serializable]
    public readonly struct KmphValue {
        public KmphValue(ushort kmph) {
            Kmph = kmph;
        }

        public override string ToString() => $"{Kmph:0.0} km/h";

        public ushort Kmph { get; }

        /// <returns>A new KmphValue increased by right</returns>
        public static KmphValue operator +(KmphValue left, ushort right)
            => new KmphValue((ushort)(left.Kmph + right));
    }

    /// <summary>
    /// Represents a speed value expressed in miles/hour.
    /// </summary>
    [Serializable]
    public readonly struct MphValue {
        public MphValue(ushort mph) {
            Mph = mph;
        }

        public override string ToString() => $"{Mph} MPH";

        public ushort Mph { get; }

        /// <returns>A new MphValue increased by right</returns>
        public static MphValue operator +(MphValue left, ushort right)
            => new MphValue((ushort)(left.Mph + right));
    }
}
