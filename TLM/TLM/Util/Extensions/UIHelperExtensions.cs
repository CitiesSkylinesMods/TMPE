namespace TrafficManager.Util.Extensions {
    using ColossalFramework.UI;
    using ICities;
    using TrafficManager.Lifecycle;
    using TrafficManager.UI.Helpers;
    using UnityEngine;

    internal static class UIHelperExtensions {
        public static UIComponent GetSelf(this UIHelperBase container) =>
            (container as UIHelper).self as UIComponent;
        public static T AddComponent<T>(this UIHelperBase container)
            where T : Component => container.GetSelf().gameObject.AddComponent<T>();

        public static T AddUIComponent<T>(this UIHelperBase container)
            where T : UIComponent => container.GetSelf().AddUIComponent<T>();

        public static CustomDownDown AddCustomDropDown(this UIHelperBase container,
                                                       string text,
                                                       string[] options,
                                                       int defaultSelection,
                                                       OnDropdownSelectionChanged eventCallback
            ) {
            if (eventCallback != null && !string.IsNullOrEmpty(text)) {
                UIComponent root = container.GetSelf();
                UIPanel uipanel = root.AddUIComponent<UIPanel>();
                uipanel.area = new Vector4(14, 0, 232, 72);
                uipanel.autoLayoutDirection = LayoutDirection.Vertical;
                uipanel.autoLayoutPadding = new RectOffset(1, 0, 2, 0);
                uipanel.autoLayout = true;

                var label = uipanel.AddUIComponent<UILabel>();
                label.text = text;
                label.name = "Label";

                var dropdown = uipanel.AddUIComponent<CustomDownDown>();
                dropdown.name = "Dropdown";
                dropdown.area = new Vector4(0, 24, 225, 38);
                dropdown.items = options;
                dropdown.selectedIndex = defaultSelection;
                dropdown.triggerButton = dropdown;
                dropdown.relativePosition = new Vector3(0, 24, 0);
                dropdown.itemPadding = new RectOffset(14, 14, 0, 0);
                dropdown.listPadding = new RectOffset(4, 4, 4, 4);
                dropdown.textFieldPadding = new RectOffset(14, 40, 7, 4);
                dropdown.itemHeight = 24;
                dropdown.listHeight = 200;
                dropdown.textScale = 1.25f;
                dropdown.itemHover = "ListItemHover";
                dropdown.itemHighlight = "ListItemHighlight";
                dropdown.focusedFgSprite = "OptionsDropboxFocused";
                dropdown.focusedBgSprite = "OptionsDropboxHovered";
                dropdown.hoveredBgSprite = "OptionsDropboxHovered";
                dropdown.normalBgSprite = "OptionsDropbox";
                dropdown.listBackground = "OptionsDropboxListbox";
                dropdown.popupColor = Color.white;
                dropdown.popupTextColor = new Color32(170, 170, 170, 255);
                dropdown.eventSelectedIndexChanged += delegate(UIComponent c, int sel) {
                    eventCallback(sel);
                };

                if (CustomDownDown.ScrollTemplate) {
                    // reuse gameObject template
                    dropdown.listScrollbar = CustomDownDown.ScrollTemplate.GetComponent<UIScrollbar>();
                } else {
                    UIScrollbar scrollbar = CreateScrollbar();
                    // cache gameObject for later reuse
                    CustomDownDown.ScrollTemplate = scrollbar.gameObject;
                    dropdown.listScrollbar = scrollbar;
                }

                return dropdown;
            }
            return null;
        }

        private static UIScrollbar CreateScrollbar() {
            UIScrollbar verticalScrollbar = new GameObject("TMPE_ScrollbarV").AddComponent<UIScrollbar>();
            // attach to lifecycle gameObject for easier search in gameObject tree
            verticalScrollbar.gameObject.transform.SetParent(TMPELifecycle.Instance.gameObject.transform);

            verticalScrollbar.name = "TMPE_ScrollbarV";
            verticalScrollbar.width = 25;
            verticalScrollbar.height = 200;
            verticalScrollbar.orientation = UIOrientation.Vertical;
            verticalScrollbar.pivot = UIPivotPoint.TopLeft;
            verticalScrollbar.minValue = 0;
            verticalScrollbar.maxValue = 87;
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
            thumbSprite.width = thumbSprite.parent.width - 6;
            thumbSprite.spriteName = "ScrollbarThumb";
            verticalScrollbar.thumbObject = thumbSprite;

            return verticalScrollbar;
        }
    }
}