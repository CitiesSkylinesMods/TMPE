namespace TrafficManager.Patch._DefaultTool {
    using Harmony;
    using JetBrains.Annotations;
    using TrafficManager.UI;
    using UnityEngine;
    using static TrafficManager.UI.SubTools.PrioritySignsTool;

    [HarmonyPatch(typeof(DefaultTool), "OnToolGUI")]
    [UsedImplicitly]
    public static class OnToolGUIPatch {
        /// <summary>
        /// Renders mass edit overlay even when traffic manager is not current tool.
        /// </summary>
        [HarmonyPostfix]
        [UsedImplicitly]
        public static void Postfix(Event e) {
            if (MassEditOVerlay.IsActive) {
                var tmTool = ModUI.GetTrafficManagerTool(true);
                if(ToolsModifierControl.toolController.CurrentTool != tmTool) {
                    tmTool.CallOnToolGUI(e);
                }
            }
        }
    }
}