namespace TrafficManager.U {
    using ColossalFramework.UI;
    using TrafficManager.State;
    using UnityEngine;

    public class UIScaler {
        private UIView uiView_;
        private Camera uiCamera_;

        public UIScaler(UIView uiView) {
            uiView_ = uiView;
            uiCamera_ = uiView.GetComponent<Camera>();
        }

        /// <summary>Screen width for GUI is always fixed at 1920.</summary>
        public float GuiWidth => uiCamera_.pixelWidth;

        /// <summary>Screen height for GUI is always fixed at 1080.</summary>
        public float GuiHeight => uiCamera_.pixelHeight;

        /// <summary>Calculate size based on screen width fraction.</summary>
        /// <param name="fraction">Fraction.</param>
        /// <returns>Value scaled to screen width.</returns>
        public float ScreenWidthFraction(float fraction) {
            return fraction * GuiWidth;
        }

        /// <summary>Calculate size based on screen height fraction.</summary>
        /// <param name="fraction">Fraction.</param>
        /// <returns>Value scaled to screen height.</returns>
        public float ScreenHeightFraction(float fraction) {
            return fraction * GuiHeight;
        }

        /// <summary>Based on screen width and screen height, pick the lesser fraction.</summary>
        /// <param name="widthFrac">Fraction of screen width.</param>
        /// <param name="heightFrac">Fraction of screen height.</param>
        /// <returns>Smallest of them two.</returns>
        public float ScreenSizeSmallestFraction(float widthFrac, float heightFrac) {
            return Mathf.Min(
                ScreenWidthFraction(widthFrac),
                ScreenHeightFraction(heightFrac));
        }

        /// <summary>
        /// Calculate UI scale based on GUI scale slider in options multiplied by uiView's scale.
        /// </summary>
        /// <returns>UI scale combined.</returns>
        public float GetScale() {
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