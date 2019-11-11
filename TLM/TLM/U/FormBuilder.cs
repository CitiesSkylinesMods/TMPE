namespace TrafficManager.U {
    using System;
    using Controls;
    using CSUtil.Commons;
    using JetBrains.Annotations;
    using UnityEngine;
    using UnityEngine.UI;

    /// <summary>
    /// Handles creation of the form tree. Type template T is the control which is being created with FormBuilder.
    /// </summary>
    /// <typeparam name="T">Behaviour of a Canvas gameobject that characterizes the current window element</typeparam>
    public class FormBuilder<T>
        where T : MonoBehaviour {
        /// <summary>
        /// Root UWindow attached to a Unity Canvas
        /// </summary>
        private readonly UWindow window_;

        private FormBuilder(UWindow root, [NotNull] GameObject currentControl) {
            var uConstrained = currentControl.GetComponent<UConstrained>();
            Log._Debug($"Creating formbuilder<{typeof(T)}>'{currentControl.name}' uconstr={uConstrained != null}");
            this.window_ = root;
            this.currentControl_ = currentControl;
        }

        /// <summary>
        /// The control owned by this form builder, also the root of the form tree for this builder. Creating more
        /// nested controls will create a new form builder with deeper controls as <see cref="currentControl_"/>.
        /// </summary>
        private GameObject currentControl_ { get; }

        /// <summary>
        /// Create form builder with an empty form
        /// </summary>
        /// <param name="formName">Form name in Unity Scene and in UIView (same name)</param>
        /// <returns>new FormBuilder</returns>
        public static FormBuilder<UPanel> CreateWindow(string formName) {
            GameObject rootCanvas = UWindow.CreateCanvasObject(formName);
            var rootWindow = rootCanvas.GetComponent<UWindow>();
            Log._Assert(rootWindow != null,
                        "Creating Canvas Object for FormBuilder failed, rootWindow is null");

            var rootPanelBuilder = new FormBuilder<UWindow>(rootWindow, rootCanvas);
            return rootPanelBuilder
                   .Panel()
                   .VerticalLayout();
        }

        /// <summary>
        /// Add a panel to the form. Position is set automatically by the active layout group.
        /// </summary>
        /// <returns>New nested formbuilder targeting the new panel</returns>
        public FormBuilder<UPanel> Panel() {
            GameObject parentObject = this.currentControl_ == null
                                          ? this.window_.gameObject
                                          : this.currentControl_;
            GameObject newPanel = UPanel.Create(this.window_,
                                                parentObject,
                                                $"Panel{this.window_.panelCounter_++}");
            return new FormBuilder<UPanel>(this.window_, newPanel);
        }

        public FormBuilder<UText> Text(string text) {
            GameObject parentObject = this.currentControl_ == null
                                          ? this.window_.gameObject
                                          : this.currentControl_;
            GameObject newText = UText.Create(parentObject,
                                              $"Text{this.window_.textCounter_++}",
                                              text);
            return new FormBuilder<UText>(this.window_, newText);
        }

        public FormBuilder<UButton> Button(string text) {
            GameObject parentObject = this.currentControl_ == null
                                          ? this.window_.gameObject
                                          : this.currentControl_;
            UButton newButton = UButton.Construct(parentObject,
                                               $"Button{this.window_.buttonCounter_++}",
                                               text);
            return new FormBuilder<UButton>(this.window_, newButton.gameObject);
        }

        /// <summary>
        /// Create a vertical layout group which arranges children top to bottom, no new control is added just
        /// a component is added to the current control.
        /// </summary>
        /// <returns>The group if you ever need it</returns>
        public FormBuilder<T> VerticalLayout() {
            this.currentControl_.AddComponent<VerticalLayoutGroup>();
            return this;
        }

        /// <summary>
        /// Create a vertical layout group which arranges children top to bottom, no new control is added just
        /// a component is added to the current control.
        /// </summary>
        /// <returns>The group if you ever need it</returns>
        public FormBuilder<T> HorizontalLayout() {
            this.currentControl_.AddComponent<HorizontalLayoutGroup>();
            return this;
        }

        public T Get() {
            var component = this.currentControl_.GetComponent<T>();
            Log._Assert(component != null,
                        $"FormBuilder<{typeof(T)}>'{this.currentControl_.name}' without that component");
            return component;
        }

        public UConstrained GetUConstrained() {
            var constrainedComponent = this.currentControl_.GetComponent<UConstrained>();
            Log._Assert(constrainedComponent != null,
                        $"FormBuilder<{typeof(T)}>'{this.currentControl_.name}' without a UConstrained component");
            return constrainedComponent;
        }

        public UWindow GetUWindow() {
            return this.window_;
        }
    }
}