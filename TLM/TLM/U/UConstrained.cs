namespace TrafficManager.U {
    using System;
    using System.Collections.Generic;
    using CSUtil.Commons;
    using UnityEngine;
    using UnityEngine.UI;

    /// <summary>
    /// Implements a component for a Canvas gameobject, which applies constraints to its size and position.
    /// </summary>
    public class UConstrained : MonoBehaviour {
        private readonly List<UConstraint> constraints_ = new List<UConstraint>();

        public UConstrained AddConstraint(UConstraint c) {
            this.constraints_.Add(c);
            return this;
        }

        public UConstrained SetLeft(float value, Unit unit) {
            return AddConstraint(new UConstraint(TransformField.Left, value, unit));
        }

        public UConstrained SetRight(float value, Unit unit) {
            return AddConstraint(new UConstraint(TransformField.Right, value, unit));
        }

        public UConstrained SetWidth(float value, Unit unit) {
            return AddConstraint(new UConstraint(TransformField.Width, value, unit));
        }

        public UConstrained SetTop(float value, Unit unit) {
            return AddConstraint(new UConstraint(TransformField.Top, value, unit));
        }

        public UConstrained SetBottom(float value, Unit unit) {
            return AddConstraint(new UConstraint(TransformField.Bottom, value, unit));
        }

        public UConstrained SetHeight(float value, Unit unit) {
            return AddConstraint(new UConstraint(TransformField.Height, value, unit));
        }

        /// <summary>
        /// Applies constraints to this control. Call this repeatedly for nested controls.
        /// </summary>
        public void ApplyConstraints(RectTransform rt) {
            if (this.constraints_.Count <= 0) {
                return;
            }

            // Do not allow RectTransform do the scaling, unhook from the parent size
            rt.anchoredPosition = Vector2.zero;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;

            foreach (UConstraint c in this.constraints_) {
                c.Apply(rt);
            }
        }

        public void ApplyConstraints() {
            // Log._Assert(this.rootObject_ != null, "Must create rootObject before applying constraints");

            void ApplyRecursive(GameObject obj) {
                var rectTr = obj.GetComponent<RectTransform>();
                var control = obj.GetComponent<UConstrained>();

                if ((control != null) && (rectTr != null)) {
                    control.ApplyConstraints(rectTr);
                }

                foreach (Transform child in obj.transform) {
                    ApplyRecursive(child.gameObject);
                }
            }

            // Root form will be inherited from UControl instead of containing a component UControl,
            // so it will not be matched in `GetComponent<UControl>` above.
            // ApplyConstraints(this.rootObject_.GetComponent<RectTransform>());
            ApplyRecursive(this.gameObject);
        }

        public void ForEachConstraintModify(Action<UConstraint> forEach) {
            foreach (UConstraint c in this.constraints_) {
                forEach(c);
            }
        }

        /// <summary>
        /// See https://docs.unity3d.com/Manual/script-LayoutElement.html for preferred, minimum and
        /// flexible sizes
        /// </summary>
        /// <param name="val">Value to set</param>
        /// <returns>This</returns>
        public UConstrained PreferredHeight(float val) {
            var layoutElement = this.gameObject.GetComponent<LayoutElement>();
            Log._Assert(layoutElement != null, "A UControl must have a LayoutElement component in parent"); 
            
            layoutElement.minHeight = val;
            layoutElement.preferredHeight = val;
            return this;
        }
    }
}