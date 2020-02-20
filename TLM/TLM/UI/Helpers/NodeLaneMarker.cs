using ColossalFramework.Math;
using UnityEngine;

namespace TrafficManager.UI.Helpers {
    internal class NodeLaneMarker {
        internal Vector3 TerrainPosition; /// projected on terrain
        internal Vector3 Position; /// original height.
        static internal float Radius = 1f;

        /// <summary>
        ///  Intersects mouse ray with marker bounds.
        /// </summary>
        /// <returns><c>true</c>if mouse ray intersects with marker <c>false</c> otherwise</returns>
        internal bool IntersectRay() {
            Camera currentCamera = Camera.main;
            Ray mouseRay = currentCamera.ScreenPointToRay(Input.mousePosition);
            float hitH = TrafficManagerTool.GetAccurateHitHeight();

            Vector3 pos = Position;
            float mouseH = UIBase.GetTrafficManagerTool(false).MousePosition.y;
            if (hitH < mouseH - TrafficManagerTool.MAX_HIT_ERROR) {
                // For metros use projection on the terrain.
                pos = TerrainPosition;
            } else if (hitH - pos.y > TrafficManagerTool.MAX_HIT_ERROR) {
                // if marker is projected on road plane above then modify its height
                pos.y = hitH;
            }
            Bounds bounds = new Bounds(Vector3.zero, Vector3.one * Radius) {
                center = pos,
            };
            return bounds.IntersectRay(mouseRay);
        }

        internal void RenderOverlay(RenderManager.CameraInfo cameraInfo, Color color, bool enlarge = false) {
            float magnification = enlarge ? 2f : 1f;
            RenderManager.instance.OverlayEffect.DrawCircle(
                cameraInfo,
                color,
                TerrainPosition,
                Radius * magnification,
                TerrainPosition.y - 100f, // through all the geometry -100..100
                TerrainPosition.y + 100f,
                false,
                true);
            RenderManager.instance.OverlayEffect.DrawCircle(
                cameraInfo,
                Color.black,
                TerrainPosition,
                Radius * 0.75f * magnification, // inner black
                TerrainPosition.y - 100f, // through all the geometry -100..100
                TerrainPosition.y + 100f,
                false,
                false);
        }
    }
}
