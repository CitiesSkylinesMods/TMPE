namespace TrafficManager.U {
    using System;
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using UnityEngine;

    /// <summary>
    /// Create an UI builder to populate a panel with good things: buttons, sub-panels, create a
    /// drag handle and other controls.
    /// </summary>
    public class UIBuilder {
        private readonly UIComponent parent_;

        public UIBuilder(UIComponent parent) {
            parent_ = parent;
        }

        /// <summary>Enables horizontal stacking autolayout for panel's children.</summary>
        /// <param name="padding">Padding inside the panel.</param>
        /// <returns>Self.</returns>
        public UIBuilder AutoLayoutHorizontal(int padding = 0) {
            var p = parent_ as UIPanel;
            if (p == null) {
                Log.Error($"UIBuilder: Gameobject of type {parent_.GetType()} is not a UIPanel");
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
            var p = parent_ as UIPanel;
            if (p == null) {
                Log.Error($"UIBuilder: Gameobject of type {parent_.GetType()} is not a UIPanel");
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
            where T : UIButton
        {
            var newButton = parent_.AddUIComponent(typeof(T)) as T;
            setupFn(newButton);
            return this;
        }

        public UIBuilder Label<T>(string t)
            where T : UILabel
        {
            var newLabel = parent_.AddUIComponent(typeof(T)) as T;
            newLabel.text = t;
            return this;
        }

        public UIBuilder NestedPanel<T>(Action<T> setupFn)
            where T : UIPanel
        {
            var newPanel = parent_.AddUIComponent(typeof(T)) as T;
            setupFn(newPanel);
            return new UIBuilder(newPanel);
        }
    }
}