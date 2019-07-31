namespace TrafficManager.Util {
    using System;
    using UnityEngine;

    /// <summary>
    /// Provides static functions for handling floating point values.
    /// </summary>
    public class FloatUtil {
        /// <summary>
        /// A very small value for float comparisons to zero
        /// </summary>
        public const float VERY_SMALL_FLOAT = 1e-12f;

        /// <summary>
        /// Checks whether two floats are very close to each other
        /// </summary>
        /// <param name="a">One float</param>
        /// <param name="b">Another float</param>
        /// <returns>Are really closed</returns>
        public static bool NearlyEqual(float a, float b) {
            return Mathf.Abs(a - b) < VERY_SMALL_FLOAT;
        }

        public static bool IsZero(float speed) {
            return Math.Abs(speed) < VERY_SMALL_FLOAT;
        }
    }
}