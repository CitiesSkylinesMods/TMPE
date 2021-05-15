namespace TrafficManager.API.Manager {
    using System;
    using TrafficManager.API.Geometry;
    using TrafficManager.API.Traffic.Data;
    using TrafficManager.API.Util;

    public interface IGeometryManager {
        // TODO define me!
        void SimulationStep(bool onylFirstPass = false);
        void OnUpdateSegment(ref ExtSegment segment);
        void OnSegmentEndReplacement(SegmentEndReplacement replacement);
        IDisposable Subscribe(IObserver<GeometryUpdate> observer);
        void MarkAllAsUpdated();
        void MarkAsUpdated(ref ExtSegment segment, bool updateNodes = true);
        void MarkAsUpdated(ushort nodeId, bool updateSegments = false);
    }

    public struct GeometryUpdate {
        public GeometryUpdate(ref ExtSegment segment) {
            this.segment = segment;
            nodeId = null;
            replacement = default(SegmentEndReplacement);
        }

        public GeometryUpdate(ushort nodeId) {
            this.nodeId = nodeId;
            segment = null;
            replacement = default(SegmentEndReplacement);
        }

        public GeometryUpdate(SegmentEndReplacement replacement) {
            this.replacement = replacement;
            segment = null;
            nodeId = null;
        }

        public ExtSegment? segment { get; private set; }
        public ushort? nodeId { get; private set; }
        public SegmentEndReplacement replacement { get; private set; }
    }
}