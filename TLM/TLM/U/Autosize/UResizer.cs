namespace TrafficManager.U.Autosize {
    using System;
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using JetBrains.Annotations;
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

        public UResizer(UIComponent control,
                        [CanBeNull]
                        UIComponent previousSibling,
                        UBoundingBox childrenBox) {
            Control = control;
            ChildrenBox = childrenBox;
            PreviousSibling = previousSibling;
        }

        // /// <summary>
        // /// Descends recursively into component children to calculate their sizes and positions.
        // /// For each control: OnResize is called, and then its bounding box is joined with sibling
        // /// boxes. Then the resulting box is passed one level up to the parent control.
        // /// </summary>
        // /// <param name="current">The current component.</param>
        // public static void UpdateHierarchy(UIComponent current) {
        //     UBoundingBox childrenBox = UpdateControlRecursive(current, null);
        //     UResizerConfig.CallOnResize(current, null, childrenBox);
        //
        //     // current.relativePosition = currentBox.A;
        //     // current.size = currentBox.Size;
        // }

        /// <summary>
        /// Recursively descends down the GUI controls tree and calls OnResize on <see cref="ISmartSizableControl"/>
        /// implementors, then allows parents to adjust their size to the contents and so on.
        /// </summary>
        /// <returns>The bounding box.</returns>
        /// <param name="current">The control being updated.</param>
        /// <param name="previousSibling">If not null, points to previous sibling for control stacking.</param>
        internal static UBoundingBox UpdateControlRecursive([NotNull] UIComponent current,
                                                            [CanBeNull] UIComponent previousSibling) {
            Log._Debug($"before UpdateControlRec: {current.name}");

            // Create an empty bounding box update it with all children bounding boxes
            UBoundingBox allChildrenBox = default;

            // For all children visit their resize functions and update allChildrenBox
            UIComponent previousChild = null;
            for (int i = 0; i < current.transform.childCount; i++) {
                Transform child = current.transform.GetChild(i);
                UIComponent childUiComponent = child.gameObject.GetComponent<UIComponent>();
                UBoundingBox childBox = UpdateControlRecursive(
                    childUiComponent,
                    previousChild);
                allChildrenBox.ExpandToFit(childBox);

                previousChild = childUiComponent;
            }

            UBoundingBox currentBox = UResizerConfig.CallOnResize(
                current,
                previousSibling,
                allChildrenBox);
            Log._Debug(
                $"after UpdateControlRec: {current.name} currentBox={currentBox} "
                + $"relPos={current.relativePosition} size={current.size}");
            return currentBox;
        }

        /// <summary>Calculates value based on the UI component.</summary>
        /// <param name="self">The UI component.</param>
        /// <returns>The calculated value.</returns>
        public float Calculate(UValue val, UIComponent self) {
            switch (val.Rule) {
                case URule.Ignore:
                    return 0f;
                case URule.FixedSize:
                    return val.Value;
                case URule.FractionScreenWidth:
                    return Screen.width * val.Value;
                case URule.MultipleOfWidth:
                    return self.width * val.Value;
                case URule.FractionScreenHeight:
                    return Screen.height * val.Value;
                case URule.MultipleOfHeight:
                    return self.height * val.Value;
                case URule.ReferenceWidthAt1080P:
                    return UIScaler.ScreenWidthFraction(val.Value / 1920f);
                case URule.ReferenceHeightAt1080P:
                    return UIScaler.ScreenHeightFraction(val.Value / 1080f);
                case URule.FitChildrenWidth:
                    return this.ChildrenBox.B.x;
                case URule.FitChildrenHeight:
                    return this.ChildrenBox.B.y;
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
            this.Width(new UValue(URule.FitChildrenWidth, 0f));
            this.Height(new UValue(URule.FitChildrenHeight, 0f));
        }

        /// <summary>Instructs U UI to place the control vertically below the previous sibling.</summary>
        public void StackVertical() {
            Vector3 pos = this.PreviousSibling == null
                              ? Vector3.zero
                              : this.PreviousSibling.relativePosition + new Vector3(
                                    0f,
                                    this.PreviousSibling.height,
                                    0f);
            this.Control.relativePosition = pos;
        }

        /// <summary>Instructs U UI to place the control to the right of the previous sibling.</summary>
        public void StackHorizontal() {
            Vector3 pos = this.PreviousSibling == null
                              ? Vector3.zero
                              : this.PreviousSibling.relativePosition + new Vector3(
                                    this.PreviousSibling.width,
                                    0f,
                                    0f);
            this.Control.relativePosition = pos;
        }
    }
}