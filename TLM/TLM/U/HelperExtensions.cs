namespace TrafficManager.U {
    using ColossalFramework.UI;
    using UnityEngine;

    public static class HelperExtensions {
        /// <summary>
        /// Get rectangle of a control in GUI screen space.
        /// GUI screen space is used to draw overlay textures in overlay helpers.
        /// </summary>
        public static Rect GetScreenRectInGuiSpace(this UIComponent ctrl) {
            Vector2 pos = UIScaler.ScreenPointToGuiPoint(ctrl.absolutePosition);
            Vector2 size = UIScaler.ScreenPointToGuiPoint(ctrl.size);
            return new Rect(pos, size);
        }
    }
}