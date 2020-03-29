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
    /// <typeparam name="T">The UI Component and ISmartSizableControl we're placing into the UI.</typeparam>
    public class UiBuilder<TControl> : IDisposable
        where TControl : UIComponent, ISmartSizableControl
    {
        public readonly TControl Control;

        public UiBuilder(TControl curr) {
            Control = curr;
        }

        /// <summary>
        /// Create a button as a child of current UIBuilder. A new UIBuilder is returned.
        /// </summary>
        /// <typeparam name="TButton">The type of UIButton and ISmartSizableControl.</typeparam>
        /// <returns>New UIBuilder.</returns>
        public UiBuilder<TButton> Button<TButton>()
            where TButton : UIButton, ISmartSizableControl {
            var newButton = Control.AddUIComponent(typeof(TButton)) as TButton;

            return new UiBuilder<TButton>(newButton);
        }

        public UiBuilder<TLabel> Label<TLabel>(string t)
            where TLabel : UILabel, ISmartSizableControl {
            var newLabel = Control.AddUIComponent(typeof(TLabel)) as TLabel;
            newLabel.text = t;
            return new UiBuilder<TLabel>(newLabel);
        }

        public UiBuilder<TPanel> ChildPanel<TPanel>(Action<TPanel> setupFn)
            where TPanel : UIPanel, ISmartSizableControl {
            var newPanel = Control.AddUIComponent(typeof(TPanel)) as TPanel;
            setupFn(newPanel);
            return new UiBuilder<TPanel>(newPanel);
        }

        public void ResizeFunction(Action<UResizer> resizeFn) {
            Control.GetResizerConfig().SetResizeFunction(resizeFn);
        }

        /// <summary>
        /// When form building is finished, recalculates all nested sizes and places stuff
        /// according to sizes and positions configured in USizePosition members of form controls.
        /// </summary>
        public void Done() {
            // UResizer.UpdateHierarchy(this.Control);
            UResizer.UpdateControlRecursive(this.Control, null);
        }

        /// <summary>End of `using (var x = ...) {}` statement.</summary>
        public void Dispose() { }

        public void SetPadding(float f) {
            Control.GetResizerConfig().Padding = f;
        }
    }
}