namespace TrafficManager.Util.Iterators {
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using TrafficManager.Manager.Impl;

    /// <summary>
    /// Mutable struct enumerator to be returned from the GetSegmentLaneIds method.
    /// This implementation is just for perf optimizations and should be handled with care since it is a mutable struct!
    /// This should be fine for the regular foreach use case, but could cause bugs if used for anything else inappropriately.
    /// </summary>
    public struct GetSegmentLaneIdsEnumerator : IEnumerator<LaneIdAndIndex> {
        private uint _initialLaneId;
        private int _netInfoLanesLength;
        private NetLane[] _laneBuffer;

        private bool _firstRun;
        private LaneIdAndIndex _currentLaneIdAndIndex;

        public GetSegmentLaneIdsEnumerator(uint initialLaneId, int netInfoLanesLength, NetLane[] laneBuffer) {
            _initialLaneId = initialLaneId;
            _netInfoLanesLength = netInfoLanesLength;
            _laneBuffer = laneBuffer ?? throw new ArgumentNullException(nameof(laneBuffer));

            _firstRun = true;

            _currentLaneIdAndIndex = new LaneIdAndIndex(uint.MaxValue, -1);
        }

        public LaneIdAndIndex Current => _currentLaneIdAndIndex;

        object IEnumerator.Current => _currentLaneIdAndIndex;

        public bool MoveNext() {
            if (_initialLaneId == 0 || _netInfoLanesLength < 1) {
                return false;
            }

            if (_firstRun) {
                _firstRun = false;
                _currentLaneIdAndIndex = new LaneIdAndIndex(_initialLaneId, 0);
                return true;
            }

            _currentLaneIdAndIndex = new LaneIdAndIndex(
                _laneBuffer[_currentLaneIdAndIndex.laneId].m_nextLane,
                _currentLaneIdAndIndex.laneIndex + 1);

            return _currentLaneIdAndIndex.laneId != 0
                && _currentLaneIdAndIndex.laneIndex < _netInfoLanesLength;
        }

        public void Reset() {
            _firstRun = true;
            _currentLaneIdAndIndex = new LaneIdAndIndex(uint.MaxValue, -1);
        }

        public void Dispose() {
        }
    }
}
