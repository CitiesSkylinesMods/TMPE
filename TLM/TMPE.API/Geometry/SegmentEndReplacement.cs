namespace TrafficManager.API.Geometry {
    using System;
    using TrafficManager.API.Traffic;

    public struct SegmentEndReplacement {
        public ISegmentEndId oldSegmentEndId;
        public ISegmentEndId newSegmentEndId;

        public override string ToString() {
            return string.Format(
                "[SegmentEndReplacement\n\toldSegmentEndId = {0}\n\tnewSegmentEndId = {1}\n" +
                "SegmentEndReplacement]",
                oldSegmentEndId,
                newSegmentEndId);
        }

        public bool IsDefined() {
            return oldSegmentEndId != null && newSegmentEndId != null;
        }
    }
}