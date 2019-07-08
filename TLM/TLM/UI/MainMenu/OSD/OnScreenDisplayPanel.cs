namespace TrafficManager.UI.MainMenu.OSD {
    using System.Collections.Generic;
    using ColossalFramework.UI;
    using State.Keybinds;
    using UnityEngine;

    public class OnScreenDisplayPanel {
        public class Configurator {
            private OnScreenDisplayPanel panel_;

            public Configurator(OnScreenDisplayPanel panel) {
                panel_ = panel;
                panel_.items_.Clear();
            }

            public Configurator Title(string text) {
                panel_.Title = text;
                return this;
            }

            public Configurator Shortcut(string text, KeybindSetting setting) {
                panel_.items_.Add(new OsdItem_Shortcut(text, setting));
                return this;
            }

            public void Show() {
                panel_.Update();
                panel_ = null;
            }
        }

        public string Title { get; set; }

        /// <summary>
        /// White. Text color for simple messages.
        /// </summary>
        public static readonly Color PALETTE_TEXT = new Color(1f, 1f, 1f, 1f);

        /// <summary>
        /// Silver gray. Text color for shortcut description.
        /// </summary>
        public static readonly Color PALETTE_SHORTCUT_TEXT = new Color(.75f, .75f, .75f, 1f);

        /// <summary>
        /// Sand yellow. Shortcut text.
        /// </summary>
        public static readonly Color PALETTE_SHORTCUT = new Color(.8f, .6f, .3f, 1f);
        // public static readonly Color PALETTE_SHORTCUT = Color.black;

        /// <summary>
        /// Text line with 8px paddings above and below
        /// </summary>
        public const float LABEL_HEIGHT = 16f;

        /// <summary>
        /// 2 text lines with 8px paddings above, below and between
        /// </summary>
        public const float PANEL_HEIGHT = (LABEL_HEIGHT * 2) + (3 * PADDING);

        /// <summary>
        /// Distance between panel elements (2x PADDING)
        /// </summary>
        public const float PADDING = 8f;

        private readonly UIPanel thisPanel_;

        private readonly List<OsdItem> items_;

        /// <summary>
        /// Initializes a new instance of the <see cref="OnScreenDisplayPanel"/> class.
        /// Constructs the empty OSD panel and hides it.
        /// </summary>
        /// <param name="mainPanel">The parent panel to attach to</param>
        public OnScreenDisplayPanel(UIPanel mainPanel) {
            items_ = new List<OsdItem>();

            thisPanel_ = mainPanel.AddUIComponent<UIPanel>();
            thisPanel_.width = 10f;
            thisPanel_.height = PANEL_HEIGHT;
            thisPanel_.backgroundSprite = "GenericPanel";
            thisPanel_.color = new Color32(64, 64, 64, 240);
            Update();
        }

        public Configurator Setup() {
            return new Configurator(this);
        }

        public void Clear() {
            items_.Clear();
            Update();
        }

        public void Update() {
            UpdatePosition();
            thisPanel_.isVisible = items_.Count > 0;
            UpdatePanelItems();
        }

        private void UpdatePanelItems() {
            ClearPanelItems();

            var titleLabel = thisPanel_.AddUIComponent<UILabel>();
            titleLabel.textColor = PALETTE_TEXT;
            titleLabel.text = Title;
            titleLabel.relativePosition = new Vector3(PADDING, PADDING);

            // Add items to the panel. Resize the panel to fit everything.
            var position = new Vector2(0, LABEL_HEIGHT + (PADDING * 2));

            foreach (var item in items_) {
                position = item.AddTo(thisPanel_, position);
            }

            thisPanel_.width = Mathf.Max(position.x, titleLabel.width + 2 * PADDING);
        }

        private void ClearPanelItems() {
            foreach (var c in thisPanel_.components) {
                UnityEngine.Object.Destroy(c);
            }
        }

        public void UpdatePosition() {
            var parent = (UIPanel) thisPanel_.parent;
            if (parent.relativePosition.y < Screen.height / 2f) {
                // Upper part of the screen, place below the TM:PE panel, with 1px margin
                thisPanel_.relativePosition = new Vector3(0f, parent.height + 1f, 0f);
            } else {
                // Lower part of the screen, place above the TM:PE panel, with 1px margin
                thisPanel_.relativePosition = new Vector3(0f, -thisPanel_.height - 1f, 0f);
            }
        }
    }
}