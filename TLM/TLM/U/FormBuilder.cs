namespace TrafficManager.U {
    using System;
    using Controls;
    using UnityEngine;
    using UnityEngine.UI;

    /// <summary>
    /// Handles creation of the form tree.
    /// </summary>
    public class FormBuilder {
        private readonly UWindow window_;
        private readonly GameObject scope_;

        private FormBuilder(UWindow window, GameObject scope) {
            this.window_ = window;
            this.scope_ = scope;
        }

        /// <summary>
        /// Create form builder with an empty form
        /// </summary>
        /// <param name="formName">Form name in Unity Scene and in UIView (same name)</param>
        /// <param name="pos">Position</param>
        /// <param name="size">Size</param>
        /// <returns>new FormBuilder</returns>
        public static FormBuilder Create(string formName) {
            var form = new UWindow(formName);
            return new FormBuilder(form, null);
        }

        /// <summary>
        /// Populate the form with controls. Nesting is created by calling Populate on controls.
        /// </summary>
        /// <param name="addControlsFn">Function to populate the controls</param>
        /// <returns>The form ready to use</returns>
        public UWindow Populate(Action<FormBuilder> addControlsFn) {
            addControlsFn(this);
            return this.window_;
        }

        /// <summary>
        /// Add a panel to the form. Position is set automatically by the active layout group.
        /// </summary>
        /// <param name="parent">Parent to attach to</param>
        /// <param name="size">Size (may be modified by the layout group)</param>
        /// <returns>New panel</returns>
        public UPanel Panel() {
            return UPanel.Create(this.scope_ == null ? this.window_.rootObject_ : this.scope_,
                                 this.window_,
                                 $"Panel{this.window_.panelCounter_++}");
        }

        public CanvasText Text(string text) {
            return CanvasText.Create(this.scope_ == null ? this.window_.rootObject_ : this.scope_,
                                     $"Text{this.window_.textCounter_++}",
                                     text);
        }

        public CanvasButton Button(string text) {
            return CanvasButton.Create(this.scope_ == null ? this.window_.rootObject_ : this.scope_,
                                       $"Button{this.window_.buttonCounter_++}",
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
                this.scope_ == null ? this.window_.rootObject_.transform : this.scope_.transform,
                false);
            return new FormBuilder(this.window_, groupObject);
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
                this.scope_ == null ? this.window_.rootObject_.transform : this.scope_.transform,
                false);
            return new FormBuilder(this.window_, groupObject);
        }
    }
}