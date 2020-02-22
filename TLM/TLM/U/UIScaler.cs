namespace TrafficManager.U {
    using TrafficManager.State;
    using UnityEngine;

    public static class UIScaler {
        /// <summary>Calculate size based on screen width fraction.</summary>
        /// <param name="fraction">Fraction.</param>
        /// <returns>Value scaled to screen width.</returns>
        public static float ScreenWidthFraction(float fraction) {
            return fraction * Screen.width;
        }

        /// <summary>Calculate size based on screen height fraction.</summary>
        /// <param name="fraction">Fraction.</param>
        /// <returns>Value scaled to screen height.</returns>
        public static float ScreenHeightFraction(float fraction) {
            return fraction * Screen.height;
        }

        /// <summary>Based on screen width and screen height, pick the lesser fraction.</summary>
        /// <param name="widthFrac">Fraction of screen width.</param>
        /// <param name="heightFrac">Fraction of screen height.</param>
        /// <returns>Smallest of them two.</returns>
        public static float ScreenSizeSmallestFraction(float widthFrac, float heightFrac) {
            return Mathf.Min(
                ScreenWidthFraction(widthFrac),
                ScreenHeightFraction(heightFrac));
        }

        public static float GetUIScale() {
            return GlobalConfig.Instance.Main.GuiScale * 0.01f;
        }
    }
}