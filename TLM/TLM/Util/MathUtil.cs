using ColossalFramework.Math;

namespace TrafficManager.Util {
    using JetBrains.Annotations;

    // Not used
    [UsedImplicitly]
    public static class MathUtil {
        public static float RandomizeFloat(Randomizer rng, float lower, float upper) {
            return ((float)rng.UInt32(0, 10001) / 10000f) * (upper - lower) + lower;
        }
    }
}