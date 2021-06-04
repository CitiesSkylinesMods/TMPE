using ColossalFramework.Math;
using UnityEngine;

namespace TrafficManager.UI.Helpers {
    using Util;

    internal class NodeLaneMarker {
        internal Vector3 TerrainPosition; // projected on terrain
        internal Vector3 Position; // original height.
        static internal float Radius = 1f;

        /// <summary>
        ///  Intersects mouse ray with marker bounds.
        /// </summary>
        /// <returns><c>true</c>if mouse ray intersects with marker <c>false</c> otherwise</returns>
        internal bool IntersectRay() {
            Ray mouseRay = InGameUtil.Instance.CachedMainCamera.ScreenPointToRay(Input.mousePosition);
            float hitH = TrafficManagerTool.GetAccurateHitHeight();

            Bounds bounds = new Bounds(Vector3.zero, Vector3.one * Radius) {
                center = Position
            };
            return bounds.IntersectRay(mouseRay);
        }

        internal void RenderOverlay(RenderManager.CameraInfo cameraInfo, Color color, bool enlarge = false, bool renderLimits = false) {
            float magnification = enlarge ? 2f : 1f;
            float overdrawHeight = renderLimits ? 0f : 5f;
            RenderManager.instance.OverlayEffect.DrawCircle(
                cameraInfo,
                color,
                Position,
                Radius * magnification,
                Position.y - overdrawHeight,
                Position.y + overdrawHeight,
                renderLimits,
                true);
            RenderManager.instance.OverlayEffect.DrawCircle(
                cameraInfo,
                Color.black,
                Position,
                Radius * 0.75f * magnification, // inner black
                Position.y - overdrawHeight,
                Position.y + overdrawHeight,
                renderLimits,
                false);
        }
    }
}
