namespace TrafficManager.UI.WhatsNew {
    using System.Collections.Generic;
    using ColossalFramework;
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using Lifecycle;
    using TrafficManager.State;
    using U;
    using UnityEngine;

    public class WhatsNewPanel : UIPanel {
        private UIDragHandle _header;
        private UIPanel _footerPanel;
        private const int _defaultWidth = 750;
        private const int _defaultHeight = 500;
        private const int _footerPanelHeight = 40;

        private readonly RectOffset _paddingZero = new RectOffset(0, 0, 0, 0);
        private readonly RectOffset _pillTextPadding = new RectOffset(4, 4, 5, 0);
        private readonly RectOffset _bulletListPadding = new RectOffset(8, 0, 0, 0);

        private readonly Color32 _panelBgColor = new Color32(55, 55, 55, 255);
        private readonly Color32 _textColor = new Color32(220, 220, 220, 255);
        private readonly Color _linkTextColorHover = new Color32(158, 219, 255, 255);
        private readonly Color _linkTextColor = new Color(0f, 0.52f, 1f);

        // Used in AddKeywordLabel()
        private readonly Vector2 _minKeywordLabelSize = new(90, 20);

        private static string T(string key) => Translation.Options.Get(key);

        public override void Awake() {
            base.Awake();

            isVisible = true;
            canFocus = true;
            isInteractive = true;
            anchor = UIAnchorStyle.Left | UIAnchorStyle.Top | UIAnchorStyle.Proportional;
            size = new Vector2(_defaultWidth, _defaultHeight);
            backgroundSprite = "GenericPanel";
            color = _panelBgColor;
            atlas = TextureUtil.Ingame;

            AddHeader();
            AddContent();
            AddFooter();
        }

        private void AddFooter() {
            _footerPanel = AddUIComponent<UIPanel>();
            _footerPanel.autoLayoutDirection = LayoutDirection.Vertical;
            _footerPanel.autoFitChildrenVertically = true;
            _footerPanel.size = new Vector2(_defaultWidth, 40);
            _footerPanel.relativePosition = new Vector2(0, _defaultHeight - _header.height);
            _footerPanel.autoLayoutPadding = new RectOffset(10, 10, 14, 14);

            UIHelper helper = new UIHelper(_footerPanel);
            helper.AddSpace(15);
            _footerPanel.autoLayout = true;
        }

        private void AddContent() {
            var panel = AddUIComponent<UIScrollablePanel>();
            panel.autoLayout = false;
            panel.maximumSize = GetMaxContentSize();
            panel.relativePosition = new Vector2(5, 40);
            panel.size = new Vector2(_defaultWidth - 10, _defaultHeight - _header.height - _footerPanelHeight);

            var content = panel.AddUIComponent<UIScrollablePanel>();
            content.autoLayout = false;
            content.autoLayoutDirection = LayoutDirection.Vertical;
            content.scrollWheelDirection = UIOrientation.Vertical;
            content.clipChildren = true;
            content.autoLayoutPadding = new RectOffset(0, 0, 10, 5);
            content.autoReset = true;
            content.size = new Vector2(_defaultWidth - 20, _defaultHeight - _header.height - _footerPanelHeight);
            AddScrollbar(panel, content);

            var stableRelease = Util.VersionUtil.IsStableRelease;

            List<Changelog> changelogs = TMPELifecycle.Instance.WhatsNew.Changelogs;
            foreach (Changelog changelog in changelogs) {
                if (!stableRelease || changelog.Stable) {
                    AddChangelogContent(changelog, content);
                }
            }

            content.autoLayout = true;
            panel.autoLayout = true;
        }

        private void AddChangelogContent(Changelog changelog, UIScrollablePanel uiScrollablePanel) {
            UIPanel panel = AddRowAutoLayoutPanel(parentPanel: uiScrollablePanel,
                                                  panelPadding: new RectOffset(5, 0, 0, 6),
                                                  panelWidth: _defaultWidth - 5,
                                                  vertical: true);
            panel.minimumSize = new Vector4(_defaultWidth - 10, 36);
            panel.name = "Changelog Content";
            AddVersionRow(panel, changelog);

            foreach (Changelog.Item item in changelog.Items) {
                AddBulletPoint(panel, item, _bulletListPadding);
            }

            AddSeparator(panel);
            panel.autoLayout = true;
        }

        private void AddSeparator(UIComponent parentPanel) {
            UIPanel separator = parentPanel.AddUIComponent<UIPanel>();
            separator.name = "Separator";
            separator.isInteractive = false;
            separator.height = 30;
        }

