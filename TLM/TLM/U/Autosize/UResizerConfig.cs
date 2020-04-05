namespace TrafficManager.U.Autosize {
    using System;
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using JetBrains.Annotations;

    /// <summary>Stores callback info for a resizable control.</summary>
    public class UResizerConfig {
        /// <summary>
        /// Handler is called at control creation and also when resize is required due to screen
        /// resolution or UI scale change. Can be null, in that case the size is preserved.
        /// </summary>
        [CanBeNull]
        private Action<UResizer> onResize_;

        /// <summary>
        /// Distance from control border to child controls, also affects autosizing.
        /// </summary>
        public float Padding;

        /// <summary>
        /// Set this to false, then the control will be positioned but will not contribute to
        /// parent's all children bounding box. Useful for controls hanging outside of the parent.
        /// </summary>
        public bool ContributeToBoundingBox { get; set; }

        public UResizerConfig() {
            onResize_ = null;
            ContributeToBoundingBox = true;
            Padding = 0f;
        }

        public static UResizerConfig From(UIComponent control) {
            return (control as ISmartSizableControl)?.GetResizerConfig();
        }

        /// <summary>Calls <see cref="onResize_"/> if it is not null.</summary>
        /// <param name="control">The control which is to be refreshed.</param>
        /// <param name="previousSibling">Previous sibling if exists, for control stacking.</param>
        /// <param name="childrenBox">The bounding box of all children of that control.</param>
        /// <returns>Updated box for that control.</returns>
        public static UBoundingBox? CallOnResize([NotNull] UIComponent control,
                                                 [CanBeNull] UIComponent previousSibling,
                                                 UBoundingBox childrenBox) {
            if (control is ISmartSizableControl currentAsResizable) {
                UResizerConfig resizerConfig = currentAsResizable.GetResizerConfig();
                currentAsResizable.OnBeforeResizerUpdate();

                if (resizerConfig.onResize_ != null) {
                    // Create helper UResizer and run it
                    UResizer resizer = new UResizer(
                        control,
                        resizerConfig,
                        previousSibling,
                        childrenBox);
                    try {
                        resizerConfig.onResize_(resizer);
                    }
                    catch (Exception e) {
                        Log.Error($"While calling OnResize on {control.name}: {e}");
                    }
                }

                currentAsResizable.OnAfterResizerUpdate();

                if (!resizerConfig.ContributeToBoundingBox) {
                    return null;
                }
            } else {
                Log._Debug("CallOnResize for a non-ISmartSizableControl");
            }
            return new UBoundingBox(control);
        }

        public void SetResizeFunction(Action<UResizer> resizeFn) {
            this.onResize_ = resizeFn;
        }
    }
}