namespace TrafficManager.UI.Helpers; 
using ColossalFramework.Math;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Diagnostics;
using TrafficManager.Util;
using System.Linq;

internal static class DebugOverlay {
    public class ActionDict {
        private static Dictionary<int, Action> dict_ = new();

        public Action this[int key] {
            get {
                lock (dict_) {
                    return dict_[key];
                }
            }
            set {
                lock (dict_) {
                    dict_[key] = value;
                }
            }
        }

        public Action[] Values {
            get {
                lock (dict_) {
                    return dict_.Values.ToArray();
                }
            }
        }

        public bool Remove(int key) {
            lock (dict_) {
                return dict_.Remove(key);
            }
        }

        public void Clear() {
            lock (dict_) {
                dict_.Clear();
            }
        }
    }

    private static RenderManager.CameraInfo cameraInfo_;

    /// <summary>
    /// key : id
    /// value : render action
    /// </summary>
    public static ActionDict Actions;

#if DEBUG
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

    public static void RenderDebugArrowOverlay(this Segment3 segment, Color color) {
        // draw line
        float y = segment.a.y;
        RenderManager.instance.OverlayEffect.DrawSegment(
            cameraInfo: cameraInfo_,
            color: color,
            segment: segment,
            dashLen: 0,
            size: 1,
            minY: y - 5,
            maxY: y + 5,
            renderLimits: true,
            alphaBlend: true);

        // draw arrow head:
        Vector3 dir = (segment.b - segment.a).normalized;
        Vector3 dir90 = dir.RotateXZ90CW();
        Vector3 center = segment.b;

        Quad3 quad = new Quad3 {
            a = center + dir90,
            b = center - dir90,
            c = center + 2 * dir,
            d = center + 2 * dir,
        };

        RenderManager.instance.OverlayEffect.DrawQuad(
            cameraInfo_,
            color,
            quad,
            y - 5,
            y + 5,
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
