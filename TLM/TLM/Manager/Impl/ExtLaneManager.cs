using CSUtil.Commons;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.API.Manager;
using TrafficManager.Patch;
using TrafficManager.Util.Extensions;

namespace TrafficManager.Manager.Impl {
    internal class ExtLaneManager : AbstractCustomManager, IExtLaneManager {

        private static readonly object lockObject = new object();

        private readonly ExtLane[] lanes;

        static ExtLaneManager() {
            Instance = new ExtLaneManager();
        }

        private ExtLaneManager() {
            lanes = new ExtLane[NetManager.MAX_LANE_COUNT];

            for (int i = 0; i < lanes.Length; i++) {
                lanes[i] = new ExtLane((uint)i);
            }

            NetManagerEvents.Instance.ReleasedLane += ReleasedLane;
        }

        public static ExtLaneManager Instance { get; }

        public bool GetSegmentAndIndex(uint laneId, out ushort segmentId, out int laneIndex)
            => lanes[laneId].GetSegmentAndIndex(laneId, out segmentId, out laneIndex);

        public int GetLaneIndex(uint laneId) => lanes[laneId].GetLaneIndex(laneId);

        public NetInfo.Lane GetLaneInfo(uint laneId) => lanes[laneId].GetLaneInfo(laneId);

        public override void OnLevelUnloading() {
            base.OnLevelUnloading();

            for (uint laneId = 1; laneId < lanes.Length; ++laneId) {
                lanes[laneId].Reset(laneId);
            }
        }

        protected override void InternalPrintDebugInfo() {
            base.InternalPrintDebugInfo();
            Log._Debug($"Extended lane data:");

            for (uint laneId = 1; laneId < lanes.Length; ++laneId) {
                ref ExtLane lane = ref lanes[laneId];
                if (lane.IsValidWithSegment(laneId))
                    Log._Debug(lane.ToString(laneId));
            }
        }

        // If we see evidence of performance-impacting collisions,
        // this could be enhanced to use objects from a static array
        // based on an ID-derived index.
        private static object GetLockObject(uint laneId) => lockObject;

        private void ReleasedLane(uint laneId) => lanes[laneId].Reset(laneId);

        private struct ExtLane {
            /// <summary>
            /// This lane's index in the associated segment.
            /// </summary>
            private int laneIndex;

            internal ExtLane(uint laneId) {
                laneIndex = -1;
            }

            public override string ToString() {
                return $"[ExtLane\n" +
                        $"\tlaneIndex={laneIndex}\n" +
                        "ExtLane]";
            }

            internal string ToString(uint laneId) {
                return $"[ExtLane {laneId}\n" +
                        $"\tlaneIndex={laneIndex}\n" +
                        "ExtLane]";
            }

            internal bool IsValidWithSegment(uint laneId) => laneId.ToLane().IsValidWithSegment();

            internal bool GetSegmentAndIndex(uint laneId, out ushort segmentId, out int laneIndex) {
                ref var lane = ref laneId.ToLane();
                var result = CheckLaneIndex(laneId, ref lane);
                segmentId = result ? lane.m_segment : default;
                laneIndex = this.laneIndex;
                return result;
            }

            internal int GetLaneIndex(uint laneId) => CheckLaneIndex(laneId) ? laneIndex : -1;

            internal NetInfo.Lane GetLaneInfo(uint laneId) {
                ref var lane = ref laneId.ToLane();
                return CheckLaneIndex(laneId, ref lane)
                        ? lane.m_segment.ToSegment().Info.m_lanes[laneIndex]
                        : null;
            }

            internal void Reset(uint laneId) {
                laneIndex = -1;
            }

            private bool Recalculate(uint laneId, ref NetLane lane) {
                if (lane.IsValidWithSegment()) {
                    laneIndex = ExtSegmentManager.Instance.GetLaneIndex(lane.m_segment, laneId);
                    return true;
                }
                Reset(laneId);
                return false;
            }

            private bool LockAndCheckLaneIndex(uint laneId, ref NetLane lane) {
                lock (GetLockObject(laneId)) {
                    return laneIndex >= 0 || Recalculate(laneId, ref lane);
                }
            }

            private bool CheckLaneIndex(uint laneId, ref NetLane lane)
                => laneIndex >= 0 || LockAndCheckLaneIndex(laneId, ref lane);

            private bool CheckLaneIndex(uint laneId)
                => laneIndex >= 0 || LockAndCheckLaneIndex(laneId, ref laneId.ToLane());
        }
    }
}
