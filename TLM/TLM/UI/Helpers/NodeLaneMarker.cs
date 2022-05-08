namespace TrafficManager.UI.Helpers {
    using System;
    using TrafficManager.Util;
    using UnityEngine;

    internal class NodeLaneMarker {
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
            Highlight.Shape shape = Highlight.Shape.Circle,
            bool enlarge = false,
            bool renderLimits = false) {
            float overdrawHeight = renderLimits ? 0f : 5f;
            float magnification = enlarge ? 2f : 1f;
            float size = RADIUS * magnification;
            float outlineScale = shape is Highlight.Shape.Circle ? 0.75f : 0.9f;
            float outlineSize = RADIUS * outlineScale * magnification;

            Highlight.DrawShape(
                cameraInfo,
                shape,
                color,
                Position,
                Direction,
                size,
                Position.y - overdrawHeight,
                Position.y + overdrawHeight,
                renderLimits,
                true);
            Highlight.DrawShape(
                cameraInfo,
                shape,
                Color.black,
                Position,
                Direction,
                outlineSize, // black outline
                Position.y - overdrawHeight,
                Position.y + overdrawHeight,
                renderLimits,
                false);
        }
    }
}
