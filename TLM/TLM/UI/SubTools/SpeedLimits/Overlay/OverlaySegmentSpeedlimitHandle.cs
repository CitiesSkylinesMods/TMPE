namespace TrafficManager.UI.SubTools.SpeedLimits.Overlay {
    using System;
    using ColossalFramework;
    using CSUtil.Commons;
    using TrafficManager.Manager.Impl;
    using TrafficManager.State;
    using TrafficManager.Util;

    /// <summary>
    /// Describes a recently rendered speed icon on the speed limits overlay for SEGMENT.
    /// It is created while rendering, and if mouse is hovering over it, it is added to the list.
    /// Click is handled separately away from the rendering code.
    /// </summary>
    public readonly struct OverlaySegmentSpeedlimitHandle {
        /// <summary>Segment id where the speedlimit sign was displayed.</summary>
        private readonly ushort segmentId_;

        /// <summary>Segment side, where the speedlimit sign was.</summary>
        private readonly NetInfo.Direction finalDirection_;

        public OverlaySegmentSpeedlimitHandle(ushort segmentId,
                                              NetInfo.Direction finalDirection) {
            this.segmentId_ = segmentId;
            this.finalDirection_ = finalDirection;
        }

        /// <summary>
        /// Called when mouse is down, and when mouse is not in parent tool window area.
        /// The show per lane mode is disabled and editing per segment.
        /// </summary>
        /// <param name="action">What speed limit to set or clear.</param>
        /// <param name="target">The speed limit destination object (override or default, segment or lane).</param>
        /// <param name="multiSegmentMode">True if user holds Shift to edit the road.</param>
        public void Click(in SetSpeedLimitAction action,
                          SetSpeedLimitTarget target,
                          bool multiSegmentMode) {
            NetManager netManager = Singleton<NetManager>.instance;
            NetSegment[] segmentsBuffer = netManager.m_segments.m_buffer;

            Apply(
                segmentId: this.segmentId_,
                finalDir: this.finalDirection_,
                netInfo: segmentsBuffer[this.segmentId_].Info,
                action: action,
                target: target);

            if (multiSegmentMode) {
                this.ClickMultiSegment(action, target);
            }
        }

        /// <summary>
        /// Called if speed limit icon was clicked in segment display mode,
        /// but also multisegment mode was enabled (like holding Shift).
        /// </summary>
        /// <param name="action">The active speed limit on the palette.</param>
        private void ClickMultiSegment(SetSpeedLimitAction action,
                                       SetSpeedLimitTarget target) {
            NetManager netManager = Singleton<NetManager>.instance;

            if (new RoundaboutMassEdit().TraverseLoop(this.segmentId_, out var segmentList)) {
                foreach (ushort segId in segmentList) {
                    SpeedLimitManager.Instance.SetSegmentSpeedLimit(segId, action);
                }

                return;
            }

            NetInfo.Direction normDir = this.finalDirection_;
            NetSegment[] segmentsBuffer = netManager.m_segments.m_buffer;

            if ((segmentsBuffer[this.segmentId_].m_flags &
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
                    Apply(
                        segmentId: otherSegmentId,
                        finalDir: laneInfo.m_finalDirection,
                        netInfo: otherSegmentInfo,
                        action: action,
                        target: target);
                }

                return true;
            }

            SegmentLaneTraverser.Traverse(
                initialSegmentId: this.segmentId_,
                direction: SegmentTraverser.TraverseDirection.AnyDirection,
                side: SegmentTraverser.TraverseSide.AnySide,
                laneStopCrit: SegmentLaneTraverser.LaneStopCriterion.LaneCount,
                segStopCrit: SegmentTraverser.SegmentStopCriterion.Junction,
                laneTypeFilter: SpeedLimitManager.LANE_TYPES,
                vehicleTypeFilter: SpeedLimitManager.VEHICLE_TYPES,
                laneVisitor: ForEachSegmentFun);
        } // end Click MultiSegment

        /// <summary>
        /// Based on target value, applies speed limit to a segmet or default for that road type.
        /// </summary>
        /// <param name="netInfo">For defaults, will set default speed limit for that road type.</param>
        private static void Apply(ushort segmentId,
                                  NetInfo.Direction finalDir,
                                  NetInfo netInfo,
                                  SetSpeedLimitAction action,
                                  SetSpeedLimitTarget target) {
            switch (target) {
                case SetSpeedLimitTarget.SegmentOverride:
                    SpeedLimitManager.Instance.SetSegmentSpeedLimit(segmentId, finalDir, action);
                    break;
                case SetSpeedLimitTarget.SegmentDefault:
                    // SpeedLimitManager.Instance.FixCurrentSpeedLimits(netInfo);
                    if (action.Override.HasValue) {
                        SpeedLimitManager.Instance.SetCustomNetInfoSpeedLimit(
                            info: netInfo,
                            customSpeedLimit: action.Override.Value.GameUnits);
                    }

                    break;
                default:
                    Log.Error(
                        $"Target for SEGMENT speed handle click is not supported {nameof(target)}");
                    throw new NotSupportedException();
            }
        }
    } // end struct
} // end namespace