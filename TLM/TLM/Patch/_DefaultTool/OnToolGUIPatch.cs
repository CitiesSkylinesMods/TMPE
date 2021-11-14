namespace TrafficManager.Patch._DefaultTool {
    using HarmonyLib;
    using JetBrains.Annotations;
    using TrafficManager.UI;
    using UnityEngine;
    using TrafficManager.Lifecycle;
    [HarmonyPatch(typeof(DefaultTool), "OnToolGUI")]
    [UsedImplicitly]
    public static class OnToolGUIPatch {
        /// <summary>
        /// Renders mass edit overlay even when traffic manager is not current tool.
        /// </summary>
        [HarmonyPostfix]
        [UsedImplicitly]
        public static void Postfix(Event e) {
            if (TMPELifecycle.PlayMode && !TrafficManagerTool.IsCurrentTool) {
                if (UI.SubTools.PrioritySigns.MassEditOverlay.IsActive) {
                    ModUI.
                        GetTrafficManagerTool()?.OnToolGUIImpl(e);
                }
            }
        }
    }
}