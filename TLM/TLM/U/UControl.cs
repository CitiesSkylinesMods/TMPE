namespace TrafficManager.U {
    using System;
    using System.Collections.Generic;
    using UnityEngine;

    public abstract class UControl
        : MonoBehaviour 
    {
        private readonly List<UConstraint> constraints_ = new List<UConstraint>();

        public UControl AddConstraint(UConstraint c) {
            this.constraints_.Add(c);
            return this;
        }

        public UControl SetLeft(float value, Unit unit) {
            return AddConstraint(new UConstraint(TransformField.Left, value, unit));
        }

        public UControl SetRight(float value, Unit unit) {
            return AddConstraint(new UConstraint(TransformField.Right, value, unit));
        }

        public UControl SetWidth(float value, Unit unit) {
            return AddConstraint(new UConstraint(TransformField.Width, value, unit));
        }

        public UControl SetTop(float value, Unit unit) {
            return AddConstraint(new UConstraint(TransformField.Top, value, unit));
        }

        public UControl SetBottom(float value, Unit unit) {
            return AddConstraint(new UConstraint(TransformField.Bottom, value, unit));
        }

        public UControl SetHeight(float value, Unit unit) {
            return AddConstraint(new UConstraint(TransformField.Height, value, unit));
        }

        /// <summary>
        /// Applies constraints to this control. Call this repeatedly for nested controls.
        /// </summary>
        public void ApplyConstraints(RectTransform rt) {
            // Do not allow RectTransform do the scaling, unhook from the parent size
            rt.anchoredPosition = Vector2.zero;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;

            foreach (UConstraint c in this.constraints_) {
                c.Apply(rt);
            }
        }

        public abstract void ApplyConstraints();

        public void ForEachConstraintModify(Action<UConstraint> forEach) {
            foreach (UConstraint c in this.constraints_) {
                forEach(c);
            }
        }
    }
}