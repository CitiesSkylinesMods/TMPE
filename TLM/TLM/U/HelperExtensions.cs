namespace TrafficManager.U {
    using ColossalFramework.UI;
    using UnityEngine;

    public static class HelperExtensions {
        /// <summary>
        /// Get rectangle of a control in GUI screen space.
        /// GUI screen space is used to draw overlay textures in overlay helpers.
        /// </summary>
        public static Rect GetScreenRectInGuiSpace(this UIComponent ctrl) {
            Vector3 absPosition = ctrl.absolutePosition;
            float scaleX = Screen.width / 1920f;
            float scaleY = Screen.height / 1080f;
            return new Rect(
                absPosition.x * scaleX,
                absPosition.y * scaleX,
                ctrl.width * scaleY,
                ctrl.height * scaleY);
            // var b = ctrl.GetBounds();
            // return new Rect(
            //     (Screen.width * 0.5f) + (b.min.x * Screen.width),
            //     (Screen.height * 0.5f) - (b.min.y * Screen.height),
            //     b.extents.x * Screen.width,
            //     b.extents.y * Screen.height);
        }
    }
}