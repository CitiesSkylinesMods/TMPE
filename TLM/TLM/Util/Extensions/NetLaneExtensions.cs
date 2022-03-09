namespace TrafficManager.Util.Extensions {
    using ColossalFramework;

    public static class NetLaneExtensions {
        private static NetLane[] _laneBuffer = Singleton<NetManager>.instance.m_lanes.m_buffer;

        internal static ref NetLane ToLane(this uint laneId) => ref _laneBuffer[laneId];

        /// <summary>
        /// Checks <paramref name="netLane"/> is <c>Created</c> but not <c>Deleted</c>.
        /// </summary>
        /// <param name="netLane">netLane</param>
        /// <returns>Returns <c>true</c> if valid, otherwise <c>false</c>.</returns>
        public static bool IsValid(this ref NetLane netLane) =>
            ((NetLane.Flags)netLane.m_flags).CheckFlags(
                required: NetLane.Flags.Created,
                forbidden: NetLane.Flags.Deleted);

        /// <summary>
        /// Checks <paramref name="laneId"/> is not <c>0</c>,
        /// then checks netLane is <c>Created</c> but not <c>Deleted</c>.
        /// </summary>
        /// <param name="laneId">The id of the lane to check.</param>
        /// <returns>Returns <c>true</c> if valid, otherwise <c>false</c>.</returns>
        public static bool IsValid(this uint laneId)
            => (laneId != 0) && laneId.ToLane().IsValid();

        /// <summary>
        /// Checks <paramref name="netLane"/> is <c>Created</c> but not <c>Deleted</c>,
        /// then checks its netSegment is <c>Created</c> but not <c>Collapsed|Deleted</c>.
        /// </summary>
        /// <param name="netLane">netLane</param>
        /// <returns>Returns <c>true</c> if valid, otherwise <c>false</c>.</returns>
        public static bool IsValidWithSegment(this ref NetLane netLane) {
            return netLane.IsValid()
                && netLane.m_segment.ToSegment().IsValid();
        }

        /// <summary>
        /// Checks <paramref name="laneId"/> is not <c>0</c>,
        /// then checks netLane is <c>Created</c> but not <c>Deleted</c>,
        /// then checks its segmentId is not <c>0</c>,
        /// then checks its netSegment is <c>Created</c> but not <c>Collapsed|Deleted</c>.
        /// </summary>
        /// <param name="laneId">The id of the lane to check.</param>
        /// <returns>Returns <c>true</c> if valid, otherwise <c>false</c>.</returns>
        public static bool IsValidWithSegment(this uint laneId) {
            if (laneId == 0)
                return false;

            ref NetLane netLane = ref laneId.ToLane();

            if (!netLane.IsValid())
                return false;

            return netLane.m_segment.IsValid();
        }
    }
}
