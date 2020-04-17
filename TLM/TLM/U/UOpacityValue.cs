namespace TrafficManager.U {
    using JetBrains.Annotations;
    using UnityEngine;

    /// <summary>
    /// Allows storing same value as both opacity and transparency and avoid confusion.
    /// </summary>
    public struct UOpacityValue {
        private float value_;

        /// <summary>Create from opacity value where 1f is opaque, 0f is invisible.</summary>
        /// <param name="v">Range 0..1f.</param>
        /// <returns>New Opacity struct.</returns>
        public static UOpacityValue FromOpacity(float v) {
            return new UOpacityValue {
                                    value_ = Mathf.Clamp(value: v, min: 0f, max: 1f),
                                };
        }

        /// <summary>Create from transparency value where 0 is opaque, 1f is invisible.</summary>
        /// <param name="v">Range 0..1f.</param>
        /// <returns>New Opacity struct.</returns>
        [UsedImplicitly]
        public static UOpacityValue FromTransparency(float v) {
            return new UOpacityValue {
                                    value_ = 1f - Mathf.Clamp(value: v, min: 0f, max: 1f),
                                };
        }

        [UsedImplicitly]
        public float GetOpacity() => this.value_;

        [UsedImplicitly]
        public float GetTransparency() => 1f - this.value_;

        public byte GetOpacityByte() => (byte)(this.value_ * 255f);
    }
}