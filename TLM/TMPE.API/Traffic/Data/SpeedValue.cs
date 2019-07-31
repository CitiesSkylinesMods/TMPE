namespace TrafficManager.API.Traffic.Data {
    using System;

    /// <summary>
    /// Represents a speed value expressed in km/hour.
    /// </summary>
    [Serializable]
    public struct KmphValue {
        public float Kmph { get; }
    }

    /// <summary>
    /// Represents a speed value expressed in miles/hour.
    /// </summary>
    [Serializable]
    public struct MphValue {
        public float Mph { get; }
    }

    /// <summary>
    /// Represents a speed value expressed in game units where 1f = 50 km/h or 32 MPH.
    /// </summary>
    [Serializable]
    public struct SpeedValue {
        public SpeedValue(float gameSpeed) {
            GameSpeed = gameSpeed;
        }

        public float GameSpeed { get; }

        public static SpeedValue FromKmph(float f) {
            return new SpeedValue(f / ApiConstants.SPEED_TO_KMPH);
        }
    }
}
