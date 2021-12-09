namespace TrafficManager.UI.Helpers {
    using UnityEngine;

    /// <summary>
    /// Provides consistent coloring logic for graphic overlays rendered with GUI.DrawTexture.
    /// Usage:
    /// 1. Create color controller at the start of your overlay render.
    /// 2. Call .SetGUIColor() with mouse hover true or false.
    /// 3. In the end call .RestoreGUIColor()
    /// </summary>
    public readonly struct OverlayHandleColorController {
        private readonly bool isInteractable_;

        /// <summary>
        /// This color is used when drawing overlay clickable handles which are not disabled
        /// and have mouse hovering over them (i.e. can be interacted with).
        /// </summary>
        private static readonly Color MOUSE_HOVER_INTERACTABLE_COLOR = new(r: 1f, g: .7f, b: 0f);
        private static readonly Color NON_INTERACTABLE_COLOR = Color.gray;

        private readonly Color originalColor_;

        public OverlayHandleColorController(bool isInteractable) {
            this.isInteractable_ = isInteractable;
            this.originalColor_ = GUI.color;
        }

        /// <summary>
        /// Magical logic, coloring interactable GUI overlay signs with orange and making them semi
        /// or full opaque.
        /// </summary>
        /// <param name="hovered">Whether mouse is over the sign. Note: Interactive is set from
        /// the constructor.</param>
        /// <param name="intersectsGuiWindows">Whether rect enters area of some GUI window.</param>
        /// <param name="opacityMultiplier">Multiply alpha with this. Can use to fade signs with distance.</param>
        public void SetGUIColor(bool hovered,
                                bool intersectsGuiWindows,
                                float opacityMultiplier = 1f) {
            var tmpColor = this.originalColor_;

            if (intersectsGuiWindows) { // fade the sign to not block the GUI
                opacityMultiplier *= 0.4f;
            }

            if (this.isInteractable_) {
                tmpColor.a = TrafficManagerTool.GetHandleAlpha(hovered) * opacityMultiplier;
                if (hovered) {
                    tmpColor = Color.Lerp(tmpColor, MOUSE_HOVER_INTERACTABLE_COLOR, 0.5f);
                }
            } else {
                // Gray-ish color and non-hover transparency
                tmpColor.a = TrafficManagerTool.GetHandleAlpha(hovered: false) * opacityMultiplier;
                tmpColor = Color.Lerp(tmpColor, NON_INTERACTABLE_COLOR, 0.5f);
            }

            GUI.color = tmpColor;
        }

        /// <summary>Set GUI color like it was before. Probably white and full opacity.</summary>
        public void RestoreGUIColor() {
            GUI.color = this.originalColor_;
        }
    }
}