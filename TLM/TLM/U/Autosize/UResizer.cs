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
            UpdateControlRecursive(control, null);
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
            if (!current || current.transform == null) { // is object valid?
                return default;
            }

            // Create an empty bounding box update it with all children bounding boxes
            UBoundingBox allChildrenBox = default;

            // For all children visit their resize functions and update allChildrenBox
            UIComponent previousChild = null;

            foreach (Transform child in current.transform) {
                UIComponent childUiComponent = child.gameObject.GetComponent<UIComponent>();
                UBoundingBox? childBox = UpdateControlRecursive(
                    childUiComponent,
                    previousChild);

                // if child contributes to our bounding box, we can update our box
                if (childBox != null) {
                    allChildrenBox.ExpandToFit(childBox.Value);
                }

                previousChild = childUiComponent;
            }

            return UResizerConfig.CallOnResize(
                current,
                previousSibling,
                allChildrenBox);
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
                    return val.Value * UIScaler.GetScale();

                case URule.FractionScreenWidth:
                    return UIScaler.GuiWidth * val.Value * UIScaler.GetScale();

                case URule.MultipleOfWidth:
                    return self.width * val.Value * UIScaler.GetScale();

                case URule.FractionScreenHeight:
                    return UIScaler.GuiHeight * val.Value * UIScaler.GetScale();

                case URule.MultipleOfHeight:
                    return self.height * val.Value * UIScaler.GetScale();

                case URule.FitChildrenWidth:
                    return this.ChildrenBox.B.x + this.Config.Padding;

                case URule.FitChildrenHeight:
                    return this.ChildrenBox.B.y + this.Config.Padding;
            }

            throw new ArgumentOutOfRangeException();
        }

        /// <summary>Set width based on various rules.</summary>
        /// <param name="val">The rule to be used.</param>
        public void Width(UValue val) {
            Control.width = this.Calculate(val, Control);
        }

        /// <summary>Set height based on various rules.</summary>
        /// <param name="val">The rule to be used.</param>
        public void Height(UValue val) {
            Control.height = this.Calculate(val, Control);
        }

        /// <summary>Automatically defines control size to wrap around child controls.</summary>
        public void FitToChildren() {
            // value 0f is ignored, the padding is to be set in UResizerConfig
            this.Width(new UValue(URule.FitChildrenWidth));
            this.Height(new UValue(URule.FitChildrenHeight));
        }

        /// <summary>Instructs U UI to place the control vertically below the previous sibling.</summary>
        /// <param name="spacing">Step away from the control above but not from the form top.</param>
        /// <param name="stackUnder">Some sibling whose position will be used (must be
        /// inserted into the form before the current control.</param>
        public void StackVertical(float spacing = 0f, UIComponent stackUnder = null) {
            var padding = 0f;
            if (this.Control.parent.GetComponent<UIComponent>() is ISmartSizableControl parent) {
                padding = parent.GetResizerConfig().Padding;
            }
            if (stackUnder == null) {
                stackUnder = this.PreviousSibling;
            }
            Vector3 pos = stackUnder == null
                              ? new Vector3(padding, padding, 0f)
                              : stackUnder.relativePosition + new Vector3(
                                    0f,
                                    stackUnder.height + spacing,
                                    0f);
            this.Control.relativePosition = pos;
        }

        /// <summary>Stacks vertically but starts a new line (resets X position to 0).</summary>
        /// <param name="spacing">Step down by this many pixels.</param>
        /// <param name="stackUnder">The control to use as a reference.</param>
        public void StackVerticalNewRow(float spacing = 0f, UIComponent stackUnder = null) {
            var padding = 0f;
            if (this.Control.parent.GetComponent<UIComponent>() is ISmartSizableControl parent) {
                padding = parent.GetResizerConfig().Padding;
            }
            if (stackUnder == null) {
                stackUnder = this.PreviousSibling;
            }
            Vector3 pos = stackUnder == null
                              ? new Vector3(padding, padding, 0f)
                              : new Vector3(
                                    padding,
                                    stackUnder.relativePosition.y + stackUnder.height + spacing,
                                    0f);
            this.Control.relativePosition = pos;
        }

        /// <summary>Instructs U UI to place the control to the right of the previous sibling.</summary>
        /// <param name="spacing">Step away from the control to the left but not from the form left.</param>
        /// <param name="stackUnder">Some sibling whose position will be used (must be
        /// inserted into the form before the current control.</param>
        public void StackHorizontal(float spacing = 0f, UIComponent stackUnder = null) {
            var padding = 0f;
            if (this.Control.parent.GetComponent<UIComponent>() is ISmartSizableControl parent) {
                padding = parent.GetResizerConfig().Padding;
            }
            if (stackUnder == null) {
                stackUnder = this.PreviousSibling;
            }
            Vector3 pos = stackUnder == null
                              ? new Vector3(padding, padding, 0f)
                              : stackUnder.relativePosition + new Vector3(
                                    stackUnder.width + spacing,
                                    0f,
                                    0f);
            this.Control.relativePosition = pos;
        }
    }
}