namespace TrafficManager.Util.Extensions {
using UnityEngine;

    public static class CameraInfoExtensions {

        /// <summary>
        /// Check if <paramref name="point"/> is within <paramref name="maxDistance"/>
        /// of <paramref name="cameraInfo"/>.
        /// </summary>
        /// <param name="cameraInfo">Camera info.</param>
        /// <param name="point">Point to check in relation to camera.</param>
        /// <param name="maxDistance">Maximum allowed distance.</param>
        /// <returns>Returns <c>true</c> if within range.</returns>
        /// <remarks>Based on <see href="https://github.com/Quistar-LAB/EManagersLib/blob/master/EMath.cs"/>.</remarks>
        public static bool ECheckRenderDistance(this RenderManager.CameraInfo cameraInfo, Vector3 point, float maxDistance) {
            float distance = maxDistance * 0.45f;
            Vector3 campos = cameraInfo.m_position;
            Vector3 camforward = cameraInfo.m_forward;
            float x = point.x - campos.x - camforward.x * distance;
            float y = point.y - campos.y - camforward.y * distance;
            float z = point.z - campos.z - camforward.z * distance;
            return (x * x + y * y + z * z) < maxDistance * maxDistance * 0.3025f;
        }
    }
}