        private void AddVersionRow(UIComponent parentPanel, Changelog changelog) {
            bool isCurrentVersion = WhatsNew.CurrentVersion.Equals(changelog.Version);
            string buildString = StableOrTest(changelog);
            bool wasReleased = !string.IsNullOrEmpty(buildString);

            // row: [version number] released xyz
            UIPanel versionRow = parentPanel.AddUIComponent<UIPanel>();
            versionRow.name = "Version Row";
            versionRow.autoSize = false;
            versionRow.width = _defaultWidth - 10;
            versionRow.height = wasReleased? 46 : 36;

            // part: [version number]
            UIPanel versionLabel = AddVersionLabel(versionRow, changelog, buildString, wasReleased);
            versionLabel.name = "Version";
            // part released xyz
            UILabel title = versionRow.AddUIComponent<UILabel>();
            title.name = "Released";
            title.text = string.IsNullOrEmpty(changelog.Released) ? "Not released yet" : changelog.Released.TrimStart();
            title.suffix = isCurrentVersion ? " - current version" : string.Empty;
            title.textScale = 1.3f;
            title.textColor = _textColor;
            title.minimumSize = new Vector2(200, 36);
            title.padding = new RectOffset(16, 0, 0, 0);
            title.verticalAlignment = UIVerticalAlignment.Middle;
            title.relativePosition = new Vector3(versionLabel.width, 0, 0);
            if (!string.IsNullOrEmpty(changelog.Link)) {
                SetupLink(title, changelog);
            }
        }

        private string StableOrTest(Changelog changelog) =>
            changelog.Stable
                ? " STABLE"
                : changelog.Released != null
                    ? " TEST"
                    : string.Empty;

        private void SetupLink(UILabel title, Changelog changelog) {
            string url = $"https://github.com/CitiesSkylinesMods/TMPE/blob/master/CHANGELOG.md#{changelog.Link}";
            title.tooltip = url;
            title.textColor = _linkTextColor;
            title.eventMouseEnter += (label, _) => ((UILabel)label).textColor = _linkTextColorHover;
            title.eventMouseLeave += (label, _) => ((UILabel)label).textColor = _linkTextColor;
            title.eventClicked += (_, _) => Application.OpenURL(url);
        }

        private void AddBulletPoint(UIComponent parentPanel, Changelog.Item item, RectOffset bulletPadding) {
            // row: [keyword] text what has been changed
            UIPanel panel = AddRowAutoLayoutPanel(parentPanel: parentPanel,
                                                  panelPadding: bulletPadding,
                                                  panelWidth: _defaultWidth - 10);
            panel.name = "ChangelogItem";
            panel.padding = new RectOffset(20, 0, 0, 0);
            // part: [fixed/updated/removed]
            AddKeywordLabel(panel, item.Keyword.ToString(), item.Keyword);
            // part: text
            UILabel label = panel.AddUIComponent<UILabel>();
            label.name = "ChangelogItemText";
            label.wordWrap = true;
            label.text = item.Text;
            label.textScale = 0.8f;
            label.textColor = _textColor;
            label.autoHeight = true;
            label.width = _defaultWidth - 140;
            label.minimumSize = new Vector2(0, 25);
            label.verticalAlignment = UIVerticalAlignment.Middle;
            // update layout
            panel.autoLayout = true;
        }

        private UILabel AddKeywordLabel(UIPanel panel, string text, MarkupKeyword keyword) {
            UILabel label = panel.AddUIComponent<UILabel>();
            label.name = "ChangelogItemKeyword";
            label.text = text.ToUpper();
            label.textScale = 0.7f;
            label.textColor = Color.white;
            label.backgroundSprite = "TextFieldPanel";
            label.colorizeSprites = true;
            label.color = keyword.ToColor();
            label.minimumSize = _minKeywordLabelSize;
            label.textAlignment = UIHorizontalAlignment.Center;
            label.verticalAlignment = UIVerticalAlignment.Middle;
            label.padding = _pillTextPadding;
            label.atlas = TextureUtil.Ingame;

            return label;
        }

        private UIPanel AddVersionLabel(UIPanel parentPanel, Changelog changelog, string buildString, bool wasReleased) {
            UIPanel panel = AddRowAutoLayoutPanel(parentPanel: parentPanel,
                                                  panelPadding: _paddingZero,
                                                  panelWidth: 100,
                                                  vertical: true);
            panel.backgroundSprite = "TextFieldPanel";
            panel.color = MarkupKeyword.VersionStart.ToColor();
            panel.minimumSize = new Vector2(75, wasReleased ? 46 : 32);
            panel.padding = new RectOffset(0, 0, 6, 0);
            panel.height = wasReleased ? 45 : 36;
            panel.relativePosition = Vector3.zero;
            panel.atlas = TextureUtil.Ingame;

            UILabel version = panel.AddUIComponent<UILabel>();
            version.name = "ChangelogVersionNumber";
            version.text = changelog.Version.ToString();
            version.textScale = 1.2f;
            version.textAlignment = UIHorizontalAlignment.Center;
            version.verticalAlignment = UIVerticalAlignment.Top;
            version.padding = new RectOffset();
            version.autoSize = false;
            version.width = 85;

            if (wasReleased) {
                UILabel build = panel.AddUIComponent<UILabel>();
                build.name = "ChangelogBuildType";
                build.text = buildString;
                build.textScale = 0.7f;
                build.textAlignment = UIHorizontalAlignment.Center;
                build.verticalAlignment = UIVerticalAlignment.Bottom;
                build.padding = new RectOffset();
                build.autoSize = false;
                build.width = 85;
            }

            panel.autoLayout = true;

            return panel;
        }

