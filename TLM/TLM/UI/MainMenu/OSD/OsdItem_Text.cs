namespace TrafficManager.UI.MainMenu.OSD {
    using ColossalFramework.UI;
    using UnityEngine;

    /// <summary>
    /// Represents a text label (without a keyboard shortcut)
    /// </summary>
    public class OsdItem_Text : OsdItem {
        private string text_;

        public OsdItem_Text(string text) {
            text_ = text;
        }

        public override Vector2 AddTo(UIPanel parent, Vector2 position) {
            var label = parent.AddUIComponent<UILabel>();
            label.textColor = OnScreenDisplayPanel.PALETTE_TEXT;
            label.text = text_;
            label.relativePosition = position;
            label.autoHeight = true;
            label.textAlignment = UIHorizontalAlignment.Left;

            position.x += label.width + (2 * OnScreenDisplayPanel.PADDING);

            return position;
        }
    }
}
