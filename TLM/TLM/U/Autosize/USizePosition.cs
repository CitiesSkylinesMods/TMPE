namespace TrafficManager.U.Autosize {
    using UnityEngine;

    /// <summary>Defines sizing and spacing for the control.</summary>
    public class USizePosition {
        /// <summary>How the width will be calculated, using the <see cref="widthValue"/>.</summary>
        public USizeRule widthRule = USizeRule.FixedSize;

        public float widthValue = 16f;

        /// <summary>How the height will be calculated, using the <see cref="heightValue"/>.</summary>
        public USizeRule heightRule = USizeRule.FixedSize;

        public float heightValue = 16f;

        public static Vector2 GetReferenceSize(float pixelsAt1080p) {
            return new Vector2(pixelsAt1080p / 1920f, pixelsAt1080p / 1080f);
        }
    }
}