namespace TrafficManager.U.Autosize {
    using System;
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using JetBrains.Annotations;
    using TrafficManager.UI;
    using UnityEngine;

    /// <summary>
    /// UResizer is passed to the resize function for a resizable control.
    /// Allows the user code functions to resize their controls in some way.
    /// The call will be done recursively for all form controls from the leaves of the control
    /// hierarchy up to the root, so that you will always know bounding box for child controls.
    /// </summary>
    public class UResizer {
        /// <summary>The control which is being resized.</summary>
        public readonly UIComponent Control;

        /// <summary>For stacking purposes, gives previous sibling in a sibling list.</summary>
        [CanBeNull]
        public readonly UIComponent PreviousSibling;

        /// <summary>Calculated bounding box for child controls.</summary>
        public UBoundingBox ChildrenBox;

        /// <summary>Extra data from UResizerConfig might become useful, like Padding.</summary>
        public UResizerConfig Config;

        public UResizer([NotNull] UIComponent control,
                        [NotNull] UResizerConfig config,
                        [CanBeNull] UIComponent previousSibling,
                        UBoundingBox childrenBox) {
            ChildrenBox = childrenBox;
            Config = config;
            Control = control;
            PreviousSibling = previousSibling;
        }

        public static void UpdateControl(UIComponent control) {
            UpdateControlRecursive(current: control, previousSibling: null);
        }

        /// <summary>
        /// Recursively descends down the GUI controls tree and calls OnResize on <see cref="ISmartSizableControl"/>
        /// implementors, then allows parents to adjust their size to the contents and so on.
        /// </summary>
        /// <param name="current">The control being updated.</param>
        /// <param name="previousSibling">If not null, points to previous sibling for control stacking.</param>
        /// <returns>The bounding box or null (if does not contribute to the parent's box).</returns>
        internal static UBoundingBox? UpdateControlRecursive([NotNull] UIComponent current,
                                                             [CanBeNull] UIComponent previousSibling) {
            // is object valid? is still attached to something in scene?
            if (!current.gameObject.activeSelf) {
                return default;
            }

            // Create an empty bounding box update it with all children bounding boxes
            UBoundingBox allChildrenBox = default;

            // For all children visit their resize functions and update allChildrenBox
            UIComponent previousChild = null;
            (current as ISmartSizableControl)?.OnBeforeResizerUpdate();

            foreach (Transform child in current.transform) {
                if (!child.gameObject.activeSelf) {
                    continue; // possibly a destroyed object
                }

                UIComponent childUiComponent = child.gameObject.GetComponent<UIComponent>();
                UBoundingBox? childBox = UpdateControlRecursive(
                    current: childUiComponent,
                    previousSibling: previousChild);

                // if child contributes to our bounding box, we can update our box
                if (childBox != null) {
                    allChildrenBox.ExpandToFit(childBox.Value);
                }

                previousChild = childUiComponent;
            }

            UBoundingBox? result = UResizerConfig.CallOnResize(
                control: current,
                previousSibling,
                childrenBox: allChildrenBox);

            (current as ISmartSizableControl)?.OnAfterResizerUpdate();
            return result;
        }

        /// <summary>Calculates value based on the UI component.</summary>
        /// <param name="val">The value to calculate with rule enum and a parameter.</param>
        /// <param name="self">The UI component.</param>
        /// <returns>The calculated value.</returns>
        private float Calculate(UValue val, UIComponent self) {
            switch (val.Rule) {
                case URule.Ignore:
                    return 0f;

                case URule.FixedSize:
                    return val.Value * UIScaler.UIScale;

                case URule.FractionScreenWidth:
                    return UIScaler.MaxWidth * val.Value * UIScaler.UIScale;

                case URule.MultipleOfWidth:
                    return self.width * val.Value * UIScaler.UIScale;

                case URule.FractionScreenHeight:
                    return UIScaler.MaxHeight * val.Value * UIScaler.UIScale;

                case URule.MultipleOfHeight:
                    return self.height * val.Value * UIScaler.UIScale;

                case URule.FitChildrenWidth:
                    // If there's children controls, take their width + right padding
                    return this.ChildrenBox.Width > 0
                               ? this.ChildrenBox.B.x + this.Config.Padding.Right
                               : 0f;

                case URule.FitChildrenHeight:
                    // If there's children controls, take their height + bottom padding
                    return this.ChildrenBox.Height > 0
                               ? this.ChildrenBox.B.y + this.Config.Padding.Bottom
                               : 0f;
            }

            throw new ArgumentOutOfRangeException();
        }

