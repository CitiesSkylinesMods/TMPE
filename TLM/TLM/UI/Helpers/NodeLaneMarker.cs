namespace TrafficManager.UI.Helpers {
    using System;
    using TrafficManager.Util;
    using UnityEngine;

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
            Bounds bounds = new(Vector3.zero, Vector3.one * RADIUS) {
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
                float magnification = enlarge ? 1.23f : 0.8f;
                float size = RADIUS * magnification;
                float shift = enlarge ? -0.2f : 0; // to match exactly with lane end.
                if ((shape & Shape.InOut) == Shape.InOut) {
                    if (!enlarge) {
                        Highlight.DrawDiamond(
                             cameraInfo,
                             center: Position + Direction * shift,
                             tangent: Direction,
                             texture: Highlight.SquareTexture,
                             color: Color.black, // outer black
                             size: size * 1.4f,
                             minY: Position.y - overdrawHeight,
                             maxY: Position.y + overdrawHeight,
                             renderLimits: renderLimits,
                             alphaBlend: true);
                    }
                    Highlight.DrawDiamond(
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
                    DrawTriangleHead(
                        cameraInfo: cameraInfo,
                        drawOutline: !enlarge,
                        color: color,
                        Position + Direction * shift,
                        Direction,
                        size: size,
                        outerSize: size * 1.5f,
                        minY: Position.y - overdrawHeight,
                        maxY: Position.y + overdrawHeight,
                        renderLimits: renderLimits);
                } else if ((shape & Shape.Out) != 0) {
                    if (!enlarge) shift = -0.30f; // to cover lane curve
                    DrawTriangleHead(
                        cameraInfo: cameraInfo,
                        drawOutline: !enlarge,
                        color: color,
                        pos: Position - Direction * shift,
                        dir: -Direction,
                        size: size,
                        outerSize: size * 1.5f,
                        minY: Position.y - overdrawHeight,
                        maxY: Position.y + overdrawHeight,
                        renderLimits: renderLimits);
                }
            }
        }

        private static void DrawTriangleHead(
            RenderManager.CameraInfo cameraInfo,
            bool drawOutline,
            Color color,
            Vector3 pos,
            Vector3 dir,
            float size,
            float outerSize,
            float minY,
            float maxY,
            bool renderLimits) {
            if (drawOutline) {
                outerSize *= 0.4f; // texture less triangles are bigger.
                float shift2 = (outerSize - size) * 0.5f; // shift the outline to the center.
                Highlight.DrawTriangle(
                    cameraInfo,
                    center: pos - dir * shift2,
                    tangent: dir,
                    //texture: Highlight.SquareTexture,
                    color: Color.black,
                    size: outerSize,
                    minY: minY,
                    maxY: maxY,
                    renderLimits: renderLimits,
                    alphaBlend: false);
            }
            Highlight.DrawTriangle(
                cameraInfo,
                center: pos,
                tangent: dir,
                texture: Highlight.SquareTexture,
                color: color,
                size: size,
                minY: minY,
                maxY: maxY,
                renderLimits: renderLimits,
                alphaBlend: true);
        }

    }
}
