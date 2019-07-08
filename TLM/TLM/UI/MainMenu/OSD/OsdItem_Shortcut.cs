namespace TrafficManager.UI.MainMenu.OSD {
    using ColossalFramework.UI;
    using State.Keybinds;
    using UnityEngine;

    public class OsdItem_Shortcut : OsdItem {
        private readonly string text_;
        private readonly KeybindSetting keySetting_;

        public OsdItem_Shortcut(string text, KeybindSetting keySetting) {
            text_ = text;
            keySetting_ = keySetting;
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
            label.textColor = OnScreenDisplayPanel.PALETTE_SHORTCUT_TEXT;
            label.text = text_;
            label.autoHeight = true;
            label.relativePosition = position;
            return label;
        }

        private UILabel AddShortcutText(UIPanel parent, Vector2 position) {
            var keyLabel = parent.AddUIComponent<UILabel>();
            keyLabel.backgroundSprite = string.Empty;
            keyLabel.backgroundSprite = "GenericPanelDark";
            keyLabel.textColor = OnScreenDisplayPanel.PALETTE_SHORTCUT;
            keyLabel.text = $" {keySetting_.ToLocalizedString()} ";
            // TODO not sure if height is set properly
            keyLabel.height = OnScreenDisplayPanel.LABEL_HEIGHT + (2 * OnScreenDisplayPanel.PADDING);
            keyLabel.relativePosition = position;
            return keyLabel;
        }
    }
}