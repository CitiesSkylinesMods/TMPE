namespace TrafficManager.API.Traffic.Data {
    using System;
    using System.Runtime.InteropServices;

    [StructLayout(LayoutKind.Auto)]
    public struct ExtSegment : IEquatable<ExtSegment> {
        /// <summary>
        /// Segment id
        /// </summary>
        public ushort segmentId;

        /// <summary>
        /// Segment valid?
        /// </summary>
        public bool valid;

        /// <summary>
        /// Is one-way?
        /// </summary>
        public bool oneWay;

        /// <summary>
        /// Is highway?
        /// </summary>
        public bool highway;

        /// <summary>
        /// Has bus lane?
        /// </summary>
        public bool buslane;

        /// <summary>
        /// The Lane IDs as an array for fast lookup by index.
        /// </summary>
        public uint[] lanes;

        public ExtSegment(ushort segmentId) {
            this.segmentId = segmentId;
            valid = false;
            oneWay = false;
            highway = false;
            buslane = false;
            lanes = null;
        }

        public override string ToString() {
            return string.Format(
                "[ExtSegment {0}\n\tsegmentId={1}\n\tvalid={2}\n\toneWay={3}\n\thighway={4}\n" +
                "\tbuslane={5}\nExtSegment]",
                base.ToString(),
                segmentId,
                valid,
                oneWay,
                highway,
                buslane);
        }

        public void Reset() {
            oneWay = false;
            highway = false;
            buslane = false;
            lanes = null;
        }

        public bool Equals(ExtSegment otherSeg) {
            return segmentId == otherSeg.segmentId;
        }

        public override bool Equals(object other) {
            return other is ExtSegment segment
                   && Equals(segment);
        }

        public override int GetHashCode() {
            int prime = 31;
            int result = 1;
            result = prime * result + segmentId.GetHashCode();
            return result;
        }
    }
}