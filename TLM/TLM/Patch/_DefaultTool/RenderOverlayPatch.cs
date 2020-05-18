namespace TrafficManager.Patch._DefaultTool {
    using HarmonyLib;
    using JetBrains.Annotations;
    using TrafficManager.UI;

    [HarmonyPatch(typeof(DefaultTool), "RenderOverlay")]
    [UsedImplicitly]
    public static class RenderOverlayPatch {
        /// <summary>
        /// Renders mass edit overlay even when traffic manager is not current tool.
        /// </summary>
        [HarmonyPostfix]
        [UsedImplicitly]
        public static void Postfix(RenderManager.CameraInfo cameraInfo) {
            if (ToolsModifierControl.toolController.CurrentTool.GetType() != typeof(TrafficManagerTool)) {
                if (UI.SubTools.PrioritySigns.MassEditOverlay.IsActive) {
                    ModUI.GetTrafficManagerTool(true).RenderOverlayImpl(cameraInfo);
                }
                RoadSelectionPanels.Root.RenderOverlay();
            }

        }
    }
}