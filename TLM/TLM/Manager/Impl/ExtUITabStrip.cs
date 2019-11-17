namespace TrafficManager.Manager.Impl {
    using UnityEngine;
    using ICities;
    using ColossalFramework.UI;

    public sealed class ExtUITabstrip : UITabstrip {
        private UIScrollbar CreateVerticalScrollbar(UIPanel panel, UIScrollablePanel scrollablePanel) {
            UIScrollbar verticalScrollbar = panel.AddUIComponent<UIScrollbar>();
            verticalScrollbar.name = "VerticalScrollbar";
            verticalScrollbar.width = 20f;
            verticalScrollbar.height = tabPages.height;
            verticalScrollbar.orientation = UIOrientation.Vertical;
            verticalScrollbar.pivot = UIPivotPoint.TopRight;
            verticalScrollbar.AlignTo(panel, UIAlignAnchor.TopRight);
            verticalScrollbar.minValue = 0;
            verticalScrollbar.value = 0;
            verticalScrollbar.incrementAmount = 50;
            verticalScrollbar.autoHide = true;

            UISlicedSprite trackSprite = verticalScrollbar.AddUIComponent<UISlicedSprite>();
            trackSprite.relativePosition = Vector2.zero;
            trackSprite.autoSize = true;
            trackSprite.size = trackSprite.parent.size;
            trackSprite.fillDirection = UIFillDirection.Vertical;
            trackSprite.spriteName = "ScrollbarTrack";
            verticalScrollbar.trackObject = trackSprite;

            UISlicedSprite thumbSprite = trackSprite.AddUIComponent<UISlicedSprite>();
            thumbSprite.relativePosition = Vector2.zero;
            thumbSprite.fillDirection = UIFillDirection.Vertical;
            thumbSprite.autoSize = true;
            thumbSprite.width = thumbSprite.parent.width - 8;
            thumbSprite.spriteName = "ScrollbarThumb";
            verticalScrollbar.thumbObject = thumbSprite;

            verticalScrollbar.eventValueChanged += (component, value) => {
                scrollablePanel.scrollPosition = new Vector2(0, value);
            };

            panel.eventMouseWheel += (component, eventParam) => {
                verticalScrollbar.value -= (int)eventParam.wheelDelta * verticalScrollbar.incrementAmount;
            };

            scrollablePanel.eventMouseWheel += (component, eventParam) => {
                verticalScrollbar.value -= (int)eventParam.wheelDelta * verticalScrollbar.incrementAmount;
            };

            scrollablePanel.verticalScrollbar = verticalScrollbar;

            return verticalScrollbar;
        }

        private UIScrollablePanel CreateScrollebalePanel(UIPanel panel) {
            panel.autoLayout = true;
            panel.autoLayoutDirection = LayoutDirection.Horizontal; //Why?

            UIScrollablePanel scrollablePanel = panel.AddUIComponent<UIScrollablePanel>();
            scrollablePanel.autoLayout = true;
            scrollablePanel.autoLayoutStart = LayoutStart.TopLeft;
            scrollablePanel.wrapLayout = true;
            scrollablePanel.size = new Vector2(panel.size.x - 50, panel.size.y + 35);
            scrollablePanel.autoLayoutDirection = LayoutDirection.Horizontal; //Why?

            UIScrollbar verticalScrollbar = CreateVerticalScrollbar(panel, scrollablePanel);
            verticalScrollbar.Show();
            verticalScrollbar.Invalidate();
            scrollablePanel.Invalidate();

            return scrollablePanel;
        }

        public UIHelper AddTabPage(string name) {
            UIButton tabButton = base.AddTab(name);
            tabButton.normalBgSprite = "SubBarButtonBase";
            tabButton.disabledBgSprite = "SubBarButtonBaseDisabled";
            tabButton.focusedBgSprite = "SubBarButtonBaseFocused";
            tabButton.hoveredBgSprite = "SubBarButtonBaseHovered";
            tabButton.pressedBgSprite = "SubBarButtonBasePressed";
            tabButton.textPadding = new RectOffset(10, 10, 10, 6);
            tabButton.autoSize = true;

            selectedIndex = tabCount - 1;
            UIPanel currentPanel = this.tabContainer.components[selectedIndex] as UIPanel;
            currentPanel.autoLayoutPadding = new RectOffset(10, 10, 0, 16);
            UIScrollablePanel scrollablePanel = CreateScrollebalePanel(currentPanel);
            UIHelper panelHelper = new UIHelper(scrollablePanel);
            return panelHelper;
        }

        public static ExtUITabstrip Create(UIHelperBase helper_base) {
            UIHelper actualHelper = helper_base as UIHelper;
            UIComponent container = actualHelper.self as UIComponent;

            ExtUITabstrip tabStrip = container.AddUIComponent<ExtUITabstrip>();
            tabStrip.relativePosition = new Vector3(0, 0);
            tabStrip.size = new Vector2(container.width - 20, 40);

            float h = container.height - tabStrip.height * 2 - 40;
            UITabContainer tabContainer = container.AddUIComponent<UITabContainer>();
            tabContainer.relativePosition = new Vector3(0, 80);
            tabContainer.width = tabStrip.width;
            tabContainer.height = h;
            tabStrip.tabPages = tabContainer;

            return tabStrip;
        }

        public static class Test {
            private static int n = 0;
            public static void OnSettingsUI(UIHelperBase helper_base) {
                n = 0;
                ExtUITabstrip tabStrip = ExtUITabstrip.Create(helper_base);
                AddTab(tabStrip, "A");
                AddTab(tabStrip, "B");
                AddTab(tabStrip, "C");
                AddTab(tabStrip, "D");
                AddTab(tabStrip, "E");
                AddTab(tabStrip, "D");
                tabStrip.Invalidate();
            }

            private static void AddTab(ExtUITabstrip tabStrips, string name) {
                UIHelper panelHelper = tabStrips.AddTabPage("TAB " + name);
                MakePage(panelHelper, name);
            }

            public static void MakePage(UIHelperBase container, string name) {
                n += 1;
                for (int i = 0; i < n; ++i) {
                    UIHelperBase group = container.AddGroup($"normal group");
                    for (int j = 0; j < n; ++j) {
                        group.AddCheckbox($" {j} - some text here tab {name}", false, (bool b) => { });
                    } // end for j
                } // end for i
            } // end method MakePage
        } //end cLass Test
    } //end ExtUITabstrip
} // end namesapce