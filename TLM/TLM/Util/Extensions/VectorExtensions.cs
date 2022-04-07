namespace TrafficManager.Util.Extensions {
    using UnityEngine;

    public static class VectorExtensions {
        /// <summary>Check if a world position is visible on screen.</summary>
        /// <param name="worldPos">The world point to convert.</param>
        /// <param name="screenPos">The calculated screen point.</param>
        /// <returns>Returns <c>true</c> if <paramref name="worldPos"/> is in camera view.</returns>
        /// <remarks>Use only in game/editor.</remarks>
        public static bool IsOnScreen(this Vector3 worldPos, out Vector3 screenPos) {
            screenPos = InGameUtil.Instance.CachedMainCamera.WorldToScreenPoint(worldPos);
            screenPos.y = Screen.height - screenPos.y;

            return screenPos.z >= 0;
        }
    }
}
