namespace TrafficManager.Patch._DefaultTool {
    using Harmony;
    using JetBrains.Annotations;
    using TrafficManager.UI;
    using static TrafficManager.UI.SubTools.PrioritySignsTool;

    //[HarmonyPatch(typeof(DefaultTool), "RenderOverlay")]
    [UsedImplicitly]
    public static class RenderOverlayPatch {
        /// <summary>
        /// Renders mass edit overlay even when traffic manager is not current tool.
        /// </summary>
        [HarmonyPostfix]
        [UsedImplicitly]
        public static void Postfix(RenderManager.CameraInfo cameraInfo) {
            if (MassEditOVerlay.IsActive) {
                var tmTool = ModUI.GetTrafficManagerTool(true);
                if(ToolsModifierControl.toolController.CurrentTool != tmTool) {
                    tmTool.RenderOverlay(cameraInfo);
                }
            }
        }
    }
}