namespace TrafficManager.Util.Iterators {
    using System.Collections;
    using System.Collections.Generic;
    using TrafficManager.Manager.Impl;

    /// <summary>
    /// Struct enumerable to be returned from the GetSegmentLaneIds method.
    /// This implementation is just for perf optimizations and should be handled with care since it returns a mutable struct!
    /// This should be fine for the regular foreach use case, but could cause bugs if used for anything else inappropriately.
    /// </summary>
    public readonly struct GetSegmentLaneIdsEnumerable : IEnumerable<LaneIdAndIndex> {
        private readonly uint _initialLaneId;
        private readonly int _netInfoLaneLength;
        private readonly NetLane[] _laneBuffer;

        public GetSegmentLaneIdsEnumerable(uint initialLaneId, int netInfoLaneLength, NetLane[] laneBuffer) {
            _initialLaneId = initialLaneId;
            _netInfoLaneLength = netInfoLaneLength;
            _laneBuffer = laneBuffer;
        }

        /// <summary>
        /// The method that is actually used in a foreach.
        /// </summary>
        /// <returns>Returns a GetSegmentLaneIdsEnumerator struct.</returns>
        public GetSegmentLaneIdsEnumerator GetEnumerator() {
            return new GetSegmentLaneIdsEnumerator(_initialLaneId, _netInfoLaneLength, _laneBuffer);
        }

        /// <summary>
        /// Explicit interface implementation.
        /// </summary>
        /// <returns>Returns a boxed GetSegmentLaneIdsEnumerator struct.</returns>
        IEnumerator<LaneIdAndIndex> IEnumerable<LaneIdAndIndex>.GetEnumerator() {
            return new GetSegmentLaneIdsEnumerator(_initialLaneId, _netInfoLaneLength, _laneBuffer);
        }

        /// <summary>
        /// Explicit interface implementation.
        /// </summary>
        /// <returns>Returns a boxed GetSegmentLaneIdsEnumerator struct.</returns>
        IEnumerator IEnumerable.GetEnumerator() {
            return new GetSegmentLaneIdsEnumerator(_initialLaneId, _netInfoLaneLength, _laneBuffer);
        }
    }
}
