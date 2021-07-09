namespace TrafficManager.U.Autosize {
    using System;
    using ColossalFramework.UI;
    using UnityEngine;

    /// <summary>
    /// Hides calls to <see cref="ISmartSizableControl.GetResizerConfig"/> making code shorter.
    /// </summary>
    public static class SmartResizeExtensions {
        public static void ResizeFunction(this ISmartSizableControl ctrl,
                                          Action<UResizer> resizeFn) {
            ctrl.GetResizerConfig().SetResizeFunction(resizeFn);
        }

        public static void SetPadding(this ISmartSizableControl ctrl,
                                      UPadding p) {
            ctrl.GetResizerConfig().Padding = p;
        }

        public static void ContributeToBoundingBox(this ISmartSizableControl ctrl,
                                                   bool c) {
            ctrl.GetResizerConfig().ContributeToBoundingBox = c;
        }

        /// <summary>Instruct the <see cref="UResizer"/> to always use fixed size for the control.</summary>
        /// <param name="size">The size in units of 1080p screen.</param>
        public static void SetFixedSize(this ISmartSizableControl ctrl,
                                        Vector2 size) {
            UResizerConfig c = ctrl.GetResizerConfig();
            c.SizeChoice = USizeChoice.Predefined;
            c.FixedSize = size;
        }

        /// <summary>Instruct the <see cref="UResizer"/> to always use this stacking for the control.</summary>
        /// <param name="mode">The stacking mode to always use.</param>
        /// <param name="spacing">Spacing to use in the call to automatic predefined spacing.</param>
        public static void SetStacking(this ISmartSizableControl ctrl,
                                       UStackMode mode,
                                       float spacing = 0f) {
            UResizerConfig c = ctrl.GetResizerConfig();
            c.StackingChoice = UStackingChoice.Predefined;
            c.Stacking = mode;
            c.StackingSpacing = spacing;
        }

        public static void ForceUpdateLayout(this UIComponent ctrl) {
            UResizer.UpdateControl(ctrl);
        }
    }
}