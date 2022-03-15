using CSUtil.Commons;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.API.Manager;
using TrafficManager.Util.Extensions;

namespace TrafficManager.Manager.Impl {
    internal class ExtLaneManager : AbstractCustomManager, IExtLaneManager {

        private readonly ExtLane[] lanes;

        static ExtLaneManager() {
            Instance = new ExtLaneManager();
        }

        private ExtLaneManager() {
            lanes = new ExtLane[NetManager.MAX_LANE_COUNT];

            for (int i = 0; i < lanes.Length; i++) {
                lanes[i] = new ExtLane((uint)i);
            }
        }

        public static ExtLaneManager Instance { get; }

        public bool GetSegmentAndIndex(uint laneId, out ushort segmentId, out int laneIndex) => lanes[laneId].GetSegmentAndIndex(out segmentId, out laneIndex);

        public NetInfo.Lane GetLaneInfo(uint laneId) => lanes[laneId].GetLaneInfo();

        public override void OnLevelUnloading() {
            base.OnLevelUnloading();

            for (uint laneId = 1; laneId < lanes.Length; ++laneId) {
                lanes[laneId].Reset();
            }
        }

        protected override void InternalPrintDebugInfo() {
            base.InternalPrintDebugInfo();
            Log._Debug($"Extended lane data:");

            for (uint laneId = 1; laneId < lanes.Length; ++laneId) {
                ref ExtLane lane = ref lanes[laneId];
                if (lane.IsValidWithSegment())
                    Log._Debug(lane.ToString());
            }
        }

        private struct ExtLane {
            /// <summary>
            /// The Lane ID of the corresponding NetLane.
            /// </summary>
            private uint laneId;

            /// <summary>
            /// The Segment ID that was referenced by the corresponding NetLane
            /// the last time this object was recalculated.
            /// </summary>
            private ushort segmentId;

            /// <summary>
            /// This lane's index in the segment represented by <see cref="segmentId"/>.
            /// </summary>
            private int laneIndex;

            internal ExtLane(uint laneId) {
                this.laneId = laneId;
                segmentId = 0;
                laneIndex = -1;
            }

            public override string ToString() {
                var segmentIdStatus = segmentId == laneId.ToLane().m_segment
                                        ? string.Empty
                                        : $"(needs refresh, lane segment is {laneId.ToLane().m_segment})";

                return $"[ExtLane {laneId}\n" +
                        $"\tsegmentId={segmentId} {segmentIdStatus}\n" +
                        $"\tlaneIndex={laneIndex}\n" +
                        "ExtLane]";
            }

            internal bool IsValidWithSegment() => laneId.ToLane().IsValidWithSegment();

            internal bool GetSegmentAndIndex(out ushort segmentId, out int laneIndex) {
                var result = CheckNetSegment();
                segmentId = this.segmentId;
                laneIndex = this.laneIndex;
                return result;
            }

            internal NetInfo.Lane GetLaneInfo() => CheckNetSegment() ? segmentId.ToSegment().Info.m_lanes[laneIndex] : null;

            internal void Reset() {
                segmentId = 0;
                laneIndex = -1;
            }

            private bool Recalculate(ref NetLane lane) {
                if (IsValidWithSegment()) {
                    segmentId = lane.m_segment;
                    laneIndex = ExtSegmentManager.Instance.GetLaneIndex(segmentId, laneId);
                    return true;
                }
                Reset();
                return false;
            }

            /// <summary>
            /// Recalculates the ExtLane if its Segment ID is out of sync with the corresponding NetLane.
            /// </summary>
            /// <returns>true if successful, false if the NetLane does not currently represent a valid lane</returns>
            private bool CheckNetSegment() {
                ref var lane = ref laneId.ToLane();
                if (IsValidWithSegment()) {
                    if (lane.m_segment == segmentId || Recalculate(ref lane)) {
                        return true;
                    }
                }
                Reset();
                return false;
            }
        }
    }
}
