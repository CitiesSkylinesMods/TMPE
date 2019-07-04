namespace TrafficManager.Geometry {
    using System;

    public interface ISegmentEndId : IEquatable<ISegmentEndId> {
        // TODO documentation
        ushort SegmentId { get; }
        bool StartNode { get; }

        bool Relocate(ushort segmentId, bool startNode);
    }
}