namespace CSUtil.Commons {
    using UnityEngine;

    public static class VectorUtil {
        /// <summary>
        /// Given a rectangle and screen resolution, pushes the rectangle back into screen space
        /// if it was moved beyond any screen edge.
        /// </summary>
        /// <param name="rect">The subject rectangle</param>
        /// <param name="resolution">The limiting rectangle from (0, 0) to resolution</param>
        public static void ClampRectToScreen(ref Rect rect, Vector2 resolution) {
            rect.x = Mathf.Clamp(rect.x, 0f, resolution.x - rect.width);
            rect.y = Mathf.Clamp(rect.y, 0f, resolution.y - rect.height);
        }
    }
}