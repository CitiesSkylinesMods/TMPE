namespace TrafficManager.U {
    using System;
    using System.Collections.Generic;
    using CSUtil.Commons;
    using UnityEngine;
    using UnityEngine.UI;

    /// <summary>
    /// Implements a component for a Canvas gameobject, which applies constraints to its size and position.
    /// </summary>
    public class UConstrainer : MonoBehaviour {
//        private readonly List<UConstraint> constraints_ = new List<UConstraint>();

        /// <summary>
        /// Contains constraints one per constraint type
        /// </summary>
        private readonly Dictionary<TransformField, UConstraint> uniqueConstraints_ =
            new Dictionary<TransformField, UConstraint>();

        public UConstrainer AddConstraint(UConstraint c) {
            this.uniqueConstraints_.Add(c.field_, c);
            return this;
        }

        public UConstrainer SetLeft(float value, Unit unit) {
            return AddConstraint(new UConstraint(TransformField.Left, value, unit));
        }

        public UConstrainer SetRight(float value, Unit unit) {
            return AddConstraint(new UConstraint(TransformField.Right, value, unit));
        }

        public UConstrainer SetWidth(float value, Unit unit) {
            return AddConstraint(new UConstraint(TransformField.Width, value, unit));
        }

        public UConstrainer SetTop(float value, Unit unit) {
            return AddConstraint(new UConstraint(TransformField.Top, value, unit));
        }

        public UConstrainer SetBottom(float value, Unit unit) {
            return AddConstraint(new UConstraint(TransformField.Bottom, value, unit));
        }

        public UConstrainer SetHeight(float value, Unit unit) {
            return AddConstraint(new UConstraint(TransformField.Height, value, unit));
        }

        /// <summary>
        /// Applies constraints to this control. Call this repeatedly for nested controls.
        /// </summary>
        public void ApplyConstraints(RectTransform rt) {
            if (this.uniqueConstraints_.Count <= 0) {
                return;
            }

            // Do not allow RectTransform do the scaling, unhook from the parent size
            rt.anchoredPosition = Vector2.zero;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;

            foreach (KeyValuePair<TransformField, UConstraint> kvPair in this.uniqueConstraints_) {
                kvPair.Value.Apply(rt);
            }
        }

        public void ApplyConstraints() {
            void ApplyRecursive(GameObject obj) {
                var rectTr = obj.GetComponent<RectTransform>();
                var constrainer = obj.GetComponent<UConstrainer>();

                if ((constrainer != null) && (rectTr != null)) {
                    constrainer.ApplyConstraints(rectTr);
                }

                foreach (Transform child in obj.transform) {
                    ApplyRecursive(child.gameObject);
                }
            }

            ApplyRecursive(this.gameObject);
        }

        public UConstraint GetConstraint(TransformField field) {
            return this.uniqueConstraints_[field];
        }

        /// <summary>
        /// See https://docs.unity3d.com/Manual/script-LayoutElement.html for preferred, minimum and
        /// flexible sizes
        /// </summary>
        /// <param name="val">Value to set</param>
        /// <returns>This</returns>
        public UConstrainer PreferredHeight(float val) {
            var layoutElement = this.gameObject.GetComponent<LayoutElement>();
            Log._Assert(layoutElement != null, "A UControl must have a LayoutElement component in parent"); 
            
            layoutElement.minHeight = val;
            layoutElement.preferredHeight = val;
            return this;
        }
    }
}