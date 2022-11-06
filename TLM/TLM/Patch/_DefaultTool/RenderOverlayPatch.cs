namespace TrafficManager.Patch._DefaultTool {
    using HarmonyLib;
    using JetBrains.Annotations;
    using TrafficManager.UI;
    using TrafficManager.Lifecycle;
    using ColossalFramework.Math;
    using System.Collections.Generic;
    using UnityEngine;
    using ColossalFramework;
    using System;

    [HarmonyPatch(typeof(DefaultTool), "RenderOverlay")]
    [UsedImplicitly]
    public static class RenderOverlayPatch {
        /// <summary>
        /// key : id
        /// value : render action
        /// </summary>
        public static Dictionary<int, Action<RenderManager.CameraInfo>> Actions = new();

        public static void RenderDebugOverlay(this Bezier3 bezier, RenderManager.CameraInfo cameraInfo, Color color) {
            float y = bezier.a.y;
            RenderManager.instance.OverlayEffect.DrawBezier(
                        cameraInfo: cameraInfo,
                        color: color,
                        bezier: bezier,
                        size: 1,
                        cutStart: 0,
                        cutEnd: 0,
                        minY: y - 5,
                        maxY: y + 5,
                        renderLimits: true,
                        alphaBlend: true);
        }

        public static void RenderDebugOverlay(this Bezier3 bezier, RenderManager.CameraInfo cameraInfo, Color color, float makredOffset) {
            float y = bezier.a.y;
            RenderManager.instance.OverlayEffect.DrawBezier(
                        cameraInfo: cameraInfo,
                        color: color,
                        bezier: bezier,
                        size: 1,
                        cutStart: 0,
                        cutEnd: 0,
                        minY: y - 5,
                        maxY: y + 5,
                        renderLimits: true,
                        alphaBlend: true);
            Vector3 pos = bezier.Position(makredOffset);
            pos.RenderDebugOverlay(cameraInfo, color);
        }

        public static void RenderDebugOverlay(this Vector3 pos, RenderManager.CameraInfo cameraInfo, Color color) {
            RenderManager.instance.OverlayEffect.DrawCircle(
                        cameraInfo: cameraInfo,
                        color: color,
                        center: pos,
                        size: 2,
                        minY: pos.y - 5,
                        maxY: pos.y + 5,
                        renderLimits: true,
                        alphaBlend: true);
        }

        /// <summary>
        /// Renders mass edit overlay even when traffic manager is not current tool.
        /// </summary>
        [HarmonyPostfix]
        [UsedImplicitly]
        public static void Postfix(RenderManager.CameraInfo cameraInfo) {
#if DEBUG
            foreach(var action in Actions.Values) {
                action(cameraInfo);
            }
#endif
            if (TMPELifecycle.PlayMode && !TrafficManagerTool.IsCurrentTool) {
                if (UI.SubTools.PrioritySigns.MassEditOverlay.IsActive) {
                    ModUI.GetTrafficManagerTool()?.RenderOverlayImpl(cameraInfo);
                }
                RoadSelectionPanels.Root.RenderOverlay();
            }
        }
    }
}