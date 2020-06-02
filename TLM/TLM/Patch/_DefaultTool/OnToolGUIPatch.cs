namespace TrafficManager.Patch._DefaultTool {
    using HarmonyLib;
    using JetBrains.Annotations;
    using TrafficManager.UI;
    using UnityEngine;

    [HarmonyPatch(typeof(DefaultTool), "OnToolGUI")]
    [UsedImplicitly]
    public static class OnToolGUIPatch {
        /// <summary>
        /// Renders mass edit overlay even when traffic manager is not current tool.
        /// </summary>
        [HarmonyPostfix]
        [UsedImplicitly]
        public static void Postfix(Event e) {
            if(ToolsModifierControl.toolController.CurrentTool.GetType() != typeof(TrafficManagerTool)) {
                if (UI.SubTools.PrioritySigns.MassEditOverlay.IsActive) {
                    ModUI.GetTrafficManagerTool(true).OnToolGUIImpl(e);
                }
            }
        }
    }
}