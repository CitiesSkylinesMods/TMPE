namespace TrafficManager.UI.SubTools.SpeedLimits {
    using TrafficManager.API.Traffic.Data;
    using TrafficManager.UI.Textures;
    using UnityEngine;

    /// <summary>
    /// Combo sign object rendered as GUI overlay.
    /// Consists of a large speed limit icon using current theme, and a small optional icon
    /// in the corner.
    /// </summary>
    // +-----+
    // |     |
    // | 9 0 |
    // |   50|
    // +-----+
    public struct SpeedLimitsOverlaySign {
        const float SMALL_ICON_SCALE = 0.5f;

        /// <summary>For rectangular US signs, this is set to a non 1.0f value.</summary>
        private readonly float verticalScale_;

        private float size_;
        private Vector3 screenPos_;
        private Rect screenRect_;


        public SpeedLimitsOverlaySign(float verticalScale) : this() {
            verticalScale_ = verticalScale;
        }

        /// <summary>For each new sign world position, recalculate new rect for rendering.</summary>
        /// <param name="screenPos">Sign position projected to screen.</param>
        /// <param name="size">Visible sign size.</param>
        public void Reset(Vector3 screenPos, float size) {
            this.size_ = size;
            this.screenPos_ = screenPos;

            this.screenRect_ = new Rect(
                x: screenPos.x - (size * 0.5f),
                y: screenPos.y - (size * 0.5f),
                width: size,
                height: size * verticalScale_);
        }

        /// <summary>Draw large rect with the speed value or unlimited.</summary>
        /// <param name="speedlimit">Show this speed.</param>
        public void DrawLargeTexture(SpeedValue? speedlimit) {
            Texture2D tex = speedlimit.HasValue
                ? SpeedLimitTextures.GetSpeedLimitTexture(speedlimit.Value)
                : SpeedLimitTextures.TexturesKmph[0];

            GUI.DrawTexture(
                position: this.screenRect_,
                image: tex);
        }

        public bool ContainsMouse() {
            return TrafficManagerTool.IsMouseOver(this.screenRect_);
        }

        public void DrawSmallTexture(SpeedValue? speedlimit) {
            float smallSize = this.size_ * SMALL_ICON_SCALE;

            // Offset the drawing center to the bottom right quarter of the large rect
            Vector3 drawCenter = this.screenPos_ + new Vector3(this.size_ * 0.25f,
                                                               this.size_ * 0.25f);
            Rect smallRect = new Rect(
                x: drawCenter.x - (smallSize * 0.5f),
                y: drawCenter.y - (smallSize * 0.5f),
                width: smallSize,
                height: smallSize * verticalScale_);

            Texture2D tex = speedlimit.HasValue
                ? SpeedLimitTextures.GetSpeedLimitTexture(speedlimit.Value)
                : SpeedLimitTextures.TexturesKmph[0];

            GUI.DrawTexture(
                position: smallRect,
                image: tex);
        }
    }
}