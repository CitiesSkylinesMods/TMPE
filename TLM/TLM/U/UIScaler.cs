namespace TrafficManager.U {
    using ColossalFramework;
    using ColossalFramework.UI;
    using TrafficManager.State;
    using UnityEngine;

    public static class UIScaler {
        /// <summary>Screen width for GUI is always fixed at 1920.</summary>
        public static float GuiWidth => Singleton<UIView>.instance.uiCamera.pixelWidth;

        /// <summary>Screen height for GUI is always fixed at 1080.</summary>
        public static float GuiHeight => Singleton<UIView>.instance.uiCamera.pixelHeight;

        /// <summary>
        /// Calculate UI scale based on GUI scale slider in options multiplied by uiView's scale.
        /// </summary>
        /// <returns>UI scale combined.</returns>
        public static float GetScale() {
            return GlobalConfig.Instance.Main.GuiScale * 0.01f;
        }

        /// <summary>
        /// Given a position on screen (unit: pixels) convert to GUI position (always 1920x1080).
        /// </summary>
        /// <param name="screenPos">Pixel position.</param>
        /// <returns>GUI space position.</returns>
        internal static Vector2 ScreenPointToGuiPoint(Vector2 screenPos) {
            return new Vector2(
                (screenPos.x * 1920f) / Screen.width,
                (screenPos.y * 1080f) / Screen.height);
        }
    }
}