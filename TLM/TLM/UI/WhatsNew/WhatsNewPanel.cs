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
        private const int _defaultWidth = 600;
        private const int _defaultHeight = 450;
        private const string _bulletPointPrefix = " * ";

        public override void Awake() {
            base.Awake();

            isVisible = true;
            canFocus = true;
            isInteractive = true;
            anchor = UIAnchorStyle.Left | UIAnchorStyle.Top | UIAnchorStyle.Proportional;
            size = new Vector2(_defaultWidth, _defaultHeight);
            backgroundSprite = "UnlockingPanel2";
            color = new Color32(110, 150, 180, 255);

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
            content.autoLayoutPadding = new RectOffset();
            content.autoReset = true;
            content.size = new Vector2(_defaultWidth - 20, _defaultHeight - _header.height - _footerPanel.height);
            AddScrollbar(panel, content);

            List<ChangelogEntry> changelogEntries = TMPELifecycle.Instance.WhatsNew.Changelogs;
            foreach (ChangelogEntry entry in changelogEntries) {
                AddChangelogEntry(entry, content);
            }

            content.autoLayout = true;
        }

        private void AddChangelogEntry(ChangelogEntry changelogEntry, UIScrollablePanel uiScrollablePanel) {
            UIPanel panel = uiScrollablePanel.AddUIComponent<UIPanel>();
            panel.width = _defaultWidth - 5;
            panel.autoLayoutDirection = LayoutDirection.Vertical;
            panel.autoLayoutStart = LayoutStart.TopLeft;
            panel.autoLayoutPadding = new RectOffset(5, 0, 0, 6);
            panel.autoFitChildrenVertically = true;

            bool isCurrentVersion = WhatsNew.CurrentVersion.Equals(changelogEntry.Version);

            UILabel title = panel.AddUIComponent<UILabel>();
            title.name = "Title";
            title.text = changelogEntry.Title.TrimStart();
            title.textScale = isCurrentVersion ? 1.5f : 1.2f;
            title.padding = new RectOffset(4, 0, 5, 0);

            UILabel build = panel.AddUIComponent<UILabel>();
            build.name = "BuildLabel";
            build.text = changelogEntry.Version.ToString();
            build.textScale = isCurrentVersion ? 0.8f : 0.7f;
            build.prefix = "v. ";
            build.suffix = isCurrentVersion ? " - current version" : string.Empty;
            build.padding = new RectOffset(6, 5, 0, 12);

            var bulletPadding = new RectOffset(8, 0, 0, 5);
            foreach (string text in changelogEntry.BulletPoints) {
                UILabel point = panel.AddUIComponent<UILabel>();
                point.name = "ChangelogEntryText";
                point.prefix = _bulletPointPrefix;
                point.text = text.TrimStart();
                point.textScale = isCurrentVersion ? 1f : 0.9f;
                point.padding = bulletPadding;
            }

            UIPanel separator = panel.AddUIComponent<UIPanel>();
            separator.name = "Separator";
            separator.isInteractive = false;
            separator.height = isCurrentVersion ? 50 : 30;
            panel.autoLayout = true;
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
            title.text = "What's new in TM:PE";
            title.MakePixelPerfect();

            var cancel = _header.AddUIComponent<UIButton>();
            cancel.normalBgSprite = "buttonclose";
            cancel.hoveredBgSprite = "buttonclosehover";
            cancel.pressedBgSprite = "buttonclosepressed";
            cancel.size = new Vector2(32, 32);
            cancel.relativePosition = new Vector2(_defaultWidth - 37, 4);
            cancel.eventClick += (_, _) => HandleClose();
        }

        public void AddScrollbar(UIComponent parentComponent, UIScrollablePanel scrollablePanel) {
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

            scrollbar.eventValueChanged += (component, value) => scrollablePanel.scrollPosition = new Vector2(0, value);

            parentComponent.eventMouseWheel += (component, eventParam) =>
            {
                scrollbar.value -= (int)eventParam.wheelDelta * scrollbar.incrementAmount;
            };

            scrollablePanel.eventMouseWheel += (component, eventParam) =>
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

            if (TMPELifecycle.Instance.IsGameLoaded) {
                TMPELifecycle.Instance.WhatsNew.MarkAsShown();
            }

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