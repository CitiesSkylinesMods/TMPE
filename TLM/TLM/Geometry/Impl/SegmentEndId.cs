namespace TrafficManager.Geometry.Impl {
    using TrafficManager.API.Traffic;
    using TrafficManager.Util.Extensions;

    public class SegmentEndId : ISegmentEndId {
        public SegmentEndId(ushort segmentId, ushort nodeId) {
            SegmentId = segmentId;
            StartNode = segmentId.ToSegment().IsStartNode(nodeId);
        }

        public SegmentEndId(ushort segmentId, bool startNode) {
            SegmentId = segmentId;
            StartNode = startNode;
        }

        public ushort SegmentId { get; protected set; }

        public bool StartNode { get; protected set; }

        public bool Relocate(ushort segmentId, bool startNode) {
            SegmentId = segmentId;
            StartNode = startNode;
            return true;
        }

        public override bool Equals(object other) {
            return other is ISegmentEndId
                   && Equals((ISegmentEndId)other);
        }

        public bool Equals(ISegmentEndId otherSegEndId) {
            return otherSegEndId != null
                   && SegmentId == otherSegEndId.SegmentId
                   && StartNode == otherSegEndId.StartNode;
        }

        public override int GetHashCode() {
            int prime = 31;
            int result = 1;
            result = (prime * result) + SegmentId.GetHashCode();
            result = (prime * result) + StartNode.GetHashCode();
            return result;
        }

        public override string ToString() {
            return $"[SegmentEndId {SegmentId} @ {StartNode}]";
        }
    }
}
