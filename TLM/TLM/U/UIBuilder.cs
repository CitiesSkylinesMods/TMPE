namespace TrafficManager.U {
    using System;
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using TrafficManager.U.Autosize;
    using UnityEngine;

    /// <summary>
    /// Create an UI builder to populate a panel with good things: buttons, sub-panels, create a
    /// drag handle and other controls.
    /// </summary>
    public class UIBuilder {
        private readonly UIComponent current_;

        private USizePosition GetCurrentSizePosition() {
            return (current_ as USizePositionInterface).SizePosition;
        }

        public UIBuilder(UIComponent curr) {
            current_ = curr;
        }

        /// <summary>Enables horizontal stacking autolayout for panel's children.</summary>
        /// <param name="padding">Padding inside the panel.</param>
        /// <returns>Self.</returns>
        public UIBuilder AutoLayoutHorizontal(int padding = 0) {
            var p = current_ as UIPanel;
            if (p == null) {
                Log.Error($"UIBuilder: Gameobject of type {current_.GetType()} is not a UIPanel");
                return this;
            }
            // Right padding set to 0 assuming thata the autolayout padding will appear there
            p.padding = new RectOffset(padding, 0, padding, padding);
            p.autoLayoutPadding = new RectOffset(0, padding, 0, 0);
            p.autoLayoutDirection = LayoutDirection.Horizontal;
            p.autoLayout = true;
            return this;
        }

        /// <summary>Enables vertical stacking autolayout for panel's children.</summary>
        /// <param name="padding">Padding inside the panel.</param>
        /// <returns>Self.</returns>
        public UIBuilder AutoLayoutVertical(int padding = 0) {
            var p = current_ as UIPanel;
            if (p == null) {
                Log.Error($"UIBuilder: Gameobject of type {current_.GetType()} is not a UIPanel");
                return this;
            }
            // Right padding set to 0 assuming thata the autolayout padding will appear there
            p.padding = new RectOffset(padding, padding, 0, padding);
            p.autoLayoutPadding = new RectOffset(0, 0, 0, padding);
            p.autoLayoutDirection = LayoutDirection.Vertical;
            p.autoLayout = true;
            return this;
        }

        public UIBuilder Button<T>(Action<T> setupFn)
            where T : UIButton, USizePositionInterface
        {
            var newButton = current_.AddUIComponent(typeof(T)) as T;
            setupFn(newButton);
            return this;
        }

        public UIBuilder Label<T>(string t)
            where T : UILabel, USizePositionInterface
        {
            var newLabel = current_.AddUIComponent(typeof(T)) as T;
            newLabel.text = t;
            return this;
        }

        public UIBuilder NestedPanel<T>(Action<T> setupFn)
            where T : UIPanel, USizePositionInterface
        {
            var newPanel = current_.AddUIComponent(typeof(T)) as T;
            setupFn(newPanel);
            return new UIBuilder(newPanel);
        }

        public UIBuilder Width(USizeRule rule, float value) {
            USizePosition sz = GetCurrentSizePosition();
            sz.widthRule = rule;
            sz.widthValue = value;
        }

        public UIBuilder Height(USizeRule rule, float value) {
            USizePosition sz = GetCurrentSizePosition();
            sz.heightRule = rule;
            sz.heightValue = value;
        }
    }
}