namespace TrafficManager.Util {
    using UnityEngine;

    internal static class TMPEVectorUtil {
        internal static Vector3 RotateXZ90CW(this Vector3 v) =>
            new Vector3(v.z, v.y, -v.x);
        internal static Vector3 RotateXZ90CCW(this Vector3 v) =>
            new Vector3(-v.z, v.y, v.x);

        /// <summary>
        /// rotate counter clockwise
        /// </summary>
        /// <param name="angle">in radians</param>
        internal static Vector3 RotateXZ(this Vector3 v, float angle) {
            var cos = Mathf.Cos(angle);
            var sin = Mathf.Sin(angle);
            return new Vector3 {
                x = v.x * cos - v.z * sin,
                y = v.y,
                z = v.x * sin + v.z * cos,
            };
        }
    }
}
