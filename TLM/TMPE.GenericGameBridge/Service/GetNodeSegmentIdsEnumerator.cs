using GenericGameBridge.Service;
using System;
using System.Collections;
using System.Collections.Generic;

namespace GenericGameBridge.Service {
    /// <summary>
    /// Mutable struct enumerator to be returned from the GetNodeSegmentIds method.
    /// This implementation is just for perf optimizations and should be handled with care since it is a mutable struct!
    /// This should be fine for the regular foreach usecase, but could cause bugs if used for anything else inappropriately.
    /// </summary>
    public struct GetNodeSegmentIdsEnumerator : IEnumerator<ushort> {
        private ushort nodeId;
        private ushort initialSegment;
        private ClockDirection clockDirection;
        private NetSegment[] segmentBuffer;

        private bool firstRun;
        private ushort currentSegmentId;

        public GetNodeSegmentIdsEnumerator(ushort nodeId, ushort initialSegmentId, ClockDirection clockDirection, NetSegment[] segmentBuffer) {
            this.nodeId = nodeId;
            this.initialSegment = initialSegmentId;
            this.clockDirection = clockDirection;
            this.segmentBuffer = segmentBuffer ?? throw new ArgumentNullException(nameof(segmentBuffer));

            this.firstRun = true;
            this.currentSegmentId = default;
        }

        public ushort Current => currentSegmentId;

        object IEnumerator.Current => currentSegmentId;

        public bool MoveNext() {
            if (firstRun && initialSegment == 0) {
                return false;
            } else if (firstRun && initialSegment != 0) {
                currentSegmentId = initialSegment;
                firstRun = false;
                return true;
            }

            if (clockDirection == ClockDirection.Clockwise) {
                currentSegmentId = segmentBuffer[currentSegmentId].GetLeftSegment(nodeId);
            } else if (clockDirection == ClockDirection.CounterClockwise) {
                currentSegmentId = segmentBuffer[currentSegmentId].GetRightSegment(nodeId);
            } else {
                throw new Exception($"Unknown ClockDirection '{nameof(clockDirection)}'");
            }

            if (currentSegmentId == 0 || currentSegmentId == initialSegment) {
                return false;
            }

            return true;
        }

        public void Reset() {
            throw new NotImplementedException();
        }

        public void Dispose() {
        }
    }
}
