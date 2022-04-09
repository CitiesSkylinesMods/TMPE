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
    /// <remarks>
    /// <para>
    /// Note on event naming conventions: These events shall typically appear in pairs.
    /// The present participle (e.g. <see cref="ReleasingLane"/>) identifies a presently/imminently
    /// occurring event--that is, one that is about to or is in the process of happening. The past
    /// simple (e.g. <see cref="ReleasedLane"/>) identifies an event that is already completed. In
    /// most cases, these respectively correspond to prefix and postfix Harmony patches.
    /// </para>
    /// </remarks>
    [HarmonyPatch(typeof(NetManager))]
    internal class NetManagerEvents {

        internal delegate void ReleasingLaneEventHandler(uint laneId, ref NetLane lane);
        internal delegate void ReleasedLaneEventHandler(uint laneId);

        internal delegate void ReleasingSegmentEventHandler(ushort segmentId, ref NetSegment segment);
        internal delegate void ReleasedSegmentEventHandler(ushort segmentId);

        private NetManagerEvents() {
        }

        /// <summary>
        /// Occurs when a lane is about to be released.
        /// In most cases the lane is still valid when this event occurs, so that <see cref="NetLaneExtensions.IsValidWithSegment(ref NetLane)"/> will return true.
        /// </summary>
        public event ReleasingLaneEventHandler ReleasingLane;

        /// <summary>
        /// Occurs when a lane has been released and is no longer valid.
        /// </summary>
        public event ReleasedLaneEventHandler ReleasedLane;

        /// <summary>
        /// Occurs when a segment is about to be released.
        /// In most cases the segment is still valid when this event occurs, so that <see cref="NetSegmentExtensions.IsValid(ref NetSegment)"/> will return true.
        /// </summary>
        public event ReleasingSegmentEventHandler ReleasingSegment;

        /// <summary>
        /// Occurs when a segment has been released and is no longer valid.
        /// </summary>
        public event ReleasedSegmentEventHandler ReleasedSegment;

        public static NetManagerEvents Instance { get; } = new NetManagerEvents();

        [HarmonyPostfix]
        [HarmonyPatch("ReleaseLaneImplementation")]
        [UsedImplicitly]
        internal static void ReleaseLaneImplementationPostfix(uint lane) => Instance.ReleasedLane?.Invoke(lane);

        [HarmonyPostfix]
        [HarmonyPatch(
            "ReleaseSegmentImplementation",
            new[] { typeof(ushort), typeof(NetSegment), typeof(bool) },
            new[] { ArgumentType.Normal, ArgumentType.Ref, ArgumentType.Normal }
        )]
        [UsedImplicitly]
        internal static void ReleaseSegmentImplementationPostfix(ushort segment) => Instance.ReleasedSegment?.Invoke(segment);

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
