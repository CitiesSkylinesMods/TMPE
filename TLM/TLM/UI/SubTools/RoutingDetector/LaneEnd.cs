namespace TrafficManager.UI.SubTools.RoutingDetector {
    using TrafficManager.UI.Helpers;
    using UnityEngine;

    internal class LaneEnd {
        internal uint LaneId;
        internal int LaneIndex;
        internal bool StartNode;

        internal SegmentLaneMarker SegmentMarker;
        internal NodeLaneMarker NodeMarker;

        internal Connection[] ForwardConnections;
        internal Connection[] BackwardConnections;

        /// <summary>
        ///  Intersects mouse ray with marker bounds.
        /// </summary>
        /// <returns><c>true</c>if mouse ray intersects with marker <c>false</c> otherwise</returns>
        internal bool IntersectRay() => SegmentMarker.IntersectRay();

        /// <summary>
        /// renders lane overlay. If highlighted, renders enlarged sheath(lane+circle) overlay. Otherwise
        /// renders circle at lane end.
        /// </summary>
        internal void RenderOverlay(RenderManager.CameraInfo cameraInfo, Color color, bool highlight = false, bool renderLimits = false) {
            if (highlight) {
                SegmentMarker.RenderOverlay(cameraInfo, color, enlarge: true, overDraw: renderLimits);
            }
            NodeMarker.RenderOverlay(cameraInfo, color, enlarge: highlight, renderLimits: renderLimits);
        }
    }
}
