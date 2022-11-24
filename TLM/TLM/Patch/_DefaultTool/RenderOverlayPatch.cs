namespace TrafficManager.Patch._DefaultTool {
    using HarmonyLib;
    using JetBrains.Annotations;
    using TrafficManager.UI;
    using TrafficManager.Lifecycle;
    using TrafficManager.UI.Helpers;

    [HarmonyPatch(typeof(DefaultTool), "RenderOverlay")]
    [UsedImplicitly]
    public static class RenderOverlayPatch {
        /// <summary>
        /// Renders mass edit overlay even when traffic manager is not current tool.
        /// </summary>
        [HarmonyPostfix]
        [UsedImplicitly]
        public static void Postfix(RenderManager.CameraInfo cameraInfo) {
            DebugOverlay.RenderOverlay(cameraInfo);
            if (TMPELifecycle.PlayMode && !TrafficManagerTool.IsCurrentTool) {
                if (UI.SubTools.PrioritySigns.MassEditOverlay.IsActive) {
                    ModUI.GetTrafficManagerTool()?.RenderOverlayImpl(cameraInfo);
                }
                RoadSelectionPanels.Root.RenderOverlay();
            }
        }
    }
}