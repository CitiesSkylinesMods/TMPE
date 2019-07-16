namespace CSUtil.Commons {
    using UnityEngine;

    public static class VectorUtil {
        public static void ClampRectToScreen(ref Rect rect, Vector2 resolution) {
            Log._Debug($"ClampPosToScreen([{rect.x}, {rect.y}, {rect.xMax}, {rect.yMax}], " +
                       $"[{resolution.x}, {resolution.y}]) called");
            if (rect.x < 0) {
                rect.x = 0;
            }

            if (rect.y < 0) {
                rect.y = 0;
            }

            if (rect.xMax >= resolution.x) {
                rect.x = resolution.x - rect.width;
            }

            if (rect.yMax >= resolution.y) {
                rect.y = resolution.y - rect.height;
            }

            Log._Debug($"ClampPosToScreen() -> [{rect.x}, {rect.y}, {rect.xMax}, {rect.yMax}]");
        }
    }
}