namespace TrafficManager.U {
    using System;
    using ColossalFramework.UI;
    using TrafficManager.State.Keybinds;
    using TrafficManager.U.Autosize;
    using TrafficManager.UI;

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

        /// <summary>Same as Button<T>() but allows custom subtype to be passed to AddUIComponent.</summary>
        /// <param name="t">The type to pass to AddUIComponent.</param>
        /// <typeparam name="TButton">The type to cast to.</typeparam>
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

        /// <summary>Add a colored label for a keyboard shortcut.</summary>
        /// <returns>New UI Builder with the keybind label created.</returns>
        public UiBuilder<U.Label.ULabel> ShortcutLabel(KeybindSetting ks) {
            var shortcutLabel = Control.AddUIComponent(typeof(U.Label.ULabel)) as U.Label.ULabel;

            shortcutLabel.backgroundSprite = "GenericPanelDark";
            shortcutLabel.textColor = UConst.SHORTCUT_KEYBIND_TEXT;
            shortcutLabel.text = $" {ks.ToLocalizedString()} ";

            return new UiBuilder<U.Label.ULabel>(shortcutLabel);
        }

        /// <summary>Add a colored label for a 'Shift + Ctrl + Alt' modifier key.</summary>
        /// <returns>New UI Builder with the keybind label created.</returns>
        public UiBuilder<U.Label.ULabel> ModifierLabel(bool shift, bool ctrl, bool alt) {
            var modifierLabel = Control.AddUIComponent(typeof(U.Label.ULabel)) as U.Label.ULabel;

            modifierLabel.backgroundSprite = "GenericPanelDark";
            modifierLabel.textColor = UConst.SHORTCUT_KEYBIND_TEXT;

            string modifiers = shift
                                   ? Translation.Options.Get("Shortcut.Modifier:Shift")
                                   : string.Empty;
            if (ctrl) {
                modifiers += string.IsNullOrEmpty(modifiers) ? string.Empty : " + ";
                modifiers += Translation.Options.Get("Shortcut.Modifier:Ctrl");
            }
            if (alt) {
                modifiers += string.IsNullOrEmpty(modifiers) ? string.Empty : " + ";
                modifiers += Translation.Options.Get("Shortcut.Modifier:Alt");
            }

            modifierLabel.text = $" {modifiers} ";

            return new UiBuilder<U.Label.ULabel>(modifierLabel);
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