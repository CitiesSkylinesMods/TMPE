namespace TrafficManager.UI.WhatsNew {
    using System.Collections.Generic;
    using ColossalFramework;
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using Lifecycle;
    using UnityEngine;

    public class WhatsNewPanel : UIPanel {
        private UIDragHandle _header;
        private UIPanel _footerPanel;
        private const int _defaultWidth = 750;
        private const int _defaultHeight = 500;

        private readonly RectOffset _paddingZero = new RectOffset(0, 0, 0, 0);
        private readonly RectOffset _pillTextPadding = new RectOffset(4, 4, 5, 0);
        private readonly RectOffset _bulletListPadding = new RectOffset(8, 0, 0, 0);

        private readonly Color32 _panelBgColor = new Color32(55, 55, 55, 255);
        private readonly Color32 _textColor = new Color32(220, 220, 220, 255);
        private readonly Color _linkTextColorHover = new Color32(158, 219, 255, 255);
        private readonly Color _linkTextColor = new Color(0f, 0.52f, 1f);

        // Used in AddKeywordLabel()
        private readonly Vector2 _minKeywordLabelSize = new(85, 20);

        public override void Awake() {
            base.Awake();

            isVisible = true;
            canFocus = true;
            isInteractive = true;
            anchor = UIAnchorStyle.Left | UIAnchorStyle.Top | UIAnchorStyle.Proportional;
            size = new Vector2(_defaultWidth, _defaultHeight);
            backgroundSprite = "GenericPanel";
            color = _panelBgColor;

            AddHeader();
            AddFooter();
            AddContent();
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
            panel.maximumSize = GetMaxContentSize();
            panel.relativePosition = new Vector2(5, 40);
            panel.size = new Vector2(_defaultWidth - 10, _defaultHeight - _header.height - _footerPanel.height);
            panel.autoLayout = true;

            var content = panel.AddUIComponent<UIScrollablePanel>();
            content.autoLayoutDirection = LayoutDirection.Vertical;
            content.scrollWheelDirection = UIOrientation.Vertical;
            content.clipChildren = true;
            content.autoLayoutPadding = new RectOffset(0, 0, 10, 5);
            content.autoReset = true;
            content.size = new Vector2(_defaultWidth - 20, _defaultHeight - _header.height - _footerPanel.height);
            AddScrollbar(panel, content);

            var stableRelease = Util.VersionUtil.IsStableRelease;

            List<ChangelogEntry> changelogEntries = TMPELifecycle.Instance.WhatsNew.Changelogs;
            foreach (ChangelogEntry entry in changelogEntries) {
                if (!stableRelease || entry.Stable) {
                    AddChangelogEntry(entry, content);
                }
            }

            content.autoLayout = true;
        }

        private void AddChangelogEntry(ChangelogEntry changelogEntry, UIScrollablePanel uiScrollablePanel) {
            UIPanel panel = AddRowAutoLayoutPanel(parentPanel: uiScrollablePanel,
                                                  panelPadding: new RectOffset(5, 0, 0, 6),
                                                  panelWidth: _defaultWidth - 5,
                                                  vertical: true);

            AddVersionRow(panel, changelogEntry);

            foreach (ChangelogEntry.ChangeEntry changeEntry in changelogEntry.ChangeEntries) {
                AddBulletPoint(panel, changeEntry, _bulletListPadding);
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

        private void AddVersionRow(UIComponent parentPanel, ChangelogEntry changelogEntry) {
            bool isCurrentVersion = WhatsNew.CurrentVersion.Equals(changelogEntry.Version);
            string buildString = StableOrTest(changelogEntry);
            bool wasReleased = !string.IsNullOrEmpty(buildString);

            // row: [version number] released xyz
            UIPanel versionRow = AddRowAutoLayoutPanel(parentPanel: parentPanel,
                                                       panelPadding: _paddingZero,
                                                       panelWidth: _defaultWidth - 10);
            versionRow.maximumSize = new Vector2(_defaultWidth - 10, wasReleased? 46 : 36);

            // part: [version number]
            // UILabel versionLabel = AddKeywordLabel(versionRow, versionStr, MarkupKeyword.VersionStart);
            UIPanel versionLabel = AddVersionLabel(versionRow, changelogEntry, buildString, wasReleased);
            versionLabel.name = "Version";
            // part released xyz
            UILabel title = versionRow.AddUIComponent<UILabel>();
            title.name = "Released";
            title.text = string.IsNullOrEmpty(changelogEntry.Released) ? "Not released yet" : changelogEntry.Released.TrimStart();
            title.suffix = isCurrentVersion ? " - current version" : string.Empty;
            title.textScale = 1.3f;
            title.textColor = _textColor;
            title.minimumSize = new Vector2(0, 36);
            title.padding = new RectOffset(16, 0, 0, 0);
            title.verticalAlignment = UIVerticalAlignment.Middle;
            if (!string.IsNullOrEmpty(changelogEntry.Link)) {
                SetupLink(title, changelogEntry);
            }

            versionRow.autoLayout = true;
        }

        private string StableOrTest(ChangelogEntry changelogEntry) =>
            changelogEntry.Stable
                ? " STABLE"
                : changelogEntry.Released != null
                    ? " TEST"
                    : string.Empty;

        private void SetupLink(UILabel title, ChangelogEntry changelogEntry) {
            string url = $"https://github.com/CitiesSkylinesMods/TMPE/blob/master/CHANGELOG.md#{changelogEntry.Link}";
            title.tooltip = url;
            title.textColor = _linkTextColor;
            title.eventMouseEnter += (label, _) => ((UILabel)label).textColor = _linkTextColorHover;
            title.eventMouseLeave += (label, _) => ((UILabel)label).textColor = _linkTextColor;
            title.eventClicked += (_, _) => Application.OpenURL(url);
        }

        private void AddBulletPoint(UIComponent parentPanel, ChangelogEntry.ChangeEntry changeEntry, RectOffset bulletPadding) {
            // row: [keyword] text what has been changed
            UIPanel panel = AddRowAutoLayoutPanel(parentPanel: parentPanel,
                                                  panelPadding: bulletPadding,
                                                  panelWidth: _defaultWidth - 10);
            panel.name = "ChangelogEntry";
            panel.padding = new RectOffset(20, 0, 0, 0);
            // part: [fixed/updated/removed]
            AddKeywordLabel(panel, changeEntry.Keyword.ToString(), changeEntry.Keyword);
            // part: text
            UILabel label = panel.AddUIComponent<UILabel>();
            label.name = "ChangelogEntryText";
            label.wordWrap = true;
            label.text = changeEntry.Text;
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
            label.name = "ChangelogEntryKeyword";
            label.text = text.ToUpper();
            label.textScale = 0.7f;
            label.textColor = Color.white;
            label.backgroundSprite = "TextFieldPanel";
            label.colorizeSprites = true;
            label.color = WhatsNewMarkup.GetColor(keyword);
            label.minimumSize = _minKeywordLabelSize;
            label.textAlignment = UIHorizontalAlignment.Center;
            label.verticalAlignment = UIVerticalAlignment.Middle;
            label.padding = _pillTextPadding;

            return label;
        }

        private UIPanel AddVersionLabel(UIPanel parentPanel, ChangelogEntry changelogEntry, string buildString, bool wasReleased) {
            UIPanel panel = AddRowAutoLayoutPanel(parentPanel: parentPanel,
                                                  panelPadding: _paddingZero,
                                                  panelWidth: 100,
                                                  vertical: true);
            panel.backgroundSprite = "TextFieldPanel";
            panel.color = WhatsNewMarkup.GetColor(MarkupKeyword.VersionStart);
            panel.minimumSize = new Vector2(75, wasReleased ? 46 : 32);
            panel.padding = new RectOffset(0, 0, 6, 0);
            panel.height = wasReleased ? 45 : 36;

            UILabel version = panel.AddUIComponent<UILabel>();
            version.name = "ChangelogEntryVersionNumber";
            version.text = changelogEntry.Version.ToString();
            version.textScale = 1.2f;
            version.textAlignment = UIHorizontalAlignment.Center;
            version.verticalAlignment = UIVerticalAlignment.Top;
            version.padding = new RectOffset();
            version.autoSize = false;
            version.width = 85;

            if (wasReleased) {
                UILabel build = panel.AddUIComponent<UILabel>();
                build.name = "ChangelogEntryBuildType";
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
            title.text = "What's New - " + TrafficManagerMod.ModName;
            title.MakePixelPerfect();

            var cancel = _header.AddUIComponent<UIButton>();
            cancel.normalBgSprite = "buttonclose";
            cancel.hoveredBgSprite = "buttonclosehover";
            cancel.pressedBgSprite = "buttonclosepressed";
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
            scrollbar.width = 10;
            scrollbar.height = _defaultHeight - _header.height - _footerPanel.height;
            scrollbar.scrollEasingType = EasingType.BackEaseOut;

            var trackSprite = scrollbar.AddUIComponent<UISlicedSprite>();
            trackSprite.relativePosition = Vector2.zero;
            trackSprite.autoSize = true;
            trackSprite.anchor = UIAnchorStyle.All;
            trackSprite.size = trackSprite.parent.size;
            trackSprite.fillDirection = UIFillDirection.Vertical;
            trackSprite.spriteName = "ScrollbarTrack";
            scrollbar.trackObject = trackSprite;

            var thumbSprite = trackSprite.AddUIComponent<UISlicedSprite>();
            thumbSprite.relativePosition = Vector2.zero;
            thumbSprite.fillDirection = UIFillDirection.Vertical;
            thumbSprite.autoSize = true;
            thumbSprite.width = thumbSprite.parent.width;
            thumbSprite.spriteName = "ScrollbarThumb";
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

            TMPELifecycle.Instance.WhatsNew.MarkAsShown();

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