        private UIPanel AddRowAutoLayoutPanel(UIComponent parentPanel, RectOffset panelPadding, float panelWidth, bool vertical = false) {
            UIPanel panel = parentPanel.AddUIComponent<UIPanel>();
            panel.autoLayout = false;
            panel.autoLayoutDirection = vertical ? LayoutDirection.Vertical : LayoutDirection.Horizontal;
            panel.autoLayoutStart = LayoutStart.TopLeft;
            panel.autoLayoutPadding = panelPadding;
            panel.autoFitChildrenVertically = true;
            panel.autoFitChildrenHorizontally = true;
            panel.width = panelWidth;
            return panel;
        }

        private void AddHeader() {
            _header = AddUIComponent<UIDragHandle>();
            _header.size = new Vector2(_defaultWidth, 42);
            _header.relativePosition = Vector2.zero;

            var title = _header.AddUIComponent<UILabel>();
            title.textScale = 1.35f;
            title.anchor = UIAnchorStyle.Top;
            title.textAlignment = UIHorizontalAlignment.Center;
            title.eventTextChanged += (_, _) => title.CenterToParent();
            title.text = $"{T(GeneralTab.WhatsNewButton.Label)} {TrafficManagerMod.ModName}";
            title.MakePixelPerfect();

            var cancel = _header.AddUIComponent<UIButton>();
            cancel.normalBgSprite = "buttonclose";
            cancel.hoveredBgSprite = "buttonclosehover";
            cancel.pressedBgSprite = "buttonclosepressed";
            cancel.atlas = TextureUtil.Ingame;
            cancel.size = new Vector2(32, 32);
            cancel.relativePosition = new Vector2(_defaultWidth - 37, 4);
            cancel.eventClick += (_, _) => HandleClose();
        }

        private void AddScrollbar(UIComponent parentComponent, UIScrollablePanel scrollablePanel) {
            var scrollbar = parentComponent.AddUIComponent<UIScrollbar>();
            scrollbar.orientation = UIOrientation.Vertical;
            scrollbar.pivot = UIPivotPoint.TopLeft;
            scrollbar.minValue = 0;
            scrollbar.value = 0;
            scrollbar.incrementAmount = 25;
            scrollbar.autoHide = true;
            scrollbar.width = 4;
            scrollbar.height = _defaultHeight - _header.height - _footerPanelHeight;
            scrollbar.scrollEasingType = EasingType.BackEaseOut;

            var trackSprite = scrollbar.AddUIComponent<UISlicedSprite>();
            trackSprite.relativePosition = Vector2.zero;
            trackSprite.anchor = UIAnchorStyle.All;
            trackSprite.size = scrollbar.size;
            trackSprite.fillDirection = UIFillDirection.Vertical;
            trackSprite.spriteName = string.Empty; // "ScrollbarTrack";
            scrollbar.trackObject = trackSprite;

            var thumbSprite = trackSprite.AddUIComponent<UISlicedSprite>();
            thumbSprite.relativePosition = Vector2.zero;
            thumbSprite.fillDirection = UIFillDirection.Vertical;
            thumbSprite.size = scrollbar.size;
            thumbSprite.spriteName = "ScrollbarTrack"; // "ScrollbarThumb";
            thumbSprite.atlas = TextureUtil.Ingame;
            thumbSprite.color = new Color(40, 40, 40);
            scrollbar.thumbObject = thumbSprite;

            scrollbar.eventValueChanged += (_, value) => scrollablePanel.scrollPosition = new Vector2(0, value);

            parentComponent.eventMouseWheel += (_, eventParam) =>
            {
                scrollbar.value -= (int)eventParam.wheelDelta * scrollbar.incrementAmount;
            };

            scrollablePanel.eventMouseWheel += (_, eventParam) =>
            {
                scrollbar.value -= (int)eventParam.wheelDelta * scrollbar.incrementAmount;
            };

            scrollablePanel.verticalScrollbar = scrollbar;
        }

        private Vector2 GetMaxContentSize() {
            var resolution = GetUIView().GetScreenResolution();
            return new Vector2(_defaultWidth, resolution.y - 580f);
        }

        private void HandleClose() {
            if (!gameObject) return;

            if (UIView.GetModalComponent() == this) {
                UIView.PopModal();
                UIComponent modal = UIView.GetModalComponent();
                if (modal) {
                    UIView.GetAView().BringToFront(modal);
                } else {
                    UIView.GetAView().panelsLibraryModalEffect.Hide();
                }
            }

            _header = null;
            _footerPanel = null;
            Destroy(gameObject);
            Log.Info("What's New panel closed and destroyed.");
        }
    }
}