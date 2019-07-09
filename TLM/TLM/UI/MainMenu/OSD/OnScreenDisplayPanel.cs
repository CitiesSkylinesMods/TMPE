namespace TrafficManager.UI.MainMenu.OSD {
    using System.Collections.Generic;
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using State;
    using State.Keybinds;
    using UnityEngine;

    public class OnScreenDisplayPanel {
        public class Configurator {
            private OnScreenDisplayPanel panel_;

            public Configurator(OnScreenDisplayPanel panel) {
                panel_ = panel;
                panel_.Clear();
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
                panel_ = null; // end of work for the configurator object
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

        private readonly OsdUIPanel thisPanel_;

        public UIDragHandle Drag { get; private set; }

        private readonly List<OsdItem> items_;

        /// <summary>
        /// Stores panel visibility, separate variable because even visible
        /// panel is hidden when there's no active tool.
        /// </summary>
        private bool osdVisible_;

        /// <summary>
        /// The button used to control attachment
        /// </summary>
        private UIButton OsdButton_;

        private MainMenuPanel mainMenuPanel_;

        /// <summary>
        /// Initializes a new instance of the <see cref="OnScreenDisplayPanel"/> class.
        /// Constructs the empty OSD panel and hides it.
        /// </summary>
        /// <param name="parent">The parent panel to attach to</param>
        public OnScreenDisplayPanel(UIView uiView, MainMenuPanel mainMenuPanel) {
            items_ = new List<OsdItem>();
            mainMenuPanel_ = mainMenuPanel;

            thisPanel_ = (OsdUIPanel)uiView.AddUIComponent(typeof(OsdUIPanel));
            thisPanel_.name = "TMPE_OSD_Panel";
            thisPanel_.width = 10f;
            thisPanel_.height = PANEL_HEIGHT;
            thisPanel_.backgroundSprite = "GenericPanel";
            thisPanel_.color = new Color32(64, 64, 64, 240);

            // Hide!
            thisPanel_.isVisible = false;

            // ResetPanelPosition();
            var config = GlobalConfig.Instance.Main;
            thisPanel_.absolutePosition = new Vector3(config.OSDPanelX, config.OSDPanelY);

            // Setup drag
            var dragHandler = new GameObject("TMPE_OSD_DragHandler");
            dragHandler.transform.parent = thisPanel_.transform;
            dragHandler.transform.localPosition = Vector3.zero;
            Drag = dragHandler.AddComponent<UIDragHandle>();
            Drag.enabled = true;

            // Update();
        }

//        private void ResetPanelPosition() {
//            if (mainMenuPanel_.relativePosition.y < Screen.height / 2f) {
//                // Upper part of the screen, place below the TM:PE panel, with 1px margin
//                thisPanel_.relativePosition = new Vector3(0f, mainMenuPanel_.height + 1f, 0f);
//            } else {
//                // Lower part of the screen, place above the TM:PE panel, with 1px margin
//                thisPanel_.relativePosition = new Vector3(0f, -thisPanel_.height - 1f, 0f);
//            }
//        }

        public Configurator Setup() {
            return new Configurator(this);
        }

        public void Clear() {
            items_.Clear();
            Update();
        }

        public void Update() {
            thisPanel_.isVisible = (items_.Count > 0) && osdVisible_;
            if (osdVisible_) {
                OsdButton_.textColor =  Color.green;
                OsdButton_.text = "¿";
                OsdButton_.tooltip = Translation.GetString("Toggle_OSD_panel_visible");
            } else {
                OsdButton_.textColor =  Color.gray;
                OsdButton_.text = "?";
                OsdButton_.tooltip = Translation.GetString("Toggle_OSD_panel_hidden");
            }

            UpdatePanelItems();
        }

        private void UpdatePanelItems() {
            ClearPanelGuiItems();

            var titleLabel = thisPanel_.AddUIComponent<UILabel>();
            titleLabel.textColor = PALETTE_TEXT;
            titleLabel.text = Title;
            titleLabel.relativePosition = new Vector3(PADDING, PADDING);

            // Add items to the panel. Resize the panel to fit everything.
            var position = new Vector2(0, LABEL_HEIGHT + (PADDING * 2));

            foreach (var item in items_) {
                position = item.AddTo(thisPanel_, position);
            }

            thisPanel_.width = Mathf.Max(position.x, titleLabel.width + (2 * PADDING));
        }

        /// <summary>
        /// Delete all UILabels in the panel (spare the DragHandler)
        /// </summary>
        private void ClearPanelGuiItems() {
            foreach (var c in thisPanel_.components) {
                if (c is UILabel) {
                    UnityEngine.Object.Destroy(c);
                }
            }
        }

        /// <summary>
        /// Creates a tiny [?] button on the Main Menu panel to the right of TM:PE version
        /// which will toggle visibility for the hints panel
        /// </summary>
        /// <param name="versionLabel">The version label in the mainmenu, we attach to its right</param>
        /// <returns>The OSD button we've just created</returns>
        public UIButton CreateOsdButton(UILabel versionLabel) {
            OsdButton_ = mainMenuPanel_.AddUIComponent<UIButton>();
            OsdButton_.buttonsMask = UIMouseButton.Left;
            OsdButton_.normalBgSprite = "ButtonSmall";
            OsdButton_.hoveredBgSprite = "ButtonSmallHovered";
            OsdButton_.pressedBgSprite = "ButtonSmallPressed";
            OsdButton_.canFocus = false; // no focusing just click
            OsdButton_.width = 20f;
            OsdButton_.position = new Vector3(mainMenuPanel_.width - OsdButton_.width - 4f, 4f, 0f);
            OsdButton_.eventClicked += (component, param) => {
                osdVisible_ = !osdVisible_;
                Log._Debug($"OSD visibility button clicked, now vis={osdVisible_}");

                // Update the main config
                var config = GlobalConfig.Instance.Main;
                config.OSDPanelVisible = osdVisible_;
                GlobalConfig.WriteConfig();

                Update(); // reset visibility and (unnecessary but cheap) reset items
            };
            return OsdButton_;
        }
    }
}