        /// <summary>Set width based on various rules.</summary>
        /// <param name="val">The rule to be used.</param>
        public void Width(UValue val) {
            if (this.Control.gameObject.activeSelf == false) {
                return; // do nothing on destroyed controls
            }

            Control.width = this.Calculate(val, Control);
        }

        /// <summary>Set height based on various rules.</summary>
        /// <param name="val">The rule to be used.</param>
        public void Height(UValue val) {
            if (this.Control.gameObject.activeSelf == false) {
                return; // do nothing on destroyed controls
            }

            Control.height = this.Calculate(val, Control);
        }

        /// <summary>Automatically defines control size to wrap around child controls.</summary>
        public void FitToChildren() {
            if (this.Control.gameObject.activeSelf == false) {
                return; // do nothing on destroyed controls
            }

            // value 0f is ignored, the padding is to be set in UResizerConfig
            this.Width(new UValue(URule.FitChildrenWidth));
            this.Height(new UValue(URule.FitChildrenHeight));
        }

        /// <summary>Instructs U UI to place the control vertically below the previous sibling.</summary>
        /// <param name="spacing">Step away from the control above but not from the form top.</param>
        /// <param name="stackRef">A sibling to use as reference (otherwise the previous sibling).</param>
        public void Stack(UStackMode mode,
                          float spacing = 0f,
                          UIComponent stackRef = null) {
            if (this.Control.gameObject.activeSelf == false) {
                return; // do nothing on destroyed controls
            }

            UPadding padding =
                this.Control.parent.GetComponent<UIComponent>() is ISmartSizableControl parent
                    ? parent.GetResizerConfig().Padding
                    : UPadding.Zero;

            // Stack reference: either what user has provided, or the previous sibling
            if (stackRef == null) {
                stackRef = this.PreviousSibling;
            }

            switch (mode) {
                case UStackMode.Below: {
                    this.Control.relativePosition =
                        stackRef == null
                            ? new Vector3(padding.Left, padding.Top, 0f)
                            : stackRef.relativePosition + new Vector3(
                                  x: 0f,
                                  y: stackRef.height + spacing,
                                  z: 0f);
                    return;
                }
                // case UStackMode.Above: {
                //     break;
                // }
                case UStackMode.ToTheRight: {
                    this.Control.relativePosition =
                        stackRef == null
                        ? new Vector3(padding.Left, padding.Top, 0f)
                        : stackRef.relativePosition + new Vector3(
                              x: stackRef.width + spacing,
                              y: 0f,
                              z: 0f);
                    return;
                }
                // case UStackMode.ToTheLeft: {
                //     break;
                // }
                case UStackMode.NewRowBelow: {
                    this.Control.relativePosition =
                        stackRef == null
                            ? new Vector3(padding.Left, padding.Top, 0f)
                            : new Vector3(
                                x: padding.Left,
                                y: stackRef.relativePosition.y + stackRef.height + spacing,
                                z: 0f);
                    return;
                }
                default: {
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, "Stack mode not supported");
                }
            }
        }

        public void MoveBy(Vector2 offset) {
            if (this.Control.gameObject.activeSelf == false) {
                return; // do nothing on destroyed controls
            }

            Vector3 pos = this.Control.relativePosition;
            this.Control.relativePosition = pos + new Vector3(offset.x, offset.y);
        }
    }
}