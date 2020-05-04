namespace TrafficManager.U {
    using System;
    using System.Collections.Generic;
    using ColossalFramework.UI;
    using TrafficManager.State.Keybinds;
    using TrafficManager.U.Autosize;
    using TrafficManager.UI;

    /// <summary>
    /// Create an UI builder to populate a panel with good things: buttons, sub-panels, create a
    /// drag handle and other controls.
    /// </summary>
    /// <typeparam name="TControl">The UI Component and ISmartSizableControl we're placing into the UI.</typeparam>
    public class UiBuilder<TControl> : IDisposable
        where TControl : UIComponent, ISmartSizableControl
    {
        public readonly TControl Control;

        public UiBuilder(TControl curr) {
            Control = curr;
        }

        /// <summary>
        /// Generic window creation.
        /// Creates a window with generic padding (4px). The size will be "fit to children".
        /// Calls a custom function after the setup is done, there you can populate the window.
        /// </summary>
        /// <typeparam name="TWindow">The window root panel type.</typeparam>
        /// <param name="setupFn">Function called on to perform window post-setup.</param>
        /// <returns>The new window panel.</returns>
        public static TWindow CreateWindow<TWindow>(Action<UiBuilder<TWindow>> setupFn)
            where TWindow : UIComponent, ISmartSizableControl
        {
            var parent = UIView.GetAView();
            var window = (TWindow)parent.AddUIComponent(typeof(TWindow));

            using (var builder = new U.UiBuilder<TWindow>(window)) {
                builder.ResizeFunction(r => { r.FitToChildren(); });
                builder.SetPadding(UConst.UIPADDING);

                setupFn(builder);

                // Resize everything correctly
                builder.Done();
            }

            return window;
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

        /// <summary>
        /// Same as Button&lt;T&gt;() but allows custom subtype to be passed to AddUIComponent.
        /// </summary>
        /// <typeparam name="TButton">The type to cast to.</typeparam>
        /// <param name="t">The type to pass to AddUIComponent.</param>
        /// <returns>New builder for that button.</returns>
        public UiBuilder<TButton> Button<TButton>(Type t)
            where TButton : UIButton, ISmartSizableControl {
            var newButton = Control.AddUIComponent(t) as TButton;

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
            UResizer.UpdateControl(this.Control);
        }

        /// <summary>End of `using (var x = ...) {}` statement.</summary>
        public void Dispose() { }

        public void SetPadding(float f) {
            Control.GetResizerConfig().Padding = f;
        }
    }
}