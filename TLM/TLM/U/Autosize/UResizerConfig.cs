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

        public UResizerConfig() {
            onResize_ = null;
            Padding = 0f;
        }

        /// <summary>Calls <see cref="onResize_"/> if it is not null.</summary>
        /// <param name="control">The control which is to be refreshed.</param>
        /// <param name="previousSibling">Previous sibling if exists, for control stacking.</param>
        /// <param name="childrenBox">The bounding box of all children of that control.</param>
        /// <returns>Updated box for that control.</returns>
        public static UBoundingBox CallOnResize([NotNull] UIComponent control,
                                                [CanBeNull] UIComponent previousSibling,
                                                UBoundingBox childrenBox) {
            if (control is ISmartSizableControl currentAsResizable) {
                UResizerConfig resizerConfig = currentAsResizable.GetResizerConfig();
                currentAsResizable.OnResizerUpdate();

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