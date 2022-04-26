using ColossalFramework.Math;
using UnityEngine;

namespace TrafficManager.UI.Helpers {
    using TrafficManager.Util;

    /// <summary>
    /// code revived from the old Traffic++ mod:
    /// https://github.com/joaofarias/csl-traffic/blob/a4c5609e030c5bde91811796b9836aad60ddde20/CSL-Traffic/Tools/RoadCustomizerTool.cs
    /// </summary>
    internal class SegmentLaneMarker {
        internal SegmentLaneMarker(Bezier3 bezier) {
            this.Bezier = bezier;
            this.IsUnderground = InGameUtil.CheckIsUnderground(bezier.a) ||
                                 InGameUtil.CheckIsUnderground(bezier.d);
            CalculateBounds();
        }

        private Bezier3 Bezier;

        /// <summary>Bezier size when drawing (thickness).</summary>
        internal float Size = 1.1f;

        private Bounds[] bounds;

        private bool IsUnderground { get; set; }

        /// <summary>Intersects mouse ray with lane bounds.</summary>
        internal bool IntersectRay() {
            Ray mouseRay = InGameUtil.Instance.CachedMainCamera.ScreenPointToRay(Input.mousePosition);

            foreach (Bounds eachBound in bounds) {
                if (eachBound.IntersectRay(mouseRay)) {
                    return true;
                }
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

        /// <summary>Renders lane overlay.</summary>
        internal void RenderOverlay(
            RenderManager.CameraInfo cameraInfo,
            Color color,
            bool enlarge = false,
            bool renderLimits = false,
            bool alphaBlend = false,
            bool cutStart = false,
            bool cutEnd = false) {
            float minH = Mathf.Min(Bezier.a.y, Bezier.d.y);
            float maxH = Mathf.Max(Bezier.a.y, Bezier.d.y);
            float size = enlarge ? Size * 1.41f : Size;

            float overdrawHeight = IsUnderground || renderLimits ? 0f : 5f;
            ColossalFramework.Singleton<ToolManager>.instance.m_drawCallData.m_overlayCalls++;
            RenderManager.instance.OverlayEffect.DrawBezier(
                cameraInfo: cameraInfo,
                color: color,
                bezier: Bezier,
                size: size,
                cutStart: cutStart ? size * 0.50f : 0,
                cutEnd: cutEnd ? size * 0.50f : 0,
                minY: minH - overdrawHeight,
                maxY: maxH + overdrawHeight,
                renderLimits: IsUnderground || renderLimits,
                alphaBlend: alphaBlend);
        }
    }
}
