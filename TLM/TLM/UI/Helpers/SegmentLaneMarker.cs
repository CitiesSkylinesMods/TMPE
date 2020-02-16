using ColossalFramework.Math;
using UnityEngine;

namespace TrafficManager.UI.Helpers {
    // code revived from the old Traffic++ mod : https://github.com/joaofarias/csl-traffic/blob/a4c5609e030c5bde91811796b9836aad60ddde20/CSL-Traffic/Tools/RoadCustomizerTool.cs
    internal class SegmentLaneMarker {
        internal SegmentLaneMarker(Bezier3 bezier) {
            Bezier = bezier;
        }

        internal Bezier3 Bezier;

        private Bounds[] bounds;

        /// <summary>
        /// previous vertical hit position stored for caching.
        /// </summary>
        private float prev_H;

        /// <summary>
        ///  Intersects mouse ray with lane bounds.
        /// </summary>
        /// <param name="ray"></param>
        /// <param name="hitH">vertical hit position of the raycast</param>
        /// <param name="hitH">vertical raycast hit position.</param>
        internal bool IntersectRay() {
            Camera currentCamera = Camera.main;
            Ray mouseRay = currentCamera.ScreenPointToRay(Input.mousePosition);
            float hitH = TrafficManagerTool.GetAccurateHitHeight();

            CalculateBounds(hitH);
            foreach (Bounds bounds in bounds) {
                if (bounds.IntersectRay(mouseRay))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Initializes/recalculates bezier bounds.
        /// </summary>
        /// <param name="hitH">vertical raycast hit position.</param>
        private void CalculateBounds(float hitH) {
            // maximum vertical postion of the bezier.
            float maxH = Mathf.Max(Bezier.a.y, Bezier.d.y);

            float mouseH = UIBase.GetTrafficManagerTool(false).MousePosition.y;

            if ((hitH == prev_H || hitH == maxH || prev_H == mouseH) && bounds != null) {
                // use cached results if mouse has not moved or hitH is ignored.
                return;
            }

            Bezier3 bezier0 = Bezier;
            if (hitH < mouseH - TrafficManagerTool.MAX_HIT_ERROR) {
                // For Metros use projection on the terrain.
                bezier0.a.y = bezier0.b.y = bezier0.c.y = bezier0.d.y = mouseH;
                prev_H = mouseH;
            } else if (hitH > maxH + TrafficManagerTool.MAX_HIT_ERROR) {
                // if marker is projected on another road plane then modify its height
                bezier0.a.y = bezier0.b.y = bezier0.c.y = bezier0.d.y = hitH;
                prev_H = hitH;
            } else {
                // ignore hitH
                prev_H = maxH;
            }

            float angle = Vector3.Angle(bezier0.a, bezier0.b);
            if (Mathf.Approximately(angle, 0f) || Mathf.Approximately(angle, 180f)) {
                angle = Vector3.Angle(bezier0.b, bezier0.c);
                if (Mathf.Approximately(angle, 0f) || Mathf.Approximately(angle, 180f)) {
                    angle = Vector3.Angle(bezier0.c, bezier0.d);
                    if (Mathf.Approximately(angle, 0f) || Mathf.Approximately(angle, 180f)) {
                        // linear bezier
                        Bounds bounds = bezier0.GetBounds();
                        bounds.Expand(0.4f);
                        this.bounds = new Bounds[] { bounds };
                        return;
                    }
                }
            }

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
        internal void RenderOverlay(RenderManager.CameraInfo cameraInfo, Color color, bool enlarge = false) {
            float minH = Mathf.Min(Bezier.a.y, Bezier.d.y);
            float maxH = Mathf.Max(Bezier.a.y, Bezier.d.y);
            RenderManager.instance.OverlayEffect.DrawBezier(
                cameraInfo,
                color,
                Bezier,
                enlarge ? 1.55f : 1.1f,
                0,
                0,
                minH - 100f,
                maxH + 100f,
                true,
                false);
        }
    }
}
