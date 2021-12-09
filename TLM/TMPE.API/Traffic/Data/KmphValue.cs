namespace TrafficManager.API.Traffic.Data {
    using System;

    /// <summary>
    /// Represents a speed value expressed in km/hour.
    /// </summary>
    [Serializable]
    public readonly struct KmphValue {
        public KmphValue(ushort kmph) {
            Kmph = kmph;
        }

        public override string ToString() => $"{Kmph:0.0} km/h";
        public string ToIntegerString() => $"{Kmph} km/h";

        public ushort Kmph { get; }

        /// <returns>A new KmphValue increased by right</returns>
        public static KmphValue operator +(KmphValue left, ushort right)
            => new((ushort)(left.Kmph + right));
    }
}