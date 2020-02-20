// tabbing code is borrowed from RushHour mod and Advanced toolbar mod
// https://github.com/PropaneDragon/RushHour/blob/release/RushHour/Options/OptionHandler.cs
// https://github.com/CWMlolzlz/CS-AdvancedToolbar/blob/master/Source/ExpandableToolbar.cs

namespace TrafficManager.UI.Helpers {
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

        private UIScrollablePanel CreateScrollablePanel(UIPanel panel) {
            panel.autoLayout = true;
            panel.autoLayoutDirection = LayoutDirection.Horizontal;

            UIScrollablePanel scrollablePanel = panel.AddUIComponent<UIScrollablePanel>();
            scrollablePanel.autoLayout = true;
            scrollablePanel.autoLayoutPadding = new RectOffset(10, 10, 0, 16);
            scrollablePanel.autoLayoutStart = LayoutStart.TopLeft;
            scrollablePanel.wrapLayout = true;
            scrollablePanel.size = new Vector2(panel.size.x - 28, panel.size.y + 35);
            scrollablePanel.autoLayoutDirection = LayoutDirection.Horizontal; //Vertical does not work but why?

            UIScrollbar verticalScrollbar = CreateVerticalScrollbar(panel, scrollablePanel);
            verticalScrollbar.Show();
            verticalScrollbar.Invalidate();
            scrollablePanel.Invalidate();

            return scrollablePanel;
        }

        public UIHelper AddTabPage(string name, bool scrollBars=true) {
            UIButton tabButton = base.AddTab(name);
            tabButton.normalBgSprite = "SubBarButtonBase";
            tabButton.disabledBgSprite = "SubBarButtonBaseDisabled";
            tabButton.focusedBgSprite = "SubBarButtonBaseFocused";
            tabButton.hoveredBgSprite = "SubBarButtonBaseHovered";
            tabButton.pressedBgSprite = "SubBarButtonBasePressed";
            tabButton.textPadding = new RectOffset(10, 10, 10, 6);
            tabButton.autoSize = true;

            selectedIndex = tabCount - 1;
            UIPanel currentPanel = tabContainer.components[selectedIndex] as UIPanel;
            currentPanel.autoLayout = true;

            UIHelper panelHelper;
            if (scrollBars) {
                UIScrollablePanel scrollablePanel = CreateScrollablePanel(currentPanel);
                panelHelper = new UIHelper(scrollablePanel);
            }
            else {
                currentPanel.autoLayoutDirection = LayoutDirection.Vertical;
                panelHelper = new UIHelper(currentPanel);
            }
            return panelHelper;
        }

        public static ExtUITabstrip Create(UIHelperBase helperBase) {
            UIHelper actualHelper = helperBase as UIHelper;
            UIComponent container = actualHelper.self as UIComponent;
            container.size = new Vector2(container.size.x + 10, container.size.y);

            ExtUITabstrip tabStrip = container.AddUIComponent<ExtUITabstrip>();
            tabStrip.relativePosition = new Vector3(0, 0);
            tabStrip.size = new Vector2(container.width - 10, 40);

            float h = container.height - tabStrip.height * 2;
            UITabContainer tabContainer = container.AddUIComponent<UITabContainer>();
            tabContainer.relativePosition = new Vector3(0, 80);
            tabContainer.width = tabStrip.width;
            tabContainer.height = h;
            tabStrip.tabPages = tabContainer;

            return tabStrip;
        }

#if DEBUG
        public static class Test {
            private static int n = 0;
            public static void OnSettingsUI(UIHelperBase helperBase) {
                n = 0;
                ExtUITabstrip tabStrip = ExtUITabstrip.Create(helperBase);
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
#endif

    } //end ExtUITabstrip
} // end namesapce
