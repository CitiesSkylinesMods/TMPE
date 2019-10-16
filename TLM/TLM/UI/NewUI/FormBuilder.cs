namespace TrafficManager.UI.NewUI {
    using System;
    using Controls;
    using JetBrains.Annotations;
    using UnityEngine;
    using UnityEngine.UI;

    /// <summary>
    /// Handles creation of the form tree.
    /// </summary>
    public class FormBuilder {
        private CanvasForm form_;
        private GameObject scope_;

        private FormBuilder(CanvasForm form, GameObject scope) {
            form_ = form;
            scope_ = scope;
        }

        /// <summary>
        /// Create form builder with an empty form
        /// </summary>
        /// <param name="formName">Form name in Unity Scene and in UIView (same name)</param>
        /// <param name="pos">Position</param>
        /// <param name="size">Size</param>
        /// <param name="addControlsFn">Function to populate the controls</param>
        /// <returns>new FormBuilder</returns>
        public static FormBuilder Create(string formName,
                                         Vector2 pos,
                                         Vector2 size) {
            var form = new CanvasForm(formName, pos, size);
            return new FormBuilder(form, null);
        }

        /// <summary>
        /// Populate the form with controls. Nesting is created by calling Populate on controls.
        /// </summary>
        /// <returns>The form ready to use</returns>
        public CanvasForm Populate(Action<FormBuilder> addControlsFn) {
            addControlsFn(this);
            return form_;
        }

        /// <summary>
        /// Add a panel to the form. Position is set automatically by the active layout group.
        /// </summary>
        /// <param name="parent">Parent to attach to</param>
        /// <param name="size">Size (may be modified by the layout group)</param>
        /// <returns>New panel</returns>
        public CanvasPanel Panel() {
            return CanvasPanel.Create(
                scope_ == null ? form_.rootObject_ : scope_,
                $"Panel{form_.panelCounter_++}");
        }

        public CanvasText Text(string text) {
            return CanvasText.Create(
                scope_ == null ? form_.rootObject_ : scope_,
                $"Text{form_.textCounter_++}",
                text);
        }

        public CanvasButton Button(string text) {
            return CanvasButton.Create(
                scope_ == null ? form_.rootObject_ : scope_,
                $"Button{form_.buttonCounter_++}",
                text);
        }

        /// <summary>
        /// Create a vertical layout group which arranges children top to bottom
        /// </summary>
        /// <param name="groupName">Gameobject name in scene graph</param>
        /// <param name="construct">Lambda which fills the interior with children</param>
        /// <returns>The group if you ever need it</returns>
        public FormBuilder VerticalLayoutGroup(string groupName) {
            var groupObject = new GameObject(groupName);
            groupObject.AddComponent<VerticalLayoutGroup>();
            groupObject.transform.SetParent(
                scope_ == null ? form_.rootObject_.transform : scope_.transform,
                false);
            return new FormBuilder(form_, groupObject);
        }

        /// <summary>
        /// Create a horiz layout group which arranges children left to right
        /// </summary>
        /// <param name="groupName">Gameobject name in scene graph</param>
        /// <param name="construct">Lambda which fills the interior with children</param>
        /// <returns>The group if you ever need it</returns>
        public FormBuilder HorizontalLayoutGroup(string groupName) {
            var groupObject = new GameObject(groupName);
            groupObject.AddComponent<HorizontalLayoutGroup>();
            groupObject.transform.SetParent(
                scope_ == null ? form_.rootObject_.transform : scope_.transform,
                false);
            return new FormBuilder(form_, groupObject);
        }
    }
}