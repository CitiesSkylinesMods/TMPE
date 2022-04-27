namespace TrafficManager.UI.Helpers {
    using UnityEngine;
    using System;
    using TrafficManager.Util;
    using TrafficManager.UI.SubTools;

    internal class NodeLaneMarker {
        [Flags]
        internal enum Shape {
            Circle = 1,
            In = 2,
            Out = 4,
            InOut = In | Out,
        }

        internal const float RADIUS = 1f;

        internal Vector3 TerrainPosition; // projected on terrain
        internal Vector3 Position; // original height.
        internal Vector3 Direction; // pointing toward the lane.

        /// <summary>
        ///  Intersects mouse ray with marker bounds.
        /// </summary>
        /// <returns><c>true</c>if mouse ray intersects with marker <c>false</c> otherwise</returns>
        internal bool IntersectRay() {
            Ray mouseRay = InGameUtil.Instance.CachedMainCamera.ScreenPointToRay(Input.mousePosition);
            Bounds bounds = new (Vector3.zero, Vector3.one * RADIUS) {
                center = Position,
            };
            return bounds.IntersectRay(mouseRay);
        }

        internal void RenderOverlay(
            RenderManager.CameraInfo cameraInfo,
            Color color,
            Shape shape = Shape.Circle,
            bool enlarge = false,
            bool renderLimits = false) {
            float overdrawHeight = renderLimits ? 0f : 5f;
            if (shape == Shape.Circle) {
                float magnification = enlarge ? 2f : 1f;
                RenderManager.instance.OverlayEffect.DrawCircle(
                    cameraInfo,
                    color,
                    Position,
                    RADIUS * magnification,
                    Position.y - overdrawHeight,
                    Position.y + overdrawHeight,
                    renderLimits,
                    true);
                RenderManager.instance.OverlayEffect.DrawCircle(
                    cameraInfo,
                    Color.black,
                    Position,
                    RADIUS * 0.75f * magnification, // inner black
                    Position.y - overdrawHeight,
                    Position.y + overdrawHeight,
                    renderLimits,
                    false);
            } else {
                if ((shape & Shape.InOut) == Shape.InOut) {
                    float magnification = enlarge ? 1.1f : 0.9f;
                    float size = RADIUS * magnification;
                    float shift = -0.15f; // to match exactly with lane end.
                    Highlight.DrawDiamondAt(
                        cameraInfo,
                        center: Position + Direction * shift,
                        tangent: Direction,
                        texture: Highlight.SquareTexture,
                        color: color,
                        size: size,
                        minY: Position.y - overdrawHeight,
                        maxY: Position.y + overdrawHeight,
                        renderLimits: renderLimits,
                        alphaBlend: true);
                } else if ((shape & Shape.In) != 0) {
                    float magnification = enlarge ? 1.1f : 0.9f;
                    float size = RADIUS * magnification;
                    float shift = size - 0.5f; // to match exactly with lane end.
                    Highlight.DrawSquareAt(
                        cameraInfo,
                        center: Position + Direction * shift,
                        tangent: Direction,
                        texture: Highlight.TriangleTexture,
                        color: color,
                        size: size,
                        minY: Position.y - overdrawHeight,
                        maxY: Position.y + overdrawHeight,
                        renderLimits: renderLimits,
                        alphaBlend: true);
                } else if ((shape & Shape.Out) != 0) {
                    float magnification = enlarge ? 1.1f : 0.9f;
                    float size = RADIUS * magnification;
                    float shift = size; // to match exactly with lane end.
                    Highlight.DrawSquareAt(
                        cameraInfo,
                        center: Position - Direction * shift,
                        tangent: -Direction,
                        texture: Highlight.TriangleTexture,
                        color: color,
                        size: size,
                        minY: Position.y - overdrawHeight,
                        maxY: Position.y + overdrawHeight,
                        renderLimits: renderLimits,
                        alphaBlend: true);
                }
            }
        }
    }
}
