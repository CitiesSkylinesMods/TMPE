namespace TrafficManager.API.Manager {
    using System;
    using TrafficManager.API.Traffic;

    [Obsolete("should be removed when implementing issue #240")]
    public interface ISegmentEndManager {
        // TODO documentation
        ISegmentEnd GetOrAddSegmentEnd(ISegmentEndId endId);
        ISegmentEnd GetOrAddSegmentEnd(ushort segmentId, bool startNode);
        ISegmentEnd GetSegmentEnd(ISegmentEndId endId);
        ISegmentEnd GetSegmentEnd(ushort segmentId, bool startNode);
        void RemoveSegmentEnd(ISegmentEndId endId);
        void RemoveSegmentEnd(ushort segmentId, bool startNode);
        void RemoveSegmentEnds(ushort segmentId);
        bool UpdateSegmentEnd(ISegmentEndId endId);
        bool UpdateSegmentEnd(ushort segmentId, bool startNode);
    }
}