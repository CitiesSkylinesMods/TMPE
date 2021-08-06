namespace TrafficManager.U {
    using System;
    using System.Collections.Generic;
    using ColossalFramework.UI;
    using JetBrains.Annotations;
    using TrafficManager.State.Keybinds;
    using TrafficManager.U.Autosize;
    using TrafficManager.UI;
    using TrafficManager.Util;
    using UnityEngine;

    /// <summary>
    /// Create an UI builder to populate a panel with good things: buttons, sub-panels, create a
    /// drag handle and other controls.
    /// </summary>
    /// <typeparam name="TControl">The UI Component and ISmartSizableControl we're placing into the UI.</typeparam>
    public class UBuilder {
        public AtlasBuilder AtlasBuilder;

        public UBuilder([CanBeNull]
                        AtlasBuilder atlasBuilder = null) {
            AtlasBuilder = atlasBuilder;
        }

        public static UBuilder Create(string abAtlasName,
                                      string abLoadingPath,
                                      IntVector2 abSizeHint) {
            var ab = new AtlasBuilder(
                atlasName: abAtlasName,
                loadingPath: abLoadingPath,
                sizeHint: abSizeHint);
            return new UBuilder(ab);
        }

        /// <summary>
        /// Generic window creation.
        /// Creates a window with generic padding (4px). The size will be "fit to children".
        /// </summary>
        /// <typeparam name="TWindow">The window root panel type.</typeparam>
        /// <param name="setupFn">Function called on to perform window post-setup.</param>
        /// <returns>The new window panel.</returns>
        public TWindow CreateWindow<TWindow>()
            where TWindow : UIComponent, U.Autosize.ISmartSizableControl {
            var parent = UIView.GetAView();
            var window = (TWindow)parent.AddUIComponent(typeof(TWindow));

            window.ResizeFunction((UResizer r) => { r.FitToChildren(); });
            window.SetPadding(UPadding.Default);

            return window;
        }

        /// <summary>
        /// Create a button as a child of current UIBuilder. A new UIBuilder is returned.
        /// </summary>
        /// <typeparam name="TButton">The type of UIButton and ISmartSizableControl.</typeparam>
        public TButton Button<TButton>(UIComponent parent)
            where TButton : UIButton, ISmartSizableControl {
            return parent.AddUIComponent(typeof(TButton)) as TButton;
        }

        public TButton Button<TButton>(UIComponent parent,
                                       string text,
                                       [CanBeNull]
                                       string tooltip,
                                       Vector2 size,
                                       UStackMode stack = UStackMode.ToTheRight)
            where TButton : UIButton, ISmartSizableControl {
            TButton b = this.Button<TButton>(parent);
            b.text = text;

            if (tooltip != null) {
                b.tooltip = tooltip;
            }

            if (stack != UStackMode.None) {
                b.SetStacking(stack);
            }

            b.SetFixedSize(size);
            return b;
        }

        /// <summary>Quick create a label and stack it. Optionally set markup processing mode.</summary>
        /// <param name="t">Localized text.</param>
        /// <param name="stack">Stacking mode related to previous sibling.</param>
        /// <param name="processMarkup">Whether label text contains C:S color markup.</param>
        /// <returns>New label.</returns>
        public TLabel Label<TLabel>(UIComponent parent,
                                    string t,
                                    UStackMode stack = UStackMode.ToTheRight,
                                    bool processMarkup = false)
            where TLabel : UILabel, ISmartSizableControl {
            var label = parent.AddUIComponent(typeof(TLabel)) as TLabel;
            label.text = t;

            if (stack != UStackMode.None) {
                label.SetStacking(stack);
            }

            label.processMarkup = processMarkup;

            return label;
        }

        /// <summary>Calls <see cref="Label{TLabel,TParent}"/> with default type arguments:
        /// ULabel, and parent type to UIComponent.
        /// </summary>
        public ULabel Label_(UIComponent parent,
                             string t,
                             UStackMode stack = UStackMode.ToTheRight,
                             bool processMarkup = false) {
            return Label<ULabel>(parent, t, stack, processMarkup);
        }

        public TPanel Panel<TPanel>(UIComponent parent,
                                    UStackMode stack = UStackMode.None)
            where TPanel : UIPanel, ISmartSizableControl {
            var p = parent.AddUIComponent(typeof(TPanel)) as TPanel;
            if (stack != UStackMode.None) {
                p.SetStacking(stack);
            }

            return p;
        }

        public UPanel Panel_(UIComponent parent,
                             UStackMode stack = UStackMode.None) {
            return Panel<UPanel>(parent, stack);
        }
    }
}