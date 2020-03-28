namespace TrafficManager.U.Autosize {
    using ColossalFramework.UI;
    using CSUtil.Commons;
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

        /// <summary>Calculated bounding box for child controls.</summary>
        public UBoundingBox ChildrenBox;

        public UResizer(UIComponent control, UBoundingBox childrenBox) {
            Control = control;
            ChildrenBox = childrenBox;
        }

        /// <summary>
        /// Descends recursively into component children to calculate their sizes and positions.
        /// For each control: OnResize is called, and then its bounding box is joined with sibling
        /// boxes. Then the resulting box is passed one level up to the parent control.
        /// </summary>
        /// <param name="current">The current component.</param>
        public static void UpdateHierarchy(UIComponent current) {
            UBoundingBox childrenBox = UpdateControlRecursive(current);
            UBoundingBox currentBox = UResizerConfig.CallOnResize(current, childrenBox);

            current.position = currentBox.A;
            current.size = currentBox.Size;
        }

        /// <returns>The bounding box.</returns>
        private static UBoundingBox UpdateControlRecursive(UIComponent current) {
            // Create an empty bounding box update it with all children bounding boxes
            UBoundingBox allChildrenBox = default;

            // For all children visit their resize functions and update allChildrenBox
            for (int i = 0; i < current.transform.childCount - 1; i++) {
                Transform child = current.transform.GetChild(i);
                UIComponent childUiComponent = child.gameObject.GetComponent<UIComponent>();
                UBoundingBox childBox = UpdateControlRecursive(childUiComponent);
                allChildrenBox.ExpandToFit(childBox);
            }

            UBoundingBox currentBox = UResizerConfig.CallOnResize(current, allChildrenBox);
            // current.position = CalculateControlPosition(current, current.position);
            // current.size = CalculateControlSize(current, current.size, allChildrenBox.Size);
            Log._Debug($"UpdateControlRec: {current.name} currentBox={currentBox} pos={current.position} size={current.size}");
            return currentBox;
        }

        private Vector2 CalculateControlSize(UIComponent self,
                                             Vector2 size,
                                             Vector2 allChildrenBox) {
            switch (this.Width.Rule) {
                case URule.Ignore:
                    break;
                case URule.FitChildrenWidth:
                    size.x = allChildrenBox.x;
                    break;
                default:
                    size.x = this.Width.Calculate(self);
                    break;
            }

            switch (this.Height.Rule) {
                case URule.Ignore:
                    break;
                case URule.FitChildrenHeight:
                    size.y = allChildrenBox.y;
                    break;
                default:
                    size.y = this.Height.Calculate(self);
                    break;
            }

            return size;
        }

        private Vector3 CalculateControlPosition(UIComponent self, Vector3 pos) {
            switch (this.Left.Rule) {
                case URule.Ignore:
                    break;
                default:
                    pos.x = this.Left.Calculate(self);
                    break;
            }

            switch (this.Top.Rule) {
                case URule.Ignore:
                    break;
                default:
                    pos.y = this.Top.Calculate(self);
                    break;
            }

            return pos;
        }

        public UResizer Width(UValue val) {
            Control.width = val.Calculate(Control);
            return this;
        }

        public UResizer Height(UValue val) {
            Control.height = val.Calculate(Control);
            return this;
        }
    }
}