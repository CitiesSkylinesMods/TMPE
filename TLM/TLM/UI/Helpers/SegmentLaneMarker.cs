using ColossalFramework.Math;
using UnityEngine;

namespace TrafficManager.UI.Helpers {
    using Util;

    // code revived from the old Traffic++ mod : https://github.com/joaofarias/csl-traffic/blob/a4c5609e030c5bde91811796b9836aad60ddde20/CSL-Traffic/Tools/RoadCustomizerTool.cs
    internal class SegmentLaneMarker {
        internal SegmentLaneMarker(Bezier3 bezier) {
            Bezier = bezier;
            IsUnderground = CheckIsUnderground(Bezier.a) ||
                            CheckIsUnderground(Bezier.d);
            CalculateBounds();
        }

        internal Bezier3 Bezier;
        internal float Size = 1.1f;

        private Bounds[] bounds;

        /// <summary>
        /// previous vertical hit position stored for caching.
        /// </summary>
        private float prev_H;

        public bool IsUnderground { get; private set; }

        /// <summary>
        ///  Intersects mouse ray with lane bounds.
        /// </summary>
        /// <param name="ray"></param>
        /// <param name="hitH">vertical hit position of the raycast</param>
        /// <param name="hitH">vertical raycast hit position.</param>
        internal bool IntersectRay() {
            Ray mouseRay = InGameUtil.Instance.CachedMainCamera.ScreenPointToRay(Input.mousePosition);

            foreach (Bounds bounds in bounds) {
                if (bounds.IntersectRay(mouseRay))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Forces render height.
        /// </summary>
        /// <param name="height">New height</param>
        internal void ForceBezierHeight(float height) {
            Bezier = Bezier.ForceHeight(height);
        }

        /// <summary>
        /// Initializes/recalculates bezier bounds.
        /// </summary>
        /// <param name="hitH">vertical raycast hit position.</param>
        private void CalculateBounds() {
            Bezier3 bezier0 = Bezier;

            // split bezier in 10 parts to correctly raycast curves
            int n = 10;
            bounds = new Bounds[n];
            float size = 1f / n;
            for (int i = 0; i < n; i++) {
                Bezier3 bezier = bezier0.Cut(i * size, (i + 1) * size);
                Bounds bounds = bezier.GetBounds();
                bounds.Expand(1f);
                this.bounds[i] = bounds;
            }
        }

        /// <summary>
        /// Renders lane overlay.
        /// </summary>
        internal void RenderOverlay(RenderManager.CameraInfo cameraInfo, Color color, bool enlarge = false, bool renderLimits = false) {
            float minH = Mathf.Min(Bezier.a.y, Bezier.d.y);
            float maxH = Mathf.Max(Bezier.a.y, Bezier.d.y);

            float overdrawHeight = IsUnderground || renderLimits ? 0f : 5f;
            ColossalFramework.Singleton<ToolManager>.instance.m_drawCallData.m_overlayCalls++;
            RenderManager.instance.OverlayEffect.DrawBezier(
                cameraInfo,
                color,
                Bezier,
                enlarge ? Size * 1.41f : Size,
                0,
                0,
                minH - overdrawHeight,
                maxH + overdrawHeight,
                IsUnderground || renderLimits,
                false);
        }

        private bool CheckIsUnderground(Vector3 position) {
            float maxY = position.y;
            float sampledHeight = TerrainManager.instance.SampleDetailHeightSmooth(position);
            return sampledHeight > maxY;
        }
    }
}
