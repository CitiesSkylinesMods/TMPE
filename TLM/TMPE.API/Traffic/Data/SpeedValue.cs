namespace TrafficManager.API.Traffic.Data {
    using System;
    using UnityEngine;

    /// <summary>
    /// Represents a speed value expressed in km/hour.
    /// </summary>
    [Serializable]
    public struct KmphValue {
        public KmphValue(ushort kmph) {
            Kmph = kmph;
        }

        public override string ToString() {
            return $"{Kmph:0.0} km/h";
        }

        public ushort Kmph { get; set; }
    }

    /// <summary>
    /// Represents a speed value expressed in miles/hour.
    /// </summary>
    [Serializable]
    public struct MphValue {
        public MphValue(ushort mph) {
            Mph = mph;
        }

        public override string ToString() {
            return $"{Mph} MPH";
        }

        public ushort Mph { get; set; }
    }

    /// <summary>
    /// Represents a speed value expressed in game units where 1f = 50 km/h or 32 MPH.
    /// </summary>
    [Serializable]
    public struct SpeedValue {
        public SpeedValue(float gameUnits) {
            GameUnits = gameUnits;
        }

        public float GameUnits { get; set; }

        public float ToVelocity() {
            return GameUnits * ApiConstants.SPEED_TO_VELOCITY;
        }

        public static SpeedValue FromKmph(ushort kmph) {
            return new SpeedValue(kmph / ApiConstants.SPEED_TO_KMPH);
        }

        public static SpeedValue FromVelocity(float vel) {
            return new SpeedValue(vel / ApiConstants.SPEED_TO_VELOCITY);
        }

        public static SpeedValue FromKmph(KmphValue kmph) {
            return new SpeedValue(kmph.Kmph / ApiConstants.SPEED_TO_KMPH);
        }

        public static SpeedValue operator -(SpeedValue x, SpeedValue y) {
            return new SpeedValue(x.GameUnits - y.GameUnits);
        }

        public static SpeedValue FromMph(ushort mph) {
            return new SpeedValue(mph / ApiConstants.SPEED_TO_MPH);
        }

        public static SpeedValue FromMph(MphValue mph) {
            return new SpeedValue(mph.Mph / ApiConstants.SPEED_TO_MPH);
        }

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

        public MphValue ToMphPrecise() {
            return new MphValue((ushort)Mathf.Round(GameUnits * ApiConstants.SPEED_TO_MPH));
        }

        public KmphValue ToKmphPrecise() {
            return new KmphValue((ushort)Mathf.Round(GameUnits * ApiConstants.SPEED_TO_KMPH));
        }
    }
}
