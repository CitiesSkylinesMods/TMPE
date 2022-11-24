namespace TrafficManager.UI.Helpers; 
using ColossalFramework.Math;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Diagnostics;

internal static class DebugOverlay {
    /// <summary>
    /// key : id
    /// value : render action
    /// </summary>
    public static Dictionary<int, Action> Actions;

#if DEBUG
    private static RenderManager.CameraInfo cameraInfo_;

    static DebugOverlay() => Actions = new();

#endif

    public static void RenderDebugOverlay(this Bezier3 bezier, Color color) {
        float y = bezier.a.y;
        RenderManager.instance.OverlayEffect.DrawBezier(
                    cameraInfo: cameraInfo_,
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

    public static void RenderDebugOverlay(this Bezier3 bezier, Color color, float makredOffset) {
        float y = bezier.a.y;
        RenderManager.instance.OverlayEffect.DrawBezier(
                    cameraInfo: cameraInfo_,
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
        pos.RenderDebugOverlay(color);
    }

    public static void RenderDebugOverlay(this Vector3 pos, Color color) {
        RenderManager.instance.OverlayEffect.DrawCircle(
                    cameraInfo: cameraInfo_,
                    color: color,
                    center: pos,
                    size: 2,
                    minY: pos.y - 5,
                    maxY: pos.y + 5,
                    renderLimits: true,
                    alphaBlend: true);
    }

    [Conditional("DEBUG")]
    public static void RenderOverlay(RenderManager.CameraInfo cameraInfo) {
        cameraInfo_ = cameraInfo;
        foreach (var action in Actions.Values) {
            action();
        }
    }
}
