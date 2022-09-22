namespace TrafficManager.UI.SubTools.SpeedLimits.Overlay {
    using TrafficManager.API.Traffic.Data;
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
                                     RoadSignTheme theme,
                                     bool disabled = false) {
            Texture2D tex = speedlimit.HasValue
                                ? theme.SpeedLimitTexture(speedlimit.Value, disabled)
                                : RoadSignThemeManager.Instance.NoOverride;

            GUI.DrawTexture(
                position: this.screenRect_,
                image: tex);
        }

        // public void DrawLargeTexture(Texture2D tex) {
        //     GUI.DrawTexture(
        //         position: this.screenRect_,
        //         image: tex);
        // }

        private static Texture2D ChooseTexture(SpeedValue? speedlimit,
                                               RoadSignTheme theme) {
            return speedlimit.HasValue
                       ? theme.SpeedLimitTexture(speedlimit.Value)
                       : RoadSignThemeManager.Instance.NoOverride;
        }

        /// <summary>Draws the small texture in the Bottom-Right corner.</summary>
        /// <param name="tex">Show this.</param>
        public void DrawSmallTexture_BottomRight(Texture2D tex) {
            // Offset the drawing center to the bottom right quarter of the large rect
            // The sign is drawn from the screen position (center) and must be half size of big rect
            Rect smallRect = new Rect(
                x: this.screenPos_.x,
                y: this.screenPos_.y,
                width: this.screenRect_.width * 0.5f,
                height: this.screenRect_.height * 0.5f);

            GUI.DrawTexture(
                position: smallRect,
                image: tex);
        }

        /// <summary>
        /// Used to draw default speed sign subicon overlapping bottom right corner.
        /// </summary>
        /// <param name="speed">The default speed value.</param>
        public void DrawDefaultSpeedSubIcon(SpeedValue speed) {
            Texture2D tex = SignRenderer.ChooseTexture(
                speedlimit: speed,
                theme: RoadSignThemeManager.Instance.SpeedLimitDefaults);

            float size = this.screenRect_.height * 0.4f;
            float half = size / 2;

            Rect smallRect = new Rect(
                x: this.screenPos_.x + half,
                y: this.screenPos_.y + half,
                width: size,
                height: size);

            GUI.DrawTexture(
                position: smallRect,
                image: tex);
        }

        /// <summary>Draws the small texture in the Top-Left corner.</summary>
        /// <param name="tex">Show this.</param>
        public void DrawSmallTexture_TopLeft(Texture2D tex) {
            // Offset the drawing center to the bottom right quarter of the large rect
            // The sign is drawn from the screen position (center) and must be half size of big rect
            Rect smallRect = new Rect(
                x: this.screenRect_.x,
                y: this.screenRect_.y,
                width: this.screenRect_.width * 0.5f,
                height: this.screenRect_.height * 0.5f);

            GUI.DrawTexture(
                position: smallRect,
                image: tex);
        }
    }
}