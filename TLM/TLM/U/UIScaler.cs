namespace TrafficManager.U {
    using ColossalFramework;
    using ColossalFramework.UI;
    using TrafficManager.State;
    using UnityEngine;

    public static class UIScaler {
        public const float GUI_WIDTH = 1920f;
        public const float GUI_HEIGHT = 1080f;

        /// <summary>Caching because UIView.Instance.uiCamera can be null sometimes.</summary>
        private static float cachedGuiWidth = GUI_WIDTH;

        /// <summary>Caching because UIView.Instance.uiCamera can be null sometimes.</summary>
        private static float cachedGuiHeight = GUI_HEIGHT;

        /// <summary>Screen width for GUI is always fixed at 1920.</summary>
        public static float GuiWidth {
            // TODO: Double check if GUI never changes width, the code below can be a const
            get {
                UIView uiView = UIView.GetAView();
                if (uiView != null) {
                    UIScaler.cachedGuiWidth = uiView.uiCamera.pixelWidth;
                }

                return UIScaler.cachedGuiWidth;
            }
        }

        /// <summary>Screen height for GUI is always fixed at 1080.</summary>
        public static float GuiHeight {
            // TODO: Double check if GUI never changes height, the code below can be a const
            get {
                UIView uiView = UIView.GetAView();
                if (uiView != null) {
                    UIScaler.cachedGuiHeight = uiView.uiCamera.pixelHeight;
                }

                return UIScaler.cachedGuiHeight;
            }
        }

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
                (screenPos.x * GUI_WIDTH) / Screen.width,
                (screenPos.y * GUI_HEIGHT) / Screen.height);
        }
    }
}