namespace TrafficManager.UI.SubTools.SpeedLimits {
    using ColossalFramework;
    using TrafficManager.API.Traffic.Data;
    using TrafficManager.Manager.Impl;
    using TrafficManager.Util;

    /// <summary>
    /// Describes a recently rendered speed icon on the speed limits overlay for SEGMENT.
    /// It is created while rendering, and if mouse is hovering over it, it is added to the list.
    /// Click is handled separately away from the rendering code.
    /// </summary>
    public readonly struct OverlaySegmentSpeedlimitHandle {
        /// <summary>Segment id where the speedlimit sign was displayed.</summary>
        public readonly ushort SegmentId;

        /// <summary>Segment side, where the speedlimit sign was.</summary>
        public readonly NetInfo.Direction FinalDirection;

        public OverlaySegmentSpeedlimitHandle(ushort segmentId,
                                              NetInfo.Direction finalDirection) {
            SegmentId = segmentId;
            FinalDirection = finalDirection;
        }

        /// <summary>
        /// Called when mouse is down, and when mouse is not in parent tool window area.
        /// The show per lane mode is disabled and editing per segment.
        /// </summary>
        public void Click(in SetSpeedLimitAction action,
                          bool multiSegmentMode) {
            // change the speed limit to the selected one
            SpeedLimitManager.Instance.SetSpeedLimit(
                segmentId: this.SegmentId,
                finalDir: this.FinalDirection,
                action: action);

            if (multiSegmentMode) {
                ClickMultiSegment(action);
            }
        }

        /// <summary>
        /// Called if speed limit icon was clicked in segment display mode,
        /// but also multisegment mode was enabled (like holding Shift).
        /// </summary>
        /// <param name="speedLimitToSet">The active speed limit on the palette.</param>
        private void ClickMultiSegment(SetSpeedLimitAction action) {
            NetManager netManager = Singleton<NetManager>.instance;

            if (new RoundaboutMassEdit().TraverseLoop(this.SegmentId, out var segmentList)) {
                foreach (ushort segId in segmentList) {
                    SpeedLimitManager.Instance.SetSpeedLimit(segId, action);
                }

                return;
            }

            NetInfo.Direction normDir = this.FinalDirection;
            NetSegment[] segmentsBuffer = netManager.m_segments.m_buffer;

            if ((segmentsBuffer[this.SegmentId].m_flags &
                 NetSegment.Flags.Invert) != NetSegment.Flags.None) {
                normDir = NetInfo.InvertDirection(normDir);
            }

            // Called for each lane in the traversed street
            bool ForEachSegmentFun(SegmentLaneTraverser.SegmentLaneVisitData data) {
                if (data.SegVisitData.Initial) {
                    return true;
                }

                bool reverse = data.SegVisitData.ViaStartNode ==
                               data.SegVisitData.ViaInitialStartNode;

                ushort otherSegmentId = data.SegVisitData.CurSeg.segmentId;
                NetInfo otherSegmentInfo = segmentsBuffer[otherSegmentId].Info;
                byte laneIndex = data.CurLanePos.laneIndex;
                NetInfo.Lane laneInfo = otherSegmentInfo.m_lanes[laneIndex];

                NetInfo.Direction otherNormDir = laneInfo.m_finalDirection;

                NetSegment.Flags invertFlag = segmentsBuffer[otherSegmentId].m_flags
                                              & NetSegment.Flags.Invert;

                if ((invertFlag != NetSegment.Flags.None) ^ reverse) {
                    otherNormDir = NetInfo.InvertDirection(otherNormDir);
                }

                if (otherNormDir == normDir) {
                    SpeedLimitManager.Instance.SetSpeedLimit(
                        segmentId: otherSegmentId,
                        finalDir: laneInfo.m_finalDirection,
                        action: action);
                }

                return true;
            }

            SegmentLaneTraverser.Traverse(
                initialSegmentId: this.SegmentId,
                direction: SegmentTraverser.TraverseDirection.AnyDirection,
                side: SegmentTraverser.TraverseSide.AnySide,
                laneStopCrit: SegmentLaneTraverser.LaneStopCriterion.LaneCount,
                segStopCrit: SegmentTraverser.SegmentStopCriterion.Junction,
                laneTypeFilter: SpeedLimitManager.LANE_TYPES,
                vehicleTypeFilter: SpeedLimitManager.VEHICLE_TYPES,
                laneVisitor: ForEachSegmentFun);
        } // end Click MultiSegment
    }
    // end struct
} // end namespace