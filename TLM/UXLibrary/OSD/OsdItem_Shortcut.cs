using ColossalFramework.UI;
using UnityEngine;
using UXLibrary.Keyboard;

namespace UXLibrary.OSD {
    public class OsdItemShortcut : OsdItem {
        private readonly string text_;
        private readonly string keybind_;

        public OsdItemShortcut(string text, string keybind) {
            text_ = text;
            keybind_ = keybind;
        }

        public override Vector2 AddTo(UIPanel parent, Vector2 position) {
            position.x += OnScreenDisplayPanel.PADDING;

            var keyLabel = AddShortcutText(parent, position);
            position.x += keyLabel.width + OnScreenDisplayPanel.PADDING;

            var label = AddShortcutDescriptionText(parent, position);
            position.x += label.width + OnScreenDisplayPanel.PADDING;

            return position;
        }

        private UILabel AddShortcutDescriptionText(UIPanel parent, Vector2 position) {
            var label = parent.AddUIComponent<UILabel>();
            label.textColor = OnScreenDisplayPanel.PaletteShortcutText;
            label.text = text_;
            label.autoHeight = true;
            label.relativePosition = position;
            return label;
        }

        private UILabel AddShortcutText(UIPanel parent, Vector2 position) {
            var keyLabel = parent.AddUIComponent<UILabel>();
            keyLabel.backgroundSprite = string.Empty;
            keyLabel.backgroundSprite = "GenericPanelDark";
            keyLabel.textColor = OnScreenDisplayPanel.PaletteShortcut;
            keyLabel.text = $" {keybind_} ";
            // TODO not sure if height is set properly
            keyLabel.height = OnScreenDisplayPanel.LABEL_HEIGHT + (2 * OnScreenDisplayPanel.PADDING);
            keyLabel.relativePosition = position;
            return keyLabel;
        }
    }
}