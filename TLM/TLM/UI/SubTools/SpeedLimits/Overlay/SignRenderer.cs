namespace TrafficManager.UI.SubTools.SpeedLimits.Overlay {
    using System.Collections.Generic;
    using TrafficManager.API.Traffic.Data;
    using TrafficManager.Lifecycle;
    using TrafficManager.UI.Textures;
    using UnityEngine;

    /// <summary>
    /// Combo sign object rendered as GUI overlay.
    /// Consists of a large speed limit icon using current theme, and a small optional icon
    /// in the corner. Signs can have different aspect ratios (i.e. allows to combine US rectangular
    /// and round signs).
    /// </summary>
    // +-----+
    // |     |
    // | 9 0 |
    // |   50|
    // +-----+
    public struct SignRenderer {
        private Vector3 screenPos_;

        /// <summary>The visible screen-space box of the large texture (for mouse interaction).</summary>
        private Rect screenRect_;

        /// <summary>For each new sign world position, recalculate new rect for rendering.</summary>
        /// <param name="screenPos">Sign position projected to screen.</param>
        /// <param name="size">Visible large sign size.</param>
        public Rect Reset(Vector3 screenPos, Vector2 size) {
            this.screenPos_ = screenPos;

            this.screenRect_ = new Rect(
                x: screenPos.x - (size.x * 0.5f),
                y: screenPos.y - (size.y * 0.5f),
                width: size.x,
                height: size.y);
            return this.screenRect_; // use to check intersection with tool windows
        }

        public bool ContainsMouse(Vector2? mousePos) {
            return mousePos.HasValue && this.screenRect_.Contains(mousePos.Value);
        }

        /// <summary>Draw large rect with the speed value or unlimited.</summary>
        /// <param name="speedlimit">Show this speed.</param>
        public void DrawLargeTexture(SpeedValue? speedlimit,
                                     IDictionary<int, Texture2D> textureSource) {
            Texture2D tex = speedlimit.HasValue
                                ? SpeedLimitTextures.GetSpeedLimitTexture(speedlimit.Value, textureSource)
                                : SpeedLimitTextures.NoOverride;

            GUI.DrawTexture(
                position: this.screenRect_,
                image: tex);
        }

        public void DrawLargeTexture(Texture2D tex) {
            GUI.DrawTexture(
                position: this.screenRect_,
                image: tex);
        }

        /// <summary>
        /// Draws the small texture in the corner. Size is passed here again, because we could be
        /// drawing a combination of rectangular US sign and round default speed sign.
        /// </summary>
        /// <param name="speedlimit">Show this.</param>
        /// <param name="smallSize">Size of small rect.</param>
        /// <param name="textureSource">Texture collection to use.</param>
        public void DrawSmallTexture(SpeedValue? speedlimit,
                                     IDictionary<int, Texture2D> textureSource) {
            // Offset the drawing center to the bottom right quarter of the large rect
            // The sign is drawn from the screen position (center) and must be half size of big rect
            Rect smallRect = new Rect(
                x: this.screenPos_.x,
                y: this.screenPos_.y,
                width: this.screenRect_.width * 0.5f,
                height: this.screenRect_.height * 0.5f);

            Texture2D tex = speedlimit.HasValue
                                ? SpeedLimitTextures.GetSpeedLimitTexture(speedlimit.Value, textureSource)
                                : SpeedLimitTextures.NoOverride;

            GUI.DrawTexture(
                position: smallRect,
                image: tex);
        }
    }
}