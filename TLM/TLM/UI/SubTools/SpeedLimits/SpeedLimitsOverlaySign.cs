namespace TrafficManager.UI.SubTools.SpeedLimits {
    using System.Collections.Generic;
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
        private float size_;
        private Vector3 screenPos_;
        private Rect screenRect_;

        /// <summary>For each new sign world position, recalculate new rect for rendering.</summary>
        /// <param name="screenPos">Sign position projected to screen.</param>
        /// <param name="size">Visible large sign size.</param>
        public void Reset(Vector3 screenPos, Vector2 size) {
            this.screenPos_ = screenPos;

            this.screenRect_ = new Rect(
                x: screenPos.x - (size.x * 0.5f),
                y: screenPos.y - (size.y * 0.5f),
                width: size.x,
                height: size.y);
        }

        /// <summary>Draw large rect with the speed value or unlimited.</summary>
        /// <param name="speedlimit">Show this speed.</param>
        public void DrawLargeTexture(SpeedValue? speedlimit,
                                     IDictionary<int, Texture2D> textureSource) {
            Texture2D tex = speedlimit.HasValue
                ? SpeedLimitTextures.GetSpeedLimitTexture(speedlimit.Value, textureSource)
                : textureSource[0];

            GUI.DrawTexture(
                position: this.screenRect_,
                image: tex);
        }

        public bool ContainsMouse() {
            return TrafficManagerTool.IsMouseOver(this.screenRect_);
        }

        /// <summary>
        /// Draws the small texture in the corner. Size is passed here again, because we could be
        /// drawing a combination of rectangular US sign and round default speed sign.
        /// </summary>
        /// <param name="speedlimit">Show this.</param>
        /// <param name="smallSize">Size of small rect.</param>
        /// <param name="textureSource">Texture collection to use.</param>
        public void DrawSmallTexture(SpeedValue? speedlimit,
                                     Vector2 smallSize,
                                     IDictionary<int, Texture2D> textureSource) {
            // Offset the drawing center to the bottom right quarter of the large rect
            Vector3 drawCenter = this.screenPos_ + new Vector3(this.size_ * 0.25f,
                                                               this.size_ * 0.25f);
            Rect smallRect = new Rect(
                x: drawCenter.x,
                y: drawCenter.y,
                width: smallSize.x,
                height: smallSize.y);

            Texture2D tex = speedlimit.HasValue
                ? SpeedLimitTextures.GetSpeedLimitTexture(speedlimit.Value, textureSource)
                : textureSource[0];

            GUI.DrawTexture(
                position: smallRect,
                image: tex);
        }
    }
}