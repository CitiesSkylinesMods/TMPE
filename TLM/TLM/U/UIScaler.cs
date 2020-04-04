namespace TrafficManager.U {
    using ColossalFramework.UI;
    using TrafficManager.State;
    using UnityEngine;

    public class UIScaler {
        private UIView uiView_;

        public UIScaler(UIView uiView) {
            uiView_ = uiView;
        }

        /// <summary>Calculate size based on screen width fraction.</summary>
        /// <param name="fraction">Fraction.</param>
        /// <returns>Value scaled to screen width.</returns>
        public float ScreenWidthFraction(float fraction) {
            return fraction * uiView_.fixedWidth;
        }

        /// <summary>Calculate size based on screen height fraction.</summary>
        /// <param name="fraction">Fraction.</param>
        /// <returns>Value scaled to screen height.</returns>
        public float ScreenHeightFraction(float fraction) {
            return fraction * uiView_.fixedHeight;
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
            return GlobalConfig.Instance.Main.GuiScale * 0.01f * uiView_.scale;
        }
    }
}