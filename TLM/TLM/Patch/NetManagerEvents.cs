using HarmonyLib;
using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.Util.Extensions;

namespace TrafficManager.Patch {

    /// <summary>
    /// Patches <see cref="NetManager"/> for event handling.
    /// </summary>
    [HarmonyPatch(typeof(NetManager))]
    internal class NetManagerEvents {

        internal delegate void ReleasingLaneEventHandler(uint laneId, ref NetLane lane);
        internal delegate void ReleasingSegmentEventHandler(ushort segmentId, ref NetSegment segment);

        private NetManagerEvents() {
        }

        /// <summary>
        /// Occurs when a lane is about to be released.
        /// <see cref="NetLaneExtensions.IsValidWithSegment(ref NetLane)"/> usually still returns true when this event occurs.
        /// </summary>
        public event ReleasingLaneEventHandler ReleasingLane;

        /// <summary>
        /// Occurs when a lane has been released and is no longer valid.
        /// </summary>
        public event Action<uint> LaneReleased;

        /// <summary>
        /// Occurs when a segment is about to be released.
        /// <see cref="NetSegmentExtensions.IsValid(ref NetSegment)"/> usually still returns true when this event occurs.
        /// </summary>
        public event ReleasingSegmentEventHandler ReleasingSegment;

        /// <summary>
        /// Occurs when a segment has been released and is no longer valid.
        /// </summary>
        public event Action<ushort> SegmentReleased;

        public static NetManagerEvents Instance { get; } = new NetManagerEvents();

        [HarmonyPostfix]
        [HarmonyPatch("ReleaseLaneImplementation")]
        [UsedImplicitly]
        internal static void ReleaseLaneImplementationPostfix(uint lane) => Instance.LaneReleased?.Invoke(lane);

        [HarmonyPostfix]
        [HarmonyPatch(
            "ReleaseSegmentImplementation",
            new[] { typeof(ushort), typeof(NetSegment), typeof(bool) },
            new[] { ArgumentType.Normal, ArgumentType.Ref, ArgumentType.Normal }
        )]
        [UsedImplicitly]
        internal static void ReleaseSegmentImplementationPostfix(ushort segment) => Instance.SegmentReleased?.Invoke(segment);

        [HarmonyPrefix]
        [HarmonyPatch(
            "PreReleaseSegmentImplementation",
            new[] { typeof(ushort), typeof(NetSegment), typeof(bool) },
            new[] { ArgumentType.Normal, ArgumentType.Ref, ArgumentType.Normal }
        )]
        [UsedImplicitly]
        internal static bool PreReleaseSegmentImplementationPrefix(ushort segment, ref NetSegment data) {

            if ((data.m_flags & NetSegment.Flags.Deleted) == 0) {

                if (data.m_lanes != 0 && Instance.ReleasingLane != null) {
                    uint laneId = data.m_lanes;
                    do {
                        ref var lane = ref laneId.ToLane();
                        Instance.ReleasingLane(laneId, ref lane);
                        laneId = lane.m_nextLane;
                    } while (laneId != 0);
                }

                Instance.ReleasingSegment?.Invoke(segment, ref data);
            }
            return true;
        }
    }
}
