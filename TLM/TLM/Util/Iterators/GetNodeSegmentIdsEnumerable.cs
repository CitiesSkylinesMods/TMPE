namespace TrafficManager.Util.Iterators {
    using System.Collections;
    using System.Collections.Generic;
    using TrafficManager.Manager.Impl;

    /// <summary>
    /// Struct enumerable to be returned from the GetNodeSegmentIds method.
    /// This implementation is just for perf optimizations and should be handled with care since it returns a mutable struct!
    /// This should be fine for the regular foreach use case, but could cause bugs if used for anything else inappropriately.
    /// </summary>
    public readonly struct GetNodeSegmentIdsEnumerable : IEnumerable<ushort> {
        private readonly ushort nodeId;
        private readonly ushort initialSegmentId;
        private readonly ClockDirection clockDirection;
        private readonly NetSegment[] segmentBuffer;

        public GetNodeSegmentIdsEnumerable(ushort nodeId, ushort initialSegmentId, ClockDirection clockDirection, NetSegment[] segmentBuffer) {
            this.nodeId = nodeId;
            this.initialSegmentId = initialSegmentId;
            this.clockDirection = clockDirection;
            this.segmentBuffer = segmentBuffer;
        }

        /// <summary>
        /// The method that is actually used in a foreach.
        /// </summary>
        /// <returns>Returns a GetNodeSegmentIdsEnumerator struct.</returns>
        public GetNodeSegmentIdsEnumerator GetEnumerator() {
            return new GetNodeSegmentIdsEnumerator(nodeId, initialSegmentId, clockDirection, segmentBuffer);
        }

        /// <summary>
        /// Explicit interface implementation.
        /// </summary>
        /// <returns>Returns a boxed GetNodeSegmentIdsEnumerator struct.</returns>
        IEnumerator<ushort> IEnumerable<ushort>.GetEnumerator() {
            return new GetNodeSegmentIdsEnumerator(nodeId, initialSegmentId, clockDirection, segmentBuffer);
        }

        /// <summary>
        /// Explicit interface implementation.
        /// </summary>
        /// <returns>Returns a boxed GetNodeSegmentIdsEnumerator struct.</returns>
        IEnumerator IEnumerable.GetEnumerator() {
            return new GetNodeSegmentIdsEnumerator(nodeId, initialSegmentId, clockDirection, segmentBuffer);
        }
    }
}
