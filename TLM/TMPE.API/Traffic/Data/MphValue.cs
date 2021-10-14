namespace TrafficManager.API.Traffic.Data {
    using System;

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
            => new ((ushort)(left.Mph + right));
    }
}