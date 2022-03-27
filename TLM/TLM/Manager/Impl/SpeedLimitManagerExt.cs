namespace TrafficManager.Manager.Impl {
    /// <summary>Adds extra helper functions to NetSegment and NetInfo.Lane, etc.</summary>
    public static class SpeedLimitManagerExt {
        /// <summary>
        /// Extension method. Call via 'segment.MayHaveCustomSpeedLimits()`.
        /// Check whether custom speed limits may be assigned to the given segment.
        /// </summary>
        /// <param name="segment">Reference to affected segment.</param>
        /// <returns>True if this segment type can have custom speed limits set.</returns>
        public static bool MayHaveCustomSpeedLimits(this ref NetSegment segment) {
            if ((segment.m_flags & NetSegment.Flags.Created) == NetSegment.Flags.None) {
                return false;
            }

            // Collapsed roads cannot have speed limit, but will be restored if you upgrade in place
            if ((segment.m_flags & NetSegment.Flags.Collapsed) != NetSegment.Flags.None) {
                return false;
            }

            ItemClass connectionClass = segment.Info.GetConnectionClass();
            ItemClass.SubService subService = connectionClass.m_subService;
            ItemClass.Service service = connectionClass.m_service;

            return service == ItemClass.Service.Road
                   || (service == ItemClass.Service.PublicTransport
                       && subService
                           is ItemClass.SubService.PublicTransportTrain
                           or ItemClass.SubService.PublicTransportTram
                           or ItemClass.SubService.PublicTransportMetro
                           or ItemClass.SubService.PublicTransportMonorail);
        }

        /// <summary>
        /// Extension method. Call via 'segment.MayHaveCustomSpeedLimits()`.
        /// Check whether custom speed limits may be assigned to the given lane info.
        /// </summary>
        /// <param name="laneInfo">The <see cref="NetInfo.Lane"/> that you wish to check.</param>
        /// <returns>Returns <c>true</c> if speed limit can be customised, otherwise <c>false</c>.</returns>
        public static bool MayHaveCustomSpeedLimits(this NetInfo.Lane laneInfo)
            => laneInfo.m_finalDirection != NetInfo.Direction.None
            && (laneInfo.m_laneType & SpeedLimitManager.LANE_TYPES) != NetInfo.LaneType.None
            && (laneInfo.m_vehicleType & SpeedLimitManager.VEHICLE_TYPES) != VehicleInfo.VehicleType.None;
    }